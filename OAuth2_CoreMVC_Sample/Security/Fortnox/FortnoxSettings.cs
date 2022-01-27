using System.Collections.Generic;
using Fortnox.SDK.Auth;

namespace FortnoxApiExample.Security.Fortnox
{
    public interface IFortnoxSettings
    {
        List<Scope> Scopes { get; set; }
        string BaseUrl { get; set; }
        string AuthEndpoint { get; set; }
        string TokenEndpoint { get; set; }
    }

    public class FortnoxSettings : IFortnoxSettings
    {
        public const string Name = "Fortnox";
        public List<Scope> Scopes { get; set; }
        public string BaseUrl { get; set; }
        public string AuthEndpoint { get; set; }
        public string TokenEndpoint { get; set; }
    }
}
