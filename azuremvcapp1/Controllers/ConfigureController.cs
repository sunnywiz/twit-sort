using azuremvcapp1.CommandsQueries;
using azuremvcapp1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace azuremvcapp1.Controllers
{
    public class ConfigureController : Controller
    {
        private CommandsAndQueries _cq; 

        public ConfigureController(CommandsAndQueries cq)
        {
            _cq = cq; 
        }
        
        // GET: Configure
        [Authorize]
        public ActionResult Index()
        {
            var vm = new ConfigureViewModel(); 
            try {
                var user = _cq.GetLoggedInUser(); 
                var configuration = _cq.GetUserConfiguration(user.TwitterId);
                vm.Configuration = configuration;
            } catch (Exception ex)
            {
                vm.Exception = ex; 
            }
            return View("Configure",vm);
        }

        [Authorize]
        public async System.Threading.Tasks.Task<ActionResult> CreateSampleConfiguration()
        {
            var vm = new ConfigureViewModel();
            try
            {
                var newConfig = new StringBuilder();
                var user = _cq.GetLoggedInUser();

                using (var twitterCtx = await _cq.GetTwitterContextAsync())
                {
                    // later: make this async optimized

                    var userLists = _cq.GetTwitterListsQueryable(twitterCtx, user.TwitterScreenName).ToList();
                    vm.Log.AppendFormat("{0} Lists found", userLists.Count);
                    foreach (var list in userLists)
                    {
                        newConfig.AppendFormat("LIST {0} {0}", list.Name).AppendLine();
                    }

                    var prolificTweeters = (from t in _cq.GetHomeTweetsQueryable(twitterCtx)
                                            group t by t.User.ScreenNameResponse into g
                                            orderby g.Count() descending
                                            select g).Take(5).ToList();
                    for (int i=0; i<prolificTweeters.Count; i++)
                    {
                        newConfig.AppendFormat("USER {0} Chatter#{1}",prolificTweeters[i].Key,i+1).AppendLine();
                    }
                    vm.Configuration = newConfig.ToString(); 
                }

            }
            catch (Exception ex)
            {
                vm.Exception = ex;
            }
            return View("Configure", vm);
        }

        [Authorize]
        [HttpPost]
        public ActionResult Save(string configuration)
        {
            var user = _cq.GetLoggedInUser();

            _cq.SaveUserConfiguration(user.TwitterId, configuration);

            return RedirectToAction("Index", "Read");
        }

        [Authorize]
        public ActionResult ClearReadUntils()
        {
            var user = _cq.GetLoggedInUser();
            var dbTwitterId = Convert.ToInt64(user.TwitterId);
            using (var context = new ApplicationDbContext())
            {
                var readUntils = context.ReadUntil.Where(ru => ru.TwitterUserId == dbTwitterId);
                context.ReadUntil.RemoveRange(readUntils);
                context.SaveChanges(); 
            }
            return new RedirectResult(Url.Action("Index", "Read"));
        }

        public class ConfigureViewModel
        {
            public ConfigureViewModel() { Log = new StringBuilder(); }
            public string Configuration { get; set; }
            public StringBuilder Log { get; set; }
            public Exception Exception { get; set; }
        }
    }
}