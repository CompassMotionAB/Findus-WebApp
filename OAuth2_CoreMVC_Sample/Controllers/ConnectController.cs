using System.Collections.Generic;
using System.Threading.Tasks;
using Fortnox.SDK;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using FortnoxApiExample.Helper;
using FortnoxApiExample.Models;
using FortnoxApiExample.Security.Fortnox;
using Fortnox.SDK.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Linq;

namespace FortnoxApiExample.Controllers
{
    public class ConnectController : Controller
    {
        private readonly TokensContext _tokens;
        private readonly OAuth2Keys _auth2Keys;
        public FortnoxAuthClient fortnoxAuthClient { get; private set; }
        private FortnoxSettings _fortnoxSettings { get; set; }
        public string _fortnoxState { get; private set; }

        public ConnectController(TokensContext tokens, IOptions<OAuth2Keys> auth2Keys, IOptions<FortnoxSettings> fortnoxSettings)
        {
            _tokens = tokens;
            _auth2Keys = auth2Keys.Value;
            _fortnoxSettings = fortnoxSettings.Value;
            fortnoxAuthClient = new FortnoxAuthClient();
        }

        public ActionResult Home()
        {
            return View("Connect");
        }

        // GET: /<controller>/
        public async Task<ActionResult> Index()
        {
            var state = Request.Query["state"];
            var code = Request.Query["code"].ToString();

            if (state.Count > 0 && !string.IsNullOrEmpty(code))
            {
                await GetAuthTokensAsync(code);
                return RedirectToAction("Index", "Fortnox");
            }

            return RedirectToAction("Home", "Connect");
        }

        [HttpGet]
        public IActionResult Login(string redirectUrl = null)
        {
            if (!string.IsNullOrEmpty(_auth2Keys.ClientId) && !string.IsNullOrEmpty(_auth2Keys.ClientSecret))
            {
                var scopes = _fortnoxSettings.Scopes;
                // TODO: Skip login if user is already authenticated against Fortnox WITH current scopes
                // <-----
                /*
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                var token = _tokens.Token;

                if (token != null)
                {
                    _tokens.Remove(token);
                    await _tokens.SaveChangesAsync();
                }
                */
                // ----->

                var authWorkflow = fortnoxAuthClient.StandardAuthWorkflow;
                _fortnoxState = authWorkflow.GenerateState();

                // Store custom returnUrl in state.
                _fortnoxState = GenerateState(_fortnoxState, redirectUrl);

                var authorizeUrl = authWorkflow.BuildAuthUri(_auth2Keys.ClientId, scopes, _fortnoxState, HttpContext.GenerateFullDomain() + _auth2Keys.CallbackPath);

                return Redirect(authorizeUrl.AbsoluteUri);
            }

            ViewData["Configuration"] = "NullValue";
            return View("Connect");
        }
        private string GenerateState(string state = null, string redirectUrl = null)
        {
            string stateOut = string.IsNullOrEmpty(state) ? System.Guid.NewGuid().ToString() : state;
            if (!string.IsNullOrEmpty(redirectUrl))
            {
                stateOut += ";" + System.Web.HttpUtility.ParseQueryString(redirectUrl);
            }
            return stateOut;
        }

        private async Task GetAuthTokensAsync(string code)
        {
            var authWorkflow = fortnoxAuthClient.StandardAuthWorkflow;

            var tokenResponse = await authWorkflow.GetTokenAsync(code, _auth2Keys.ClientId, _auth2Keys.ClientSecret);
            var token = _tokens.Token.FirstOrDefault();

            if (token == null)
            {
                _tokens.Add(new Token
                {
                    RealmId = "fortnox",
                    ScopeHash = _fortnoxSettings.Scopes.GetHashCode(),
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken
                });
                await _tokens.SaveChangesAsync();
            }
        }
    }
}