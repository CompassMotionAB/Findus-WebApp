using System.Threading.Tasks;
using Fortnox.SDK;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using FindusWebApp.Extensions;
using FindusWebApp.Models;
using FindusWebApp.Security.Fortnox;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Linq;
using FindusWebApp.Helpers;

namespace FindusWebApp.Controllers
{
    public class ConnectController : Controller
    {
        private readonly TokensContext _tokens;
        private readonly OAuth2Keys _auth2Keys;
        private readonly FortnoxSettings _fortnoxSettings;
        public readonly FortnoxAuthClient FortnoxAuthClient;
        public string FortnoxState { get; private set; }

        public ConnectController(TokensContext tokens, IOptions<OAuth2Keys> auth2Keys, IOptions<FortnoxSettings> fortnoxSettings)
        {
            _tokens = tokens;
            _auth2Keys = auth2Keys.Value;
            _fortnoxSettings = fortnoxSettings.Value;
            FortnoxAuthClient = new FortnoxAuthClient();
        }

        public ActionResult Home()
        {
            return View("Connect");
        }

        // GET: /<controller>/
        public async Task<ActionResult> Index()
        {
            string state = Request.Query["state"];
            var code = Request.Query["code"].ToString();

            if (!string.IsNullOrEmpty(state) && !string.IsNullOrEmpty(code))
            {
                await GetAuthTokensAsync(code);
                // Get own redirectUrl from state;

                string[] stateParams = state.GetQueryParams();
                if (stateParams.Length > 1)
                {
                    var redirectUrl = stateParams[1];
                    return Redirect(HttpContext.GenerateFullDomain() + redirectUrl);
                }
                return RedirectToAction("Index", "Fortnox");
            }

            return RedirectToAction("Home", "Connect");
        }

        public IActionResult Login(string redirectUrl = null)
        {
            if (!string.IsNullOrEmpty(_auth2Keys.ClientId) && !string.IsNullOrEmpty(_auth2Keys.ClientSecret))
            {
                var scopes = _fortnoxSettings.Scopes;
                // TODO: Login/Add User with correct Claims to requsted Fortnox scopes
                // TODO: Skip login if user is already authenticated with correct scopeHash
                // var scopeHash = scopes.GetUniqueHashForEnumerable() 

                var authWorkflow = FortnoxAuthClient.StandardAuthWorkflow;
                var callbackPath = HttpContext.GenerateFullDomain() + _auth2Keys.CallbackPath;

                FortnoxState = authWorkflow.GenerateState();

                if(!string.IsNullOrEmpty(redirectUrl)) {
                    redirectUrl = callbackPath;
                    FortnoxState.AppendQueryString(redirectUrl);
                }

                var authorizeUrl = authWorkflow.BuildAuthUri(_auth2Keys.ClientId, scopes, FortnoxState, callbackPath);

                return Redirect(authorizeUrl.AbsoluteUri);
            }

            ViewData["Configuration"] = "NullValue";
            return View("Connect");
        }
        [HttpGet]
        public async Task<IActionResult> LogoutAsync(string redirectUrl = null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _tokens.Token.RemoveRange(_tokens.Token);
            await _tokens.SaveChangesAsync();
            if (!string.IsNullOrEmpty(redirectUrl))
            {
                return Redirect(redirectUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        private async Task GetAuthTokensAsync(string code)
        {
            var authWorkflow = FortnoxAuthClient.StandardAuthWorkflow;

            var tokenResponse = await authWorkflow.GetTokenAsync(code, _auth2Keys.ClientId, _auth2Keys.ClientSecret);
            var token = _tokens.Token.FirstOrDefault();

            if (token == null)
            {
                _tokens.Add(new Token
                {
                    RealmId = "fortnox",
                    ScopeHash = _fortnoxSettings.Scopes.GetUniqueHashForEnumerable(),
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken
                });
                await _tokens.SaveChangesAsync();
            }
        }
    }
}