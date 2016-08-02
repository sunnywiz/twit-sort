using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(azuremvcapp1.Startup))]
namespace azuremvcapp1
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
