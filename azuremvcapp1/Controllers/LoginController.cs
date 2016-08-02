using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;

namespace azuremvcapp1.Controllers
{
    public class LoginController : Controller
    {
        // GET: Login
        public ActionResult Index(string redirectUri)
        {
            Session["dummy"] = "dummy"; // Create ASP.NET_SessionId cookie -- http://stackoverflow.com/questions/22535146/owin-openid-provider-getexternallogininfo-returns-null
            var whenDoneLoggingInUrl = Url.Action("ExternalLoginCallback", "Login", new { ReturnUrl = redirectUri });
            return new ChallengeResult("Twitter", whenDoneLoggingInUrl);
        }

        public ActionResult ExternalLoginCallback(string returnUrl)
        {
            var owinContext = HttpContext.GetOwinContext();
            var loginInfo = owinContext.Authentication.GetExternalLoginInfo();
            if (loginInfo == null)
            {
                // did not survive the login process
                return View("LoginFailure");
            }

            // log them in persistently here! 
            // we have to create a new claims identity with authentication type cookie. 
            // i could probably copy the claims over easier... 
            var claims = new List<Claim>();
            foreach (var externalClaim in loginInfo.ExternalIdentity.Claims)
            {
                claims.Add(externalClaim);
            }
            var id = new ClaimsIdentity(claims,
                                        DefaultAuthenticationTypes.ApplicationCookie);
            // https://stackoverflow.com/questions/23180896/how-to-remember-the-login-in-mvc5-when-an-external-provider-is-used/23228005#23228005
            owinContext.Authentication.SignIn(
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    AllowRefresh = true
                }, id);

            return new RedirectResult(returnUrl);
        }

        public ActionResult Logout()
        {
            var owinContext = HttpContext.GetOwinContext();
            owinContext.Authentication.SignOut();
            return new RedirectResult(Url.Action("Index", "Home"));
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
            { 
                LoginProvider = provider;
                RedirectUri = redirectUri;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            // public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                // this does the 302 or whatever the login provider wants you to do. 
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }

    }
}