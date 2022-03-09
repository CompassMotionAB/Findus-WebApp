using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FindusWebApp.Helpers;
using FindusWebApp.Models;
using FindusWebApp.Services.Fortnox;
using Microsoft.IdentityModel.Logging;
using FindusWebApp.Security.Fortnox;
using System;

namespace FindusWebApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = _ => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            IdentityModelEventSource.ShowPII = true;

            // <!--- Session
            services.AddDistributedMemoryCache();

            services.AddSession(options =>
            {
                // Set a short timeout for easy testing.
                if (Environment.IsDevelopment()) options.IdleTimeout = TimeSpan.FromSeconds(10);
                else options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            // ---!>

            services.Configure<FortnoxSettings>(Configuration.GetSection(FortnoxSettings.Name));
            services.AddSingleton<FortnoxSettings>();
            services.Configure<OAuth2Keys>(Configuration.GetSection(OAuth2Keys.Name));

            services.Configure<WooKeys>(Configuration.GetSection(WooKeys.Name));
            services.AddSingleton<WooKeys>();

            services.AddHttpClient();
            services.AddHttpContextAccessor();

            services.AddFortnoxAuthorization(Configuration);

            services.AddDbContext<TokensContext>(options => options.UseSqlite("name=ConnectionStrings:DBConnectionString"));

            services.AddTransient<IFortnoxServices, FortnoxServices>();

            services.AddSingleton(_ => Configuration);

            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            //app.UseCookiePolicy();

            //app.UseSession();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
