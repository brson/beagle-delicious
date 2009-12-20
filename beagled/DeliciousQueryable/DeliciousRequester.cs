// DeliciousRequester.cs
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
using System.Net;
using System.Text;
using System.Xml;
using Beagle.Util;

using Debug = System.Diagnostics.Debug;

namespace Beagle.Daemon.DeliciousQueryable
{
	/// <summary>
	/// Thrown when delicious returns a 503 error, meaning we should stop making requests
	/// </summary>
	class DeliciousUnavailableException : ApplicationException
	{
		public DeliciousUnavailableException ()
			: base ("Delicious is currently unavailable")
		{
		}
	}
	
	/// <summary>
	/// Performs all HTTP requests to delicious
	/// </summary>
	class DeliciousRequester
	{
		readonly string GET_UPDATE_TIME = "https://api.del.icio.us/v1/posts/update";
		readonly string GET_ALL_HASHES = "https://api.del.icio.us/v1/posts/all?hashes";
		readonly string GET_POSTS_FROM_HASHES = "https://api.del.icio.us/v1/posts/get?meta=yes&hashes=";

		Account account;
		RateLimiter rateLimiter = new RateLimiter ();

		public DeliciousRequester (Account account)
		{
			Debug.Assert (account.IsValid);
			this.account = account;
		}

		public Account Account
		{
			get
			{
				return account;
			}
		}			

		public XmlReader GetUpdateTimeReader ()
		{
			return GetResponseReader (GET_UPDATE_TIME);
		}

		public XmlReader GetAllHashesReader ()
		{
			return GetResponseReader (GET_ALL_HASHES);
		}

		public XmlReader GetPostsFromHashes (IEnumerable <string> hashes)
		{
			Debug.Assert (hashes != null);

			string hashParam = BuildHashParam (hashes);
			return GetResponseReader (GET_POSTS_FROM_HASHES + hashParam);
		}

		string BuildHashParam (IEnumerable <string> hashes)
		{
			StringBuilder hashParam = new StringBuilder ();
			foreach (string hash in hashes)
			{
				hashParam.Append (hash).Append("+");
			}
			// Remove the extra "+"
			if (hashParam.Length > 0)
			{
				hashParam.Remove (hashParam.Length - 1, 1);
			}

			return hashParam.ToString ();
		}

		XmlReader GetResponseReader (string url)
		{
			rateLimiter.WaitForNextTurn ();

			HttpWebRequest request = CreateWebRequest (url);

			try {
				HttpWebResponse response = request.GetResponse () as HttpWebResponse;
				return new XmlTextReader (response.GetResponseStream ());
			}
			catch (WebException e)
			{
				if ( e.Status == WebExceptionStatus.TrustFailure )
				{
					throw new ApplicationException ("Couldn't establish SSL connection with delicious. Make sure you have the appropriate root certificates installed", e);
				}
				else if ( e.Status == WebExceptionStatus.ProtocolError )
				{
					if ((e.Response as HttpWebResponse).StatusCode == HttpStatusCode.ServiceUnavailable)
					{
						throw new DeliciousUnavailableException ();
					}
				}
				throw;
			}
		}

		public HttpWebRequest CreateWebRequest (string url)
		{
			HttpWebRequest request = HttpWebRequest.Create (url) as HttpWebRequest;
			request.Credentials = new NetworkCredential (account.Username, account.Password);
			request.UserAgent = "Beagle Delicious Bot";
			return request;
		}
	}
}
