using azuremvcapp1.CommandsQueries;
using azuremvcapp1.Models;
using LinqToTwitter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace azuremvcapp1.Controllers
{
    public class ReadController : Controller
    {
        private static DateTime? _firstLoad;
        private static int _loadCount;
        private static long _lastLoadTimeInMs; 

        private CommandsAndQueries _cq; 
        public ReadController(CommandsAndQueries cq)
        {
            _cq = cq; 
        }

        [Authorize]
        public ActionResult Index()
        {
            if (!_firstLoad.HasValue)
            {
                _firstLoad = DateTime.Now;
                _loadCount = 0;
                _lastLoadTimeInMs = 0; 
            }
            return View("Read", new CrunchyMuchRoll()
            {
                FirstLoad = _firstLoad.Value,
                LastLoadTimeInMs = _lastLoadTimeInMs,
                LoadCount = _loadCount
            });
        }

        public class CrunchyMuchRoll
        {
            public DateTime? FirstLoad { get; set; }
            public int LoadCount { get; set; }
            public long LastLoadTimeInMs { get; set; }
        }

        // GET: Read
        [Authorize]
        public async System.Threading.Tasks.Task<ActionResult> Index2()
        {
            if (!_firstLoad.HasValue)
            {
                _firstLoad = DateTime.Now;
                _loadCount = 0;
            }

            _loadCount++;
            Stopwatch watch = new Stopwatch(); watch.Start();

            // TODO: this is getting really large.  Refactor this and make it simpler
            // and parralelize it more if possible. 

            var vm = new ReadViewModel();

            try
            {
                // TODO: put a big try / catch around this and report errors directly to the view. 
                ViewBag.Message = "Read";

                var userInfo = _cq.GetLoggedInUser();
                var dbTwitterId = Convert.ToInt64(userInfo.TwitterId);

                string configuration = _cq.GetUserConfiguration(userInfo.TwitterId);
                vm.Log.AppendFormat("Retrieved user configuration at {0}ms", (int)watch.ElapsedMilliseconds).AppendLine();

                vm.Log.AppendFormat("logged in user twitter id is {0}", userInfo.TwitterId).AppendLine();

                // TODO: move this into _cq as well.  And get it async, and interrogate it later.
                Dictionary<string, DateTime> readUntils = new Dictionary<string, DateTime>();
                using (var context = new ApplicationDbContext())
                {
                    // not .ToDictionary() because we're not garunteeing unique keys, yet. 
                    // because i couldn't get that to work in EF code first with a composite key.
                    var x = context.ReadUntil.Where(ru => ru.TwitterUserId == dbTwitterId).OrderBy(ru => ru.When).ToList();
                    foreach (var y in x)
                    {
                        readUntils[y.FilterName] = y.When;
                        vm.Log.AppendFormat("Filter {0} read until {1}",y.FilterName,y.When.ToString("o")).AppendLine();
                    }
                }
                vm.Log.AppendFormat("Retrieved read-until info at {0}ms", (int)watch.ElapsedMilliseconds).AppendLine();

                Dictionary<Regex, string> regexToFilterDictionary = new Dictionary<Regex, string>(); 

                using (var twitterCtx = await _cq.GetTwitterContextAsync())
                {
                    Dictionary<string, string> userToListMapping = new Dictionary<string, string>();

                    // Get this thing started early .. 
                    var tweetGetTask = _cq.GetHomeTweetsQueryable(twitterCtx)
                        .ToListAsync();

                    foreach (var line in configuration.Split('\r', '\n'))
                    {
                        var ltrim = line.Trim();

                        // check for list <listname> <target>
                        var match = Regex.Match(ltrim, @"^list\s+(\S+)\s+(.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var listToFindSlugName = match.Groups[1].Value;
                            var filterName = match.Groups[2].Value;
                            SlurpUsernamesFromSlugIntoMapping(vm, userInfo.TwitterScreenName, twitterCtx, userToListMapping, listToFindSlugName, filterName);
                            continue;
                        }
                        match = Regex.Match(ltrim, @"^user\s+(\S+)\s+(.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                        // TODO: map single user
                        if (match.Success)
                        {
                            var username = match.Groups[1].Value;
                            var filterName = match.Groups[2].Value;
                            userToListMapping[username.ToLowerInvariant()] = filterName;
                            vm.Log.AppendFormat("User [{0}] to map to Filter [{1}]", username, filterName).AppendLine();
                            // add the filter names in the order we find them in the configuration! 
                            if (vm.Filters.FirstOrDefault(f => f.Name == filterName) == null)
                            {
                                vm.Filters.Add(new ReadViewModel.FilterViewModel() { Name = filterName });
                            }
                            continue; 
                        }
                        match = Regex.Match(ltrim, @"^regex\s+(\S+)\s+(.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var regex = match.Groups[1].Value;
                            var filterName = match.Groups[2].Value;
                            try
                            {
                                var r = new Regex(regex,RegexOptions.IgnoreCase);
                                regexToFilterDictionary[r] = filterName;
                                vm.Log.AppendFormat("RegularExpresion [{0}] maps to filter {1} (after username and list filters are applied)", r.ToString(), filterName)
                                    .AppendLine();
                                // add the filter names in the order we find them in the configuration! 
                                if (vm.Filters.FirstOrDefault(f => f.Name == filterName) == null)
                                {
                                    vm.Filters.Add(new ReadViewModel.FilterViewModel() { Name = filterName });
                                }
                            }
                            catch (Exception ex)
                            {
                                vm.Log.AppendFormat("Regular expression {0} did not compile, ignoring", regex).AppendLine();
                            }
                        }
                    }
                    vm.Log.AppendFormat("Done with parsing configuration and membership lookups at {0}ms", (int)watch.ElapsedMilliseconds).AppendLine();

                    var tweets = tweetGetTask.Result;
                    vm.Log.AppendFormat("Done with tweets at {0}ms", (int)watch.ElapsedMilliseconds).AppendLine();

                    int ignoredTweets = 0; 
                    foreach (var tweet in tweets)
                    {
                        string targetFilter = GetTargetListByUserMapping(userToListMapping, tweet);

                        // check against any of the regular expressions, see if they override the targetList
                        foreach (var x in regexToFilterDictionary)
                        {
                            if (x.Key.IsMatch(tweet.Text)) { targetFilter = x.Value; }
                        }

                        DateTime w;
                        if (readUntils.TryGetValue(targetFilter, out w) && tweet.CreatedAt < w)
                        {
                            ignoredTweets++;
                            continue;
                        } 

                        vm.StoreTweetToFilter(tweet, targetFilter);
                        foreach (var tag in tweet.Entities.HashTagEntities)
                        {
                            if (!vm.HashTagCounts.ContainsKey(tag.Tag)) vm.HashTagCounts[tag.Tag] = 0;
                            vm.HashTagCounts[tag.Tag]++;
                        }

                        foreach (var mention in tweet.Entities.UserMentionEntities)
                        {
                            if (!vm.UserMentionCounts.ContainsKey(mention.ScreenName)) vm.UserMentionCounts[mention.ScreenName] = 0;
                            vm.UserMentionCounts[mention.ScreenName]++;
                        }

                    }
                    vm.Log.AppendFormat("Skipped {0} tweets due to already-read-until", ignoredTweets).AppendLine(); 

                }
            }
            catch (Exception ex)
            {
                vm.Exception = ex;
            }
            _lastLoadTimeInMs = watch.ElapsedMilliseconds;            
            return View("Read2", vm);
        }


        [Authorize]
        public JsonResult MarkAsRead(string when, string filter)
        {
            var currentUser = _cq.GetLoggedInUser();
            var twitterId = Convert.ToInt64(currentUser.TwitterId);
            var whenAsDateTIme = DateTime.Parse(when).ToUniversalTime().AddSeconds(5); 
            // TODO:  move this to _cq
            using (var context = new ApplicationDbContext())
            {
                var dbReadUntil = context.ReadUntil.FirstOrDefault(
                    ru => ru.TwitterUserId == twitterId
                    && ru.FilterName == filter)
                    ;
                if (dbReadUntil == null)
                {
                    dbReadUntil = new ReadUntil()
                    {
                        // Id = Guid.NewGuid(),
                        TwitterUserId = twitterId,
                        FilterName = filter,
                        When = whenAsDateTIme
                    };
                    context.ReadUntil.Add(dbReadUntil);
                } else
                {
                    dbReadUntil.When = whenAsDateTIme; 
                }
                context.SaveChanges();
                return Json(true);
            }
        }

        private void SlurpUsernamesFromSlugIntoMapping(
            ReadViewModel vm, string twitterScreenName, TwitterContext twitterCtx, 
            Dictionary<string, string> userToListMapping, string listToFindSlugName, 
            string filterName)
        {
            try {
                List listAndMembers = _cq.GetTwitterListWithMembers(twitterCtx, twitterScreenName, listToFindSlugName);
                // add the filter names in the order we find them in the configuration! 
                // TODO: move the call to get list and members up to make this routine take pess parameters
                if (vm.Filters.FirstOrDefault(f => f.Name == filterName) == null)
                {
                    vm.Filters.Add(new ReadViewModel.FilterViewModel() { Name = filterName });
                }
                foreach (var member in listAndMembers.Users)
                {
                    userToListMapping[member.ScreenNameResponse.ToLowerInvariant()] = filterName;
                }
                vm.Log.AppendFormat("Added {0} users to map to Filter [{1}]", listAndMembers.Users.Count, filterName).AppendLine();
            }
            catch (Exception ex)
            {
                vm.Log.AppendLine("exception " + ex.Message + " trying to get users for slug " + listToFindSlugName+", skipping");
            }

        }

        private static string GetTargetListByUserMapping(Dictionary<string, string> userToListMapping, Status vmTweet)
        {
            string targetList = "";
            var lowercaseName = vmTweet.User.ScreenNameResponse.ToLowerInvariant();
            if (!userToListMapping.TryGetValue(lowercaseName, out targetList))
            {
                targetList = "Everything Else";
            }

            return targetList;
        }

        // TODO: move this to a shared query
        public static Guid GetUserIdentityGuid(IIdentity userIdentity)
        {
            var guidClaim = ((ClaimsIdentity)(userIdentity)).Claims.FirstOrDefault(cl => cl.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (guidClaim == null) throw new NotSupportedException("Don't know who you are?");
            var userGuid = Guid.Parse(guidClaim.Value);
            return userGuid;
        }

        public class ReadViewModel
        {
            public ReadViewModel()
            {
                Filters = new List<FilterViewModel>();
                Log = new StringBuilder();
                HashTagCounts = new Dictionary<string, int>();
                UserMentionCounts = new Dictionary<string, int>(); 
            }

            public Dictionary<string,int> HashTagCounts { get; set; }
            public Exception Exception { get; set; }
            public StringBuilder Log { get; set; }
            public Dictionary<string,int> UserMentionCounts { get; set; }

            public void StoreTweetToFilter(Status tweet, string filter)
            {
                var filter2 = Filters.FirstOrDefault(f => f.Name == filter);
                if (filter2 == null)
                {
                    filter2 = new FilterViewModel() { Name = filter };
                    Filters.Add(filter2);
                }
                filter2.Tweets.Add(tweet);
            }

            public List<FilterViewModel> Filters;
            public class FilterViewModel
            {
                public FilterViewModel() { Tweets = new List<Status>(); }
                public string Name { get; set; }
                public List<Status> Tweets { get; set; }
            }

        }
    }
}