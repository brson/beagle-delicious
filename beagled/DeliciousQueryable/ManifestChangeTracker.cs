// ManifestChangeTracker.cs
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
using System.Diagnostics;
using Beagle.Util;

using Debug = System.Diagnostics.Debug;

namespace Beagle.Daemon.DeliciousQueryable
{
	
	/// <summary>
	/// Makes changes to the local manifest according to the crawl plan
	/// </summary>	
	class ManifestChangeTracker
	{
		LocalManifest localManifest;

		public ManifestChangeTracker (LocalManifest localManifest)
		{
			Debug.Assert (localManifest != null);
			this.localManifest = localManifest;
		}

		public void UpdateManifest (CrawlCommand command)
		{
			switch (command.Action)
			{
				case CrawlAction.Add:
					AddToManifest (command.Post);
					break;
				case CrawlAction.Update:
					UpdateManifest (command.Post);
					break;
				case CrawlAction.Remove:
					RemoveFromManifest (command.Post);
					break;
				default:
					Debug.Fail ("Unexpected action");
					break;
			}
		}

		void AddToManifest (Post post)
		{
			Log.Debug ("Adding " + post.Href);
			AddOrUpdateManifest (post);
		}

		void UpdateManifest (Post post)
		{
			Log.Debug ("Updating " + post.Href);
			AddOrUpdateManifest (post);
		}

		void AddOrUpdateManifest (Post post)
		{
			LocalManifestEntry entry;

			if (localManifest.ContainsKey (post.Hash))
			{
				entry = localManifest [post.Hash];
			}
			else
			{
				entry = new LocalManifestEntry();
			}

			entry.Url = post.Href;
			entry.UrlHash = post.Hash;
			entry.MetaHash = post.Meta;
			entry.LastIndexTime = DateTime.Now;

			localManifest [post.Hash] = entry;
		}

		void RemoveFromManifest (Post post)
		{
			Log.Debug ("Removing " + post.Href);
			Debug.Assert (localManifest.ContainsKey (post.Hash));
			localManifest.Remove (post.Hash);
		}
	}
}
