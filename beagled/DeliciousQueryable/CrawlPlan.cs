// CrawlPlan.cs
//
// Copyright (c) 2009 Brian Anderson <andersrb@gmail.com>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Beagle.Util;

using Debug = System.Diagnostics.Debug;

namespace Beagle.Daemon.DeliciousQueryable
{
	/// <summary>
	/// A directive describing a single step in a CrawlPlan
	/// </summary>
	class CrawlCommand
	{
		public CrawlAction Action;
		public Post Post;
	}

	enum CrawlAction
	{
		Add,
		Update,
		Remove
	}


	/// <summary>
	/// Syncing the local index to the remote set of delicious bookmarks is done
	/// by following a set of indexing steps, called the crawl plan. The crawl
	/// plan is constructed by comparing the contents of a local manifest and the
	/// contents of a remote manifest for discrepancies, and either adding, updating, 
	/// or removing Indexables as needed.
	/// </summary>
	class CrawlPlan
	{
		// Since retrieving all the Post objects necessary to fill the CrawlCommands
		// may require hitting delicious many times, we'll defer that step
		// until needed. PartialCrawlCommands will be turned into CrawlCommands
		// as needed.
		class PartialCrawlCommand
		{
			public CrawlAction Action;
			public RemoteManifestEntry Entry;
		}

		// The maximum number of posts we can request is limited mainly by URL length.
		static readonly int MAX_POSTS_PER_PULL = 20;

		Queue <PartialCrawlCommand> partialCommands = new Queue <PartialCrawlCommand>();
		Queue <CrawlCommand> commands = new Queue <CrawlCommand> ();
		RemoteDataAccess dao;

		public CrawlPlan (LocalManifest localManifest, RemoteManifest remoteManifest, RemoteDataAccess dao)
		{
			Debug.Assert (localManifest != null);
			Debug.Assert (remoteManifest != null);
			Debug.Assert (dao != null);

			this.dao = dao;

			// Clone the local manifest, because we need to make changes to this copy
			BuildCrawlPlan (localManifest.Clone (), remoteManifest);
		}

		void BuildCrawlPlan (LocalManifest localManifest, RemoteManifest remoteManifest)
		{
			Log.Debug ("Building crawl plan");

			foreach (RemoteManifestEntry remoteEntry in remoteManifest)
			{
				PartialCrawlCommand newCommand = PreparePartialCommandFromRemoteEntry (remoteEntry, localManifest);
				if (newCommand != null)
				{
					partialCommands.Enqueue (newCommand);
				}

				// Remove entries from the local manifest, so we can later
				// tell which entries only exist locally
				if (localManifest.ContainsKey (remoteEntry.UrlHash))
				{
					localManifest.Remove (remoteEntry.UrlHash);
				}
			}

			// Any entries left in the local manifest don't exist remotely,
			// so they need to be removed
			foreach (KeyValuePair <string, LocalManifestEntry> entryPair in localManifest)
			{
				CrawlCommand newCommand = new CrawlCommand ();
				newCommand.Action = CrawlAction.Remove;
				newCommand.Post = BuildFakePost (entryPair.Value);
				commands.Enqueue (newCommand);
			}
		}

		PartialCrawlCommand PreparePartialCommandFromRemoteEntry(
			RemoteManifestEntry remoteEntry,
			IDictionary <string, LocalManifestEntry> localEntries)
		{
			PartialCrawlCommand newCommand = null;

			// If the local manifest contains the remote entry,
			// then update the local entry, otherwise add a new one
			if (localEntries.ContainsKey (remoteEntry.UrlHash))
			{
				LocalManifestEntry localEntry = localEntries [remoteEntry.UrlHash];

				// Update if the meta data has changed.
				// Also pdate periodically to index changes to remote site content
				if (localEntry.MetaHash != remoteEntry.MetaHash
					|| localEntry.IsExpired)
				{
					newCommand = new PartialCrawlCommand ();
					newCommand.Action = CrawlAction.Update;
					newCommand.Entry = remoteEntry;
				}
				else
				{
					// Don't need to do anything with this entry; it's already synchronized
				}
			}
			else
			{
				newCommand = new PartialCrawlCommand ();
				newCommand.Action = CrawlAction.Add;
				newCommand.Entry = remoteEntry;
			}

			return newCommand;
		}

		Post BuildFakePost (LocalManifestEntry entry)
		{
			Post fakePost = new Post ();
			fakePost.Href = entry.Url;
			fakePost.Hash = entry.UrlHash;
			fakePost.Meta = entry.MetaHash;
			return fakePost;
		}

		public bool HasNextCommand ()
		{
			return commands.Count > 0 || partialCommands.Count > 0;
		}

		// This may throw a DeliciousUnavailableException
		public CrawlCommand GetNextCommand ()
		{
			if (commands.Count > 0)
			{
				return commands.Dequeue ();
			}
			else
			{
				FillCommandQueue ();
				return null;
			}
		}

		void FillCommandQueue ()
		{
			IEnumerable <PartialCrawlCommand> nextPartialCommands = DequeueNextPartialCommands ();
			IDictionary <string, CrawlAction> urlActionDict = new Dictionary <string, CrawlAction> ();
			IList <RemoteManifestEntry> entryList = new List <RemoteManifestEntry> ();
			
			foreach (PartialCrawlCommand partialCommand in nextPartialCommands)
			{
				entryList.Add (partialCommand.Entry);
				urlActionDict [partialCommand.Entry.UrlHash] = partialCommand.Action;
			}

			try
			{
				foreach (Post post in dao.GetPostsFromEntries (entryList))
				{
					try
					{
						CrawlCommand newCommand = new CrawlCommand ();
						newCommand.Action = urlActionDict [post.Hash];
						newCommand.Post = post;
						commands.Enqueue (newCommand);
					}
					catch (KeyNotFoundException e)
					{
						// This one won't get indexed
						Log.Error (e, "Failed to create command for " + post);
					}
				}
			}
			catch (DeliciousUnavailableException)
			{
				// Let caller know we should stop
				throw;
			}
			catch (Exception e)
			{
				// A lot of stuff's not getting indexed
				Log.Error (e, "Failed to retrieve posts from delicious");
			}
		}

		IEnumerable <PartialCrawlCommand> DequeueNextPartialCommands ()
		{
			int commandsDequeued = 0;
			while (partialCommands.Count > 0
				&& commandsDequeued < MAX_POSTS_PER_PULL)
			{
				yield return partialCommands.Dequeue ();
				commandsDequeued++;
			}
		}
	}
}
