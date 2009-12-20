// RemoteDataAccess.cs
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
using System.Xml;
using System.Text;

namespace Beagle.Daemon.DeliciousQueryable
{

	/// <summary>
	/// Builds objects from the responses returned by the delicious web services
	/// </summary>
	class RemoteDataAccess
	{
		DeliciousRequester requester;

		public RemoteDataAccess (DeliciousRequester requester)
		{
			Debug.Assert (requester != null);
			this.requester = requester;
		}

		public UpdateInfo GetUpdateInfo ()
		{
			UpdateInfo updateInfo = new UpdateInfo ();

			ParseNodes(
				requester.GetUpdateTimeReader (),
				"update",
				reader => {
					updateInfo.LastUpdateTime = DateTime.Parse (reader.GetAttribute ("time"));
					updateInfo.InboxNew = Int32.Parse (reader.GetAttribute ("inboxnew"));
				});

			return updateInfo;
		}

		public IEnumerable <RemoteManifestEntry> GetAllManifestEntries ()
		{
			IList <RemoteManifestEntry> entries = new List <RemoteManifestEntry> ();

			ParseNodes(
				requester.GetAllHashesReader (),
				"post",
				reader => {
					RemoteManifestEntry entry = new RemoteManifestEntry ();

					entry.MetaHash = reader.GetAttribute ("meta");
					entry.UrlHash = reader.GetAttribute ("url");

					entries.Add (entry);
				});

			return entries;
		}

		public IEnumerable <Post> GetPostsFromEntries (IEnumerable <RemoteManifestEntry> entries)
		{
			Debug.Assert (entries != null);

			IList <Post> posts = new List <Post> ();

			ParseNodes(
				requester.GetPostsFromHashes (MakeHashList (entries)),
				"post",
				reader => {
					Post post = new Post ();

					post.Href = reader.GetAttribute ("href");
					post.Description = reader.GetAttribute ("description");
					post.Extended = reader.GetAttribute ("extended");
					post.Hash = reader.GetAttribute ("hash");
					post.Meta = reader.GetAttribute ("meta");
					post.Time = DateTime.Parse (reader.GetAttribute ("time"));
					post.Others = reader.GetAttribute ("others");
					post.Tag = reader.GetAttribute ("tag");

					posts.Add (post);
				});

			return posts;
		}

		IEnumerable <string> MakeHashList (IEnumerable <RemoteManifestEntry> entries)
		{
			var hashes = new List <string> ();
			foreach (RemoteManifestEntry entry in entries)
			{
				hashes.Add (entry.UrlHash);
			}
			return hashes;
		}

		void ParseNodes (XmlReader reader, string name, Action <XmlReader> action)
		{
			using (reader)
			{
				while (reader.Read ())
				{
					if (reader.Name == name)
					{
						action (reader);
					}
				}
			}
		}
	}
}
