// DeliciousQueryable.cs
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
using System.IO;
using System.Threading;

using Beagle;
using Beagle.Daemon;
using Beagle.Util;

namespace Beagle.Daemon.DeliciousQueryable
{
	/// <summary>
	/// Indexes the bookmarks of a delicious account, both meta-data and link content,
	/// in the global query domain.
	/// </summary>
	/// <remarks>
	/// The DeliciousQueryable operates by periodically performing a synchronization procedure,
	/// in which meta-data about the local index (the local manifest) and meta-data about
	/// the user's delicious bookmarks (the remote manifest) are compared, and discrepancies
	/// are resolved. The contents of bookmarks are also reindexed periodically.
	///
	/// The synchronization procedure is described by a CrawlPlan, which is responsible
	/// for determining how to perform the sync. CrawlPlans are created by a CrawlPlanPrepTask
	/// and executed by a CrawlingIndexableGenerator.
	/// </remarks>
	[QueryableFlavor (Name="Delicious", Domain=QueryDomain.Global, RequireInotify=false)]
	public class DeliciousQueryable : LuceneFileQueryable
	{
		public DeliciousQueryable () : base ("DeliciousIndex") { }

		public override void Start ()
		{
			base.Start ();
			ExceptionHandlingThread.Start (new ThreadStart (StartWorker));	
		}

		private void StartWorker()
		{
			ThisScheduler.Add (new CrawlPlanPrepTask (this));
		}
		
	}

}
