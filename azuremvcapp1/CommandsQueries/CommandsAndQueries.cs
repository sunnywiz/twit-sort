using LinqToTwitter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Configuration;
using azuremvcapp1.Models;

namespace azuremvcapp1.CommandsQueries
{
    public class CommandsAndQueries
    {
        private HttpContextBase _context;

        public CommandsAndQueries(HttpContextBase context)
        {
            _context = context;
        }

        public async Task<TwitterContext> GetTwitterContextAsync()
        {

            var credStore = new SessionStateCredentialStore();  // i frequently loose session, so need to rejigger this
            if (!credStore.HasAllCredentials())
            {
                if (_context == null) return null;
                var owinContext = _context.GetOwinContext();
                if (owinContext == null) return null;
                var authman = owinContext.Authentication;
                if (authman == null) return null;
                if (authman.User == null) return null;

                foreach (var claim in authman.User.Claims)
                {
                    switch (claim.Type)
                    {
                        case "urn:twitter:userid": credStore.UserID = ulong.Parse(claim.Value); continue;
                        case "urn:twitter:screenname": credStore.ScreenName = claim.Value; continue;
                        case "TwitterAccessToken": credStore.OAuthToken = claim.Value; continue;
                        case "TwitterAccessTokenSecret": credStore.OAuthTokenSecret = claim.Value; ; continue;
                        default:
                            continue;
                    }
                }
                credStore.ConsumerKey = ConfigurationManager.AppSettings["TwitterConsumerKey"];
                credStore.ConsumerSecret = ConfigurationManager.AppSettings["TwitterConsumerSecret"];
                await credStore.StoreAsync();
                if (!credStore.HasAllCredentials()) throw new NotSupportedException("Could not get credential store to have all credentials!");
            }
            return new TwitterContext(new MvcAuthorizer() { CredentialStore = credStore });
        }

        public LoggedInUser GetLoggedInUser()
        {
            if (_context == null) return null;
            var owinContext = _context.GetOwinContext();
            if (owinContext == null) return null;
            var authman = owinContext.Authentication;
            if (authman == null) return null;
            if (authman.User == null) return null;

            var loggedInUser = new LoggedInUser();
            foreach (var claim in authman.User.Claims)
            {
                // Claims: 
                // http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier = 152333310 
                // http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name = sunjeevgulati 
                // urn:twitter:userid = 152333310 
                // urn:twitter:screenname = sunjeevgulati 
                // TwitterAccessToken =  ; 
                // TwitterAccessTokenSecret = ;
                switch (claim.Type)
                {
                    case "urn:twitter:userid": loggedInUser.TwitterId = ulong.Parse(claim.Value); continue;
                    case "urn:twitter:screenname": loggedInUser.TwitterScreenName = claim.Value; continue;
                    default:
                        continue;
                }
            }
            return loggedInUser;
        }

        public string GetUserConfiguration(ulong twitterId)
        {
            string configuration = string.Empty;
            var twitterId2 = Convert.ToInt64(twitterId);
            using (var context = new ApplicationDbContext())
            {
                var config = context.UserConfig.FirstOrDefault(uc => uc.TwitterUserId == twitterId2);
                if (config != null) configuration = config.Configuration;
            }

            return configuration;
        }

        public void SaveUserConfiguration(ulong twitterId, string configuration)
        {
            var twitterId2 = Convert.ToInt64(twitterId);
            using (var context = new ApplicationDbContext())
            {
                var config = context.UserConfig.FirstOrDefault(uc => uc.TwitterUserId == twitterId2);
                if (config == null)
                {
                    var newConfig = new UserConfig() { TwitterUserId = twitterId2, Configuration = configuration };
                    context.UserConfig.Add(newConfig);
                }
                else
                {
                    config.Configuration = configuration;
                }
                context.SaveChanges();
            }
        }

        public IQueryable<List> GetTwitterListsQueryable(TwitterContext twitterCtx, string twitterScreenName)
        {
            return (from list in twitterCtx.List
                    where list.Type == ListType.List &&
                          list.ScreenName == twitterScreenName
                    select list);
        }

        public IQueryable<Status> GetHomeTweetsQueryable(TwitterContext twitterCtx)
        {
            return (from tweet in twitterCtx.Status
                where tweet.Type == StatusType.Home
                      && tweet.Count == 200
                      && tweet.IncludeContributorDetails == true
                      && tweet.IncludeEntities == true
                      && tweet.IncludeRetweets == true
                    select tweet);
        }

        /// <summary>
        /// Returns a List object that has members populated
        /// </summary>
        /// <param name="twitterCtx"></param>
        /// <param name="twitterScreenName"></param>
        /// <param name="listToFind"></param>
        /// <returns></returns>
        public List GetTwitterListWithMembers(TwitterContext twitterCtx, string twitterScreenName, string listToFind)
        {
            return (from member in twitterCtx.List
                    where member.Type == ListType.Members &&
                    member.OwnerScreenName == twitterScreenName &&
                    member.Slug == listToFind &&
                    member.SkipStatus == true
                    select member)
                .FirstOrDefault();
        }

        public class LoggedInUser
        {
            public string TwitterScreenName;
            public ulong TwitterId;
        }
    }
}