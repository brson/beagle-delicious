// CrawlPlanPrepTask.cs
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
using Beagle;
using Beagle.Daemon;
using Beagle.Util;

using Debug = System.Diagnostics.Debug;

namespace Beagle.Daemon.DeliciousQueryable
{
	/// <summary>
	/// Begins the sync process by preparing a crawl plan and creating an indexing task
	/// to execute the crawl plan.
	/// </summary>
	/// <remarks>
	/// Every time one crawl ends, either successfully or after failure, the 
	/// CrawlPlanPrepTask will reschedule itself for later.
	///
	/// If there is no account configuration, then the task aborts without rescheduling
	/// </remarks>
	class CrawlPlanPrepTask : Scheduler.Task
	{
		static readonly TimeSpan TIME_BETWEEN_CRAWLS = new TimeSpan(12, 0, 0);
		
		DeliciousQueryable queryable;
		Account account;

		public CrawlPlanPrepTask (DeliciousQueryable queryable)
		{
			Debug.Assert (queryable != null);
			
			this.queryable = queryable;
			
			this.Description = "Preparing to crawl delicious bookmarks";
			this.Tag = "Delicious crawl plan";
			this.Source = queryable;

			this.account = LoadAccountConfiguration ();
		}

		Account LoadAccountConfiguration ()
		{
			const string CONFIG_NAME = "Delicious";
			const string USERNAME_OPTION = "Username";
			const string PASSWORD_OPTION = "Password";

			Config config = Conf.Load (CONFIG_NAME);

			if (config == null)
			{
				config = Conf.LoadNew (CONFIG_NAME);
			}

			string username = config.GetOption (USERNAME_OPTION, null);
			string password = config.GetOption (PASSWORD_OPTION, null);

			// Save the configuration file if it doesn't exist, so the user can tell where it is
			if (username == null || password == null)
			{
				config.SetOption (USERNAME_OPTION, string.Empty);
				config.SetOption (PASSWORD_OPTION, string.Empty);
				Conf.Save (config);
			}

			if (string.IsNullOrEmpty (username) || string.IsNullOrEmpty (password))
			{
				return Account.InvalidAccount;
			}
			
			return new Account (username, password);
		}
		
		protected override void DoTaskReal ()
		{
			Reschedule = false;

			if (!account.IsValid)
			{
				Log.Info ("No delicious configuration");
				return;
			}

			CrawlingIndexableGenerator generator = BuildIndexableGenerator ();
			
			if (generator != null)
			{
				Scheduler.Task task = queryable.NewAddTask (generator);
				task.Description = "Crawling delicious bookmarks";
			
				queryable.ThisScheduler.Add (task);

				// Will reschedul after the crawl task finishes
			}
			else
			{
				RescheduleCrawl ();
			}
		}

		CrawlingIndexableGenerator BuildIndexableGenerator ()
		{
			Debug.Assert (account.IsValid);

			Log.Info ("Beginning sync for " + account.Username);

			// Create some common services
			DeliciousRequester requester = new DeliciousRequester (account);
			RemoteDataAccess remoteDao = new RemoteDataAccess (requester);

			// Load the sync state
			AccountSyncStateStore store = new AccountSyncStateStore (requester, remoteDao, queryable);
			AccountSyncState syncState = null;

			try
			{
				syncState = store.LoadSyncState ();
			}
			catch (DeliciousUnavailableException e)
			{
				Log.Warn (e.ToString());
				return null;
			}
			catch (Exception e)
			{
				Log.Error (e, "Error loading sync state");
				return null;
			}

			if (! syncState.ShouldCrawl ())
			{
				Log.Info ("Skipping crawl");
				return null;
			}

			// Build the crawl plan
			CrawlPlan crawlPlan = new CrawlPlan(
				syncState.LocalManifest,
				syncState.RemoteManifest,
				remoteDao);

			// Build indexables from the steps of the crawl plan
			IndexableBuilder indexableBuilder = new IndexableBuilder (requester);
			
			// Update the sync state's local manifest as bookmarks are indexed
			ManifestChangeTracker changeTracker = new ManifestChangeTracker (syncState.LocalManifest);
			
			// Create the indexable generator that will execute the crawl plan
			CrawlingIndexableGenerator indexableGenerator = new CrawlingIndexableGenerator(
				crawlPlan,
				indexableBuilder,
				changeTracker);

			// After the crawl is done, we'll need to save the sync state,
			// and set up another scheduled crawl
			indexableGenerator.FinishedCrawl +=()=> SaveStateAndReschedule (syncState, store, crawlPlan);

			return indexableGenerator;
		}

		void SaveStateAndReschedule (AccountSyncState syncState, AccountSyncStateStore store, CrawlPlan crawlPlan)
		{
			Log.Info ("Sync finished");
			
			// If the crawl plan completed successfully, then update the
			// last update time, otherwise leave the last update time the same
			// so that we can try to crawl again next time around.
			// The crawl may abort if delicious starts returning 503s
			if (! crawlPlan.HasNextCommand ())
			{
				syncState.UpdateLastSyncTime ();
			}
			
			try
			{
				store.SaveSyncState (syncState);
			}
			catch (Exception e)
			{
				Log.Error (e, "Error saving sync state");
			}

			RescheduleCrawl ();
		}

		void RescheduleCrawl ()
		{
			TriggerTime = DateTime.Now + TIME_BETWEEN_CRAWLS;
			Reschedule = true;
			queryable.ThisScheduler.Add (this);
		}
	}

}
