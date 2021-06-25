using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Routing;
using AkkaFractal.Core;
using AkkaFractal.Core.Akka;
using AkkaFractal.Web.Akka;
using Lib.AspNetCore.ServerSentEvents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Akka.Fractal.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddMvc(option => option.EnableEndpointRouting = false);

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

            services.AddServerSentEvents(options =>
            {
                options.KeepaliveMode = ServerSentEventsKeepaliveMode.Always;
                options.KeepaliveInterval = 15;
            });
            services.AddSingleton(_ => ActorSystem.Create("fractal", ConfigurationLoader.Load()));
            services.AddSingleton<SseTileActorProvider>(provider =>
            {
                var serverSentEventsService = provider.GetService<IServerSentEventsService>();

                var actorSystem = provider.GetService<ActorSystem>();

                // TODO
                // create the instantiation of the "tileRenderActor" using the "TileRenderActor" actor.
                // After the first successful run, increase the level of parallelism using either a
                // Pool routing that distributes the work across its children (routee), or modifying the
                // HOCON config (akka.conf) file section "remoteactor"
                //
                //  Following code has to be update with the correct implementation
                var tileRenderActor = Nobody.Instance;

                // TODO
                // Same as previous TODO
                // create the instantiation of the "sseTileActor" using the "SseTileActor" actor
                // passing the correct argument in the constructor
                //
                //  Following code has to be update with the correct implementation
                var sseTileActor = Nobody.Instance;


                actorSystem.ActorOf(Props.Create(() => new SseTileActor(serverSentEventsService, tileRenderActor)),
                    "sse-tile");

                return () => sseTileActor;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            app.UseDeveloperExceptionPage();


            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });


            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => { endpoints.MapRazorPages(); });

            app.MapServerSentEvents("/fractal-tiles");
            lifetime.ApplicationStarted.Register(() =>
            {
                app.ApplicationServices.GetService<ActorSystem>(); // Start Akka.NET
            });
            lifetime.ApplicationStopping.Register(() =>
            {
                var svc = app.ApplicationServices.GetService<ActorSystem>();
                if (svc != null)
                {
                    svc.Terminate()
                        .GetAwaiter().GetResult();
                }
            });
        }
    }
}
