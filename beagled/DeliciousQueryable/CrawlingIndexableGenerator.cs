// CrawlingIndexableGenerator.cs
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
using System.Net;
using System.Threading;
using System.Xml;
using Beagle;
using Beagle.Daemon;
using Beagle.Util;

using Debug = System.Diagnostics.Debug;

namespace Beagle.Daemon.DeliciousQueryable
{
	/// <summary>
	/// Generates Indexables from a CrawlPlan while commiting each
	/// change to the local manifest
	/// </summary>
	class CrawlingIndexableGenerator : IIndexableGenerator
	{
		CrawlPlan crawlPlan;
		IndexableBuilder indexableBuilder;
		ManifestChangeTracker changeTracker;

		public event Action FinishedCrawl;

		public CrawlingIndexableGenerator(
			CrawlPlan crawlPlan,
			IndexableBuilder indexableBuilder,
			ManifestChangeTracker changeTracker )
		{
			Debug.Assert (crawlPlan != null);
			Debug.Assert (indexableBuilder != null);
			Debug.Assert (changeTracker != null);
			
			this.crawlPlan = crawlPlan;
			this.indexableBuilder = indexableBuilder;
			this.changeTracker = changeTracker;
		}

		public bool HasNextIndexable ()
		{
			bool hasNextIndexable;
			
			if (crawlPlan != null)
			{
				hasNextIndexable = crawlPlan.HasNextCommand ();
			}
			else
			{
				hasNextIndexable = false;
			}
			
			if (hasNextIndexable == false)
			{
				if (FinishedCrawl != null)
				{
					FinishedCrawl ();
					// Prevent event from possibly firing multiple times
					FinishedCrawl = null;
				}
			}
			return hasNextIndexable;
		}
		
		public Indexable GetNextIndexable ()
		{
			CrawlCommand command = null;

			try
			{
				Debug.Assert (crawlPlan != null);
				command = crawlPlan.GetNextCommand ();
			}
			catch (DeliciousUnavailableException e)
			{
				Log.Warn (e.ToString ());
				// By setting the crawl plan to null, HasNextIndexable will return false
				// and the crawl will end.
				crawlPlan = null;
			}

			if (command == null)
			{
				return null;
			}

			try
			{
				Indexable newIndexable = indexableBuilder.Build (command);
				if (newIndexable != null)
				{
					changeTracker.UpdateManifest (command);
				}
				return newIndexable;
			}
			catch (Exception e)
			{
				Log.Error (e, "Exception building indexable");
				return null;
			}
		}

		public string StatusName {
			get { return "Delicious crawl"; }
		}
		
		// Called each time a set of indexable is written to the index
		public void PostFlushHook () { }

	}
}
