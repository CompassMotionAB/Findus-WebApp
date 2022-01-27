using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FortnoxApiExample.Services.Fortnox
{
    public interface IFortnoxServiceContext
    {
        string GetAccessToken();
        string GetRefreshToken();

        void OnActionExecuted(ActionExecutedContext context);
        void OnActionExecuting(ActionExecutingContext context);
    }

    
    public interface IFortnoxServices
    {
        Task FortnoxApiCall(Action<FortnoxContext> apiCallFunction);
    }
}