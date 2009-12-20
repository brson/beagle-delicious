// Model.cs
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

namespace Beagle.Daemon.DeliciousQueryable
{
	struct Account
	{
		public string Username;
		public string Password;

		public Account (string username, string password)
		{
			Debug.Assert (username != null);
			Debug.Assert (password != null);

			this.Username = username;
			this.Password = password;
		}

		public static Account InvalidAccount
		{
			get
			{
				return new Account (string.Empty, string.Empty);
			}
		}

		public bool IsValid
		{
			get
			{
				return Username != string.Empty;
			}
		}
	}
	
	/// <summary>
	/// Maintains state of the sync process.
	///
	/// The sync state is only valid for a single crawl, afterwhich a new sync state
	/// must be loaded.
	/// </summary>
	class AccountSyncState
	{
		public Account Account;
		// The server's reported update time during the last successful sync.
		// Used to determine if the index needs to be updated.
		public DateTime LastSyncTime;
		// Information about the last remote update. Also used to determine
		// if the index needs to be updated
		public UpdateInfo RemoteUpdateInfo;
		// Meta information about each bookmark in the local index
		public LocalManifest LocalManifest = new LocalManifest ();
		// Meta information about each bookmark on delicious
		public RemoteManifest RemoteManifest = new RemoteManifest ();

		public AccountSyncState (Account account)
		{
			Debug.Assert (account.IsValid);
			this.Account = account;
		}

		public bool ShouldCrawl ()
		{
			return RemoteUpdatesAvailable ()
				|| ShouldRefreshContent ();
		}

		bool RemoteUpdatesAvailable ()
		{
			return LastSyncTime != RemoteUpdateInfo.LastUpdateTime;
		}
		
		// If there are local entries that haven't been updated in a while
		// then we know we should crawl to refresh them
		bool ShouldRefreshContent ()
		{
			foreach (LocalManifestEntry entry in LocalManifest.Values)
			{
				if (entry.IsExpired)
				{
					return true;
				}
			}
			return false;
		}

		public void UpdateLastSyncTime ()
		{
			LastSyncTime = RemoteUpdateInfo.LastUpdateTime;
		}
	}

	/// <summary>
	/// The collection of bookmarks, as indexed locally
	/// </summary>
	class LocalManifest : Dictionary <string, LocalManifestEntry>
	{
		public LocalManifest Clone ()
		{
			LocalManifest newManifest = new LocalManifest ();
			foreach (KeyValuePair <string, LocalManifestEntry> pair in this)
			{
				newManifest [pair.Key] = pair.Value;
			}
			return newManifest;
		}
	}

	/// <summary>
	/// The collection of bookmarks, as known to delicious
	/// </summary>
	class RemoteManifest : List <RemoteManifestEntry>
	{
	}

	/// <summary>
	/// Contains the information about a locally-indexed bookmark that is
	/// necessary when determining if the bookmark needs to be re-indexed.
	/// </summary>
	/// <remarks>
	/// This is public so it can be serialized.
	/// </remarks>
	public struct LocalManifestEntry
	{
		// Entries should be refreshed after 30 days
		static readonly TimeSpan EXPIRATION = new TimeSpan (30, 0, 0, 0);

		public string Url;
		// The hash of the URL is compared to one provided by delicious
		// to dermine if re-indexing is needed
		public string UrlHash;
		// Likewise for the hash of meta-data associated with the bookmark
		public string MetaHash;
		// The last time the content of this entry was indexed, using the local clock.
		// We can use this to periodically reindex bookmark content to
		// keep it from getting stale
		public DateTime LastIndexTime;

		// Expired entries should be re-indexed
		public bool IsExpired
		{
			get
			{
				return LastIndexTime + LocalManifestEntry.EXPIRATION < DateTime.Now;
			}
		}
	}

	/// <summary>
	/// Meta data, provided by delicious, about a single bookmark. When this
	/// information differs from the local copy, the bookmark needs to be
	/// re-indexed
	/// </summary>
	struct RemoteManifestEntry
	{
		public string UrlHash;
		public string MetaHash;
	}

	/// <summary>
	/// A single delicious bookmark
	/// </summary>
	struct Post
	{
		// Address of the bookmarked content
		public string Href;
		public string Description;
		// Extended description
		public string Extended;
		// Space-separated tags
		public string Tag;
		// Hash of the URL
		public string Hash;
		// Hash of the bookmark meta-data
		public string Meta;
		// Time of last update
		public DateTime Time;
		// Not sure what this is
		public string Others;
	}

	/// <summary>
	/// Information provided by delicous about the last update.
	/// Use this to determine if there is anything new to index.
	/// </summary>
	struct UpdateInfo
	{
		// The time of the last update
		public DateTime LastUpdateTime;
		// Don't remember what this is, but we don't use it
		public int InboxNew;
	}

}
