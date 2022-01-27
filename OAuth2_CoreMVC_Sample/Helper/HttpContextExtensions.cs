
using Microsoft.AspNetCore.Http;

namespace FortnoxApiExample.Helper
{

    public static class HttpContextExtensions
    {
        public static string GenerateFullDomain(this HttpContext httpContext)
        {
            string domain = httpContext.Request.Host.Value;
            string scheme = httpContext.Request.Scheme;
            string delimiter = System.Uri.SchemeDelimiter;
            string fullDomainToUse = scheme + delimiter + domain;
            return fullDomainToUse;
        }
    }
}