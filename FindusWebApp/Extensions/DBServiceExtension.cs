using FindusWebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FindusWebApp.Helpers {
    public static class DBServiceExtension {
        public static IServiceCollection AddDBService(this IServiceCollection services) {
            return services.AddDbContext<TokensContext>(options => options.UseSqlite("name=ConnectionStrings:DBConnectionString"));
        }
    }
}