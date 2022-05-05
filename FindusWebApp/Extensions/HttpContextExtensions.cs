using Microsoft.AspNetCore.Http;

namespace FindusWebApp.Extensions
{
    public static class HttpContextExtensions
    {
        public static string GenerateFullDomain(this HttpContext httpContext, string uri = null)
        {
            string domain = httpContext.Request.Host.Value;
            string scheme = httpContext.Request.Scheme;
            string delimiter = System.Uri.SchemeDelimiter;
            string fullDomainToUse = scheme + delimiter + domain;
            if (!string.IsNullOrEmpty(uri))
                return fullDomainToUse + uri;
            return fullDomainToUse;
        }
    }
}
