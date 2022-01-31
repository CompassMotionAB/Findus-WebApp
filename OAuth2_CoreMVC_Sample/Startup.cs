using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FortnoxApiExample.Helper;
using FortnoxApiExample.Models;
using FortnoxApiExample.Services.Fortnox;
using Microsoft.IdentityModel.Logging;
using FortnoxApiExample.Security.Fortnox;

namespace FortnoxApiExample
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
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            IdentityModelEventSource.ShowPII = true;

            services.Configure<FortnoxSettings>(Configuration.GetSection(FortnoxSettings.Name));
            services.AddSingleton<FortnoxSettings>();
            services.Configure<OAuth2Keys>(Configuration.GetSection(OAuth2Keys.Name));

            services.AddHttpContextAccessor();

            services.AddFortnoxAuthorization(Configuration);

            services.AddDbContext<TokensContext>(options => options.UseSqlite(Configuration.GetConnectionString("DBConnectionString")));

            services.AddTransient<IFortnoxServices, FortnoxServices>();

            services.AddSingleton(provider => Configuration);

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

            app.UseRouting();

            //app.UseAuthentication(); 
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
