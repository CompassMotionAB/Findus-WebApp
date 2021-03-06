using FindusWebApp.Models;

namespace FindusWebApp.Extensions {
    public static class TokenExtensions {
        public static Token UpdateToken(this Token token ,string newAccessToken, string newRefreshToken)
        {
            if (token != null) {
                token.AccessToken = newAccessToken;
                token.RefreshToken = newRefreshToken;
            }
            return token;
        }
    }
}