// IndexableBuilder.cs
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
using System.Net;
using Beagle.Daemon;
using Beagle.Util;

using Debug = System.Diagnostics.Debug;

namespace Beagle.Daemon.DeliciousQueryable
{
	/// <summary>
	/// Converts CrawlCommands to Indexables
	/// </summary>	
	class IndexableBuilder
	{
		DeliciousRequester requester;
		
		public IndexableBuilder (DeliciousRequester requester)
		{
			Debug.Assert (requester != null);

			this.requester = requester;
		}

		public Indexable Build (CrawlCommand command)
		{
			Debug.Assert (command != null);

			switch (command.Action)
			{
				case CrawlAction.Add:
					return BuildAddIndexable (command.Post);
				case CrawlAction.Update:
					return BuildUpdateIndexable (command.Post);
				case CrawlAction.Remove:
					return BuildRemoveIndexable (command.Post);
				default:
					Debug.Fail ("Unexpected action");
					return null;
			}
		}

		Indexable BuildAddIndexable (Post post)
		{
			Indexable indexable = new Indexable (IndexableType.Add, new Uri(post.Href));
			indexable.Timestamp = post.Time;
			indexable.Filtering = IndexableFiltering.Always;
			indexable.HitType = "Bookmark";

			indexable.AddProperty (Property.New ("dc:title", post.Description));
			indexable.AddProperty (Property.New ("dc:source", post.Href));
			indexable.AddProperty (Property.New ("dc:keywords", post.Tag));
			indexable.AddProperty (Property.New ("fixme:keywords", post.Tag));
			indexable.AddProperty (Property.New ("fixme:host", new Uri (post.Href).Host));
			// May want to support multiple accounts eventually
			indexable.AddProperty (Property.New ("delicious:account", requester.Account.Username));

			AddContentToIndexable (indexable, post);

			return indexable;
		}

		void AddContentToIndexable (Indexable indexable, Post post)
		{
			try
			{
				HttpWebRequest request = requester.CreateWebRequest (indexable.Uri.OriginalString);
				HttpWebResponse response = request.GetResponse () as HttpWebResponse;
	
				if (response.StatusCode == HttpStatusCode.OK )
				{
					indexable.MimeType = FilterContentType (response.Headers ["Content-Type"]);
					indexable.SetBinaryStream (response.GetResponseStream ());
				}
			} catch (WebException e)
			{
				// Can't index the content of the bookmark at this time.
				Log.Warn (e, "Error retrieving content for " + indexable.Uri.ToString ());
			}
		}

		// Snip encoding information from the HTTP Content-Type field
		string FilterContentType (string contentType)
		{
			if (! String.IsNullOrEmpty (contentType))
			{
				return contentType.Split (';') [0];
			}
			else
			{
				return contentType;
			}
		}

		Indexable BuildUpdateIndexable (Post post)
		{
			// Not sure if I should do something different when updating
			return BuildAddIndexable (post);
		}

		Indexable BuildRemoveIndexable (Post post)
		{
			return new Indexable (IndexableType.Remove, new Uri (post.Href));
		}
	}
}
