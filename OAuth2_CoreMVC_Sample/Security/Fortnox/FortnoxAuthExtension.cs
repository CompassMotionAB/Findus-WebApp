
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FortnoxApiExample.Helper;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FortnoxApiExample.Security.Fortnox
{
    public static class FortnoxAuthExtensions
    {
        private static FortnoxSettings _fortnoxSettings;
        public static IServiceCollection AddFortnoxAuthorization(this IServiceCollection serviceCollection, IConfiguration configuration)
        {

            _fortnoxSettings = configuration.GetSection(FortnoxSettings.Name).Get<FortnoxSettings>();
            OAuth2Keys auth2keys = configuration.GetSection(OAuth2Keys.Name).Get<OAuth2Keys>();

            var baseUrl = _fortnoxSettings.BaseUrl;
            var authEndpoint = new Uri(_fortnoxSettings.AuthEndpoint);
            var tokenEndpoint = new Uri(_fortnoxSettings.TokenEndpoint);

            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(authEndpoint.ToString()) || string.IsNullOrEmpty(tokenEndpoint.ToString()))
            {
                throw new System.Exception("Fortnox Endpoints's and BaseUrl must be configured for Fortnox authentication. See ./appsettings.sample.json");
            }

            var fortnoxScopes = _fortnoxSettings.Scopes;
            if (fortnoxScopes == null || fortnoxScopes.Count == 0)
            {
                throw new System.Exception("Fortnox Scopes must be provided in appsettings.json. See ./appsettings.sample.json");
            }


            var clientId = auth2keys.ClientId;
            var clientSecret = auth2keys.ClientSecret;
            var callbackPath = auth2keys.CallbackPath;
            var relativePath = PathString.FromUriComponent(new Uri(callbackPath));

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new System.Exception("User secrets must be configured for Fortnox authentication. See ./appsettings.sample.json");
            }
            if (string.IsNullOrEmpty(callbackPath))
            {
                throw new System.Exception("CallbackPath must be configured for Fortnox authentication. See ./appsettings.sample.json");
            }

            serviceCollection
            .AddAuthorization(options =>
            {
                options.AddFortnoxAuthPolicy();
            })
            .AddAuthentication(options =>
            {
                //options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                //options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = Constants.FortnoxScheme;
                options.DefaultChallengeScheme = Constants.FortnoxScheme;
            })
            .AddCookie(o =>
            {
                //options.Cookie.SecurePolicy = CookieSecurePolicy.Never;
                //options.Cookie.SameSite = SameSiteMode.Strict;
                //options.Cookie.HttpOnly = true;
            }).AddOAuth(Constants.FortnoxScheme, options =>
            {
                options.AuthorizationEndpoint = baseUrl + PathString.FromUriComponent(authEndpoint);
                options.TokenEndpoint = baseUrl + PathString.FromUriComponent(tokenEndpoint);
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.CallbackPath = relativePath;
                options.SaveTokens = true;

                options.SignInScheme = "Cookies";
                options.Events = new OAuthEvents
                {
                    OnRedirectToAuthorizationEndpoint = context =>
                    {
                        string scopeString = fortnoxScopes.JoinToLower(delimiter: "%20");

                        var parameters = new Dictionary<string, string> { { "scope", scopeString } };
                        var scopeQuery = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
                        context.Response.Redirect(context.RedirectUri + "&" + scopeQuery);

                        return Task.FromResult(0);
                    },
                    /*
                    OnTicketReceived = context => {
                        

                        context.HandleResponse();

                        if (string.IsNullOrEmpty(context.ReturnUri))
                        {
                            context.ReturnUri = "/";
                        }

                        context.Response.Redirect(context.ReturnUri);
                        return Task.FromResult(0);
                        
                    },
                    */
                    /*
                    OnCreatingTicket = async context =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                        response.EnsureSuccessStatusCode();

                        var user = JObject.Parse(await response.Content.ReadAsStringAsync());

                        // Add claims

                        var userId = user.Value<string>("username");
                        if (!string.IsNullOrEmpty(userId))
                        {
                            context.Identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId, ClaimValueTypes.String, context.Options.ClaimsIssuer));
                            options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, userId);
                        }

                        var name = user.Value<string>("name");
                        if (!string.IsNullOrEmpty(name))
                        {
                            context.Identity.AddClaim(new Claim(ClaimTypes.GivenName, name, ClaimValueTypes.String, context.Options.ClaimsIssuer));
                            options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, name);
                        }

                        var email = user.Value<string>("email");
                        if (!string.IsNullOrEmpty(email))
                        {
                            context.Identity.AddClaim(new Claim(ClaimTypes.Email, email, ClaimValueTypes.Email, context.Options.ClaimsIssuer));
                            options.ClaimActions.MapJsonKey(ClaimTypes.Email, email);
                        }

                        var avatar = user.Value<string>("avatar_url");
                        if (!string.IsNullOrEmpty(avatar))
                        {
                            context.Identity.AddClaim(new Claim(ClaimTypes.Uri, avatar, ClaimValueTypes.String, context.Options.ClaimsIssuer));
                            options.ClaimActions.MapJsonKey(ClaimTypes.Uri, avatar);
                        }
                    }*/
                };
            });

            return serviceCollection;
        }

        public static AuthorizationOptions AddFortnoxAuthPolicy(this AuthorizationOptions options)
        {

            var scopes = _fortnoxSettings.Scopes;


            var policy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(Constants.FortnoxScheme)
                .RequireAuthenticatedUser();
            scopes.ForEach(scope =>
                options.AddPolicy(scope.ToString(),
                    policy => policy.Requirements.Add(
                        new ScopeRequirement(_fortnoxSettings.BaseUrl, scope.ToString())
                    )
                )
            );
            /*
             options.AddPolicy(Constants.FortnoxAuthPolicy, policyBuilder => {
                    policyBuilder.RequireClaim(ClaimTypes.Name);
                });*/

            options.AddPolicy(Constants.FortnoxAuthPolicy, policy.Build());
            return options;
        }
    }
}