using System;
using System.Threading.Tasks;
using Fortnox.SDK;
using Fortnox.SDK.Authorization;
using Fortnox.SDK.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FindusWebApp.Models;
using FindusWebApp.Helpers;
using FindusWebApp.Extensions;
using System.Collections.Generic;
using Fortnox.SDK.Entities;

namespace FindusWebApp.Services.Fortnox
{
    public class FortnoxContext : IActionFilter, IFortnoxContext
    {
        public FortnoxClient Client => GetFortnoxClient();
        public FortnoxClient GetFortnoxClient() => new(new StandardAuth(GetAccessToken()));
        private readonly Token _token;
        public string CustomerNr;

        public Dictionary<string, InvoiceSubset[]> CustomerInvoices;

        public FortnoxContext(Token token)
        {
            _token = token;
        }
        public string GetAccessToken()
        {
            return _token.AccessToken;
        }
        public string GetRefreshToken()
        {
            return _token.RefreshToken;
        }

        public void OnActionExecuting(ActionExecutingContext context) {
            throw new NotImplementedException();
        }
        public void OnActionExecuted(ActionExecutedContext context)
        {
            throw new NotImplementedException();
        }
    }
    public class FortnoxServices : IFortnoxServices
    {
        private readonly TokensContext _tokens;
        private readonly OAuth2Keys _auth2keys;

        public FortnoxServices(TokensContext tokens, IOptions<OAuth2Keys> auth2Keys)
        {
            _tokens = tokens;
            _auth2keys = auth2Keys.Value;
        }

        /// <summary>
        ///     Service wrapper for Fortnox api call
        /// </summary>
        /// <param name="apiCallFunction"></param>
        public async Task FortnoxApiCall(Action<FortnoxContext> apiCallFunction)
        {
            var token = await _tokens.Token.FirstOrDefaultAsync();

            if (token == null || token.AccessToken == null)
            {
                throw new FortnoxApiException("Fortnox Api not Connected");
            }
            try
            {
                var context = new FortnoxContext(token);
                apiCallFunction(context);
                _tokens.SaveChanges();
            }
            catch (Exception ex)
            {
                // TODO: Informative message if user is not authorized to scope.
                // TODO: use Authorization Policy middleware
                if ((ex.InnerException as FortnoxApiException)?.ResponseContent == "{\"message\":\"unauthorized\"}")
                {
                    await RefreshTokens(apiCallFunction);
                }
                else
                {
                    // TODO: For now, assumes faulty token and removes it
                    _tokens.Token.RemoveRange(_tokens.Token);
                    await _tokens.SaveChangesAsync();
                    throw new Exception(message: ex.Message);
                }
            }
        }
        public async Task RefreshTokens(Action<FortnoxContext> apiCallFunction)
        {
            var token = await _tokens.Token.FirstOrDefaultAsync();
            var fortnoxAuthClient = new FortnoxAuthClient();
            var authWorkflow = fortnoxAuthClient.StandardAuthWorkflow;
            var newToken = await authWorkflow.RefreshTokenAsync(token.RefreshToken, _auth2keys.ClientId, _auth2keys.ClientSecret);
            if (newToken.AccessToken != null && newToken.RefreshToken != null)
            {
                await UpdateToken(newToken);
                await FortnoxApiCall(apiCallFunction);
            }
        }

        private Task<Token> UpdateToken(global::Fortnox.SDK.Auth.TokenInfo newToken)
        {
            return UpdateToken(newToken.AccessToken, newToken.RefreshToken);
        }

        public async Task<Token> UpdateToken(string newAccessToken, string newRefreshToken)
        {
            var token = await _tokens.Token.FirstOrDefaultAsync();
            if (token != null)
            {
                token.UpdateToken(newAccessToken, newRefreshToken);
                _tokens.SaveChanges();
            }

            return token;
        }
    }
}