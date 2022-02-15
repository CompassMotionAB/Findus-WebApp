namespace FindusWebApp.Models
{
    public class Token
    {
        public string RealmId { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public int ScopeHash { get; set; }
    }
}