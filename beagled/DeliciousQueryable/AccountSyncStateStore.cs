// AccountSyncStateStore.cs
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
using System.IO;
using System.Xml.Serialization;
using Beagle.Util;

using Debug = System.Diagnostics.Debug;

namespace Beagle.Daemon.DeliciousQueryable
{
	
	/// <summary>
	/// Responsible for pulling data from local and remote sources
	/// to create the account sync state before crawling. Also persists
	/// the sync state to disk after the crawl.
	/// </summary>
	class AccountSyncStateStore
	{
		DeliciousRequester requester;
		RemoteDataAccess remoteDao;
		DeliciousQueryable queryable;
		
		public AccountSyncStateStore(
			DeliciousRequester requester,
			RemoteDataAccess remoteDao,
			DeliciousQueryable queryable)
		{
			Debug.Assert (requester != null);
			Debug.Assert (remoteDao != null);
			Debug.Assert (queryable != null);

			this.requester = requester;
			this.remoteDao = remoteDao;
			this.queryable = queryable;
		}

		// This may throw a DeliciousUnavailableException
		public AccountSyncState LoadSyncState ()
		{
			Debug.Assert (requester.Account.IsValid);
			AccountSyncState syncState = new AccountSyncState (requester.Account);

			LoadLastSyncTime (syncState);
			LoadRemoteUpdateInfo (syncState);
			LoadLocalManifest (syncState);

			// Avoid an HTTP request if the remote manifest isn't needed.
			// ShouldCrawl depends on the sync time, remote update info, and local manifest.
			if (syncState.ShouldCrawl ())
			{
				LoadRemoteManifest (syncState);
			}
			else
			{
				Log.Debug ("Skipping remote manifest");
			}

			return syncState;
		}

		void LoadLastSyncTime (AccountSyncState syncState)
		{
			Debug.Assert (syncState.Account.IsValid);
			
			try
			{
				string lastSyncTimeString = queryable.ReadDataLine (syncState.Account.Username);
				syncState.LastSyncTime = DateTime.Parse (lastSyncTimeString);
			}
			catch
			{
				syncState.LastSyncTime = DateTime.MinValue;
			}
			
			Log.Info ("Last sync time: " + syncState.LastSyncTime);
		}

		// TODO: Instead of loading from a separate text file, we should just build
		// the local manifest from Beagle's index
		void LoadLocalManifest (AccountSyncState syncState)
		{
			Debug.Assert (syncState.Account.IsValid);
			Debug.Assert (syncState.LocalManifest != null);
			Debug.Assert (syncState.LocalManifest.Count == 0);

			IList <LocalManifestEntry> manifestList;

			Log.Debug ("Loading local manifest");

			try
			{
				using (Stream manifestStream = queryable.ReadDataStream (syncState.Account.Username + ".manifest"))
				{
					using (StreamReader reader = new StreamReader (manifestStream))
					{
						XmlSerializer serializer = new XmlSerializer (typeof (List <LocalManifestEntry>));
						manifestList = serializer.Deserialize (reader) as IList <LocalManifestEntry>;
					}
				}

				foreach (LocalManifestEntry entry in manifestList)
				{
					syncState.LocalManifest.Add (entry.UrlHash, entry);
				}
			}
			catch
			{
				// No local manifest
			}
			
			Log.Info ("Loaded " + syncState.LocalManifest.Count + " manifest entries");
		}

		void LoadRemoteUpdateInfo (AccountSyncState syncState)
		{
			syncState.RemoteUpdateInfo = remoteDao.GetUpdateInfo ();
			
			Log.Info ("Last remote update: " + syncState.RemoteUpdateInfo.LastUpdateTime);
		}
		
		void LoadRemoteManifest (AccountSyncState syncState)
		{
			Debug.Assert (syncState.RemoteManifest != null);
			Debug.Assert (syncState.RemoteManifest.Count == 0);
			
			Log.Debug ("Downloading remote manifest");

			foreach (RemoteManifestEntry entry in remoteDao.GetAllManifestEntries ())
			{
				syncState.RemoteManifest.Add (entry);
			}
			
			Log.Info ("Downloaded " + syncState.RemoteManifest.Count + " manifest entries");
		}

		public void SaveSyncState (AccountSyncState syncState)
		{
			SaveLocalManifest (syncState);
			SaveLastSyncTime (syncState);
		}

		void SaveLastSyncTime (AccountSyncState syncState)
		{
			queryable.WriteDataLine (syncState.Account.Username, syncState.LastSyncTime.ToString());
		}

		void SaveLocalManifest (AccountSyncState syncState)
		{
			IList <LocalManifestEntry> manifestList = new List <LocalManifestEntry>();
			foreach (KeyValuePair <string, LocalManifestEntry> pair in syncState.LocalManifest)
			{
				manifestList.Add (pair.Value);
			}

			using (Stream manifestStream = queryable.WriteDataStream (syncState.Account.Username + ".manifest"))
			{
				using (StreamWriter writer = new StreamWriter (manifestStream))
				{
					XmlSerializer serializer = new XmlSerializer (manifestList.GetType ());
					serializer.Serialize (writer, manifestList);
				}
			}
		}
	}
}
