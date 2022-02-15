using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FindusWebApp.Services.Fortnox
{
    public interface IFortnoxContext 
    {
        string GetAccessToken();
        string GetRefreshToken();

        void OnActionExecuted(ActionExecutedContext context);
        void OnActionExecuting(ActionExecutingContext context);
    }
    public interface IFortnoxServices
    {
        Task FortnoxApiCall(Action<FortnoxContext> apiCallFunction);
        Task RefreshTokens(Action<FortnoxContext> apiCallFunction);
    }
}