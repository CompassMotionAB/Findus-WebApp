using System;
using System.Threading.Tasks;
using Fortnox.SDK;
using Fortnox.SDK.Authorization;
using Fortnox.SDK.Exceptions;
using Fortnox.SDK.Search;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FortnoxApiExample.Models;
using FortnoxApiExample.Security.Fortnox;
using Fortnox.SDK.Interfaces;
using FortnoxApiExample.Helper;

namespace FortnoxApiExample.Services.Fortnox
{
    public class FortnoxContext : IActionFilter, IFortnoxServiceContext
    {
        private readonly Token _token;
        public ICompanyInformationConnector CompanyInformationConnector { get => Client.CompanyInformationConnector; }
        public ICustomerConnector CustomerConnector { get => Client.CustomerConnector; }
        public IInvoiceConnector InvoiceConnector { get => Client.InvoiceConnector; }
        public FortnoxClient Client { get => GetFortnoxClient(); }
        public FortnoxClient GetFortnoxClient() => new FortnoxClient(new StandardAuth(GetAccessToken()));
        public FortnoxContext()
        {
        }

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

        public void OnActionExecuted(ActionExecutedContext context)
        {
            throw new NotImplementedException();
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            throw new NotImplementedException();
        }


    }
    public class FortnoxServices : IFortnoxServices
    {
        private readonly TokensContext _tokens;
        private readonly FortnoxSettings _fortnoxSettings;
        private readonly OAuth2Keys _auth2keys;

        public FortnoxServices(TokensContext tokens, IOptions<FortnoxSettings> fortnoxSettings, IOptions<OAuth2Keys> auth2Keys)
        {
            _tokens = tokens;
            _fortnoxSettings = fortnoxSettings.Value;
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
                throw new Exception("Fortnox Api not Connected.");
            }


            try
            {
                var context = new FortnoxContext(token);
                apiCallFunction(context);
            }
            catch (FortnoxApiException ex)
            {
                if (ex.ResponseContent == "{\"message\":\"unauthorized\"}")
                {
                    var fortnoxAuthClient = new FortnoxAuthClient();
                    var authWorkflow = fortnoxAuthClient.StandardAuthWorkflow;
                    var tokens = await authWorkflow.RefreshTokenAsync(token.RefreshToken, _auth2keys.ClientId, _auth2keys.ClientSecret);
                    if (tokens.AccessToken != null && tokens.RefreshToken != null)
                    {
                        await UpdateTokens(tokens.AccessToken, tokens.RefreshToken);
                        // TODO: Possible infinite loop
                        await FortnoxApiCall(apiCallFunction);
                    }
                }
                else
                {
                    // TODO: What to do? For now, assumes faulty token and removes it ( should replace with logout fnc )
                    _tokens.Token.RemoveRange(_tokens.Token);
                    await _tokens.SaveChangesAsync();
                    throw new Exception(message: ex.Message);
                }
            }
        }


        public async Task<Token> UpdateTokens(string newAccessToken, string newRefreshToken)
        {
            var token = await _tokens.Token.FirstOrDefaultAsync();
            if (token != null)
            {
                token.AccessToken = newAccessToken;
                token.RefreshToken = newRefreshToken;
                _tokens.SaveChanges();
            }

            return token;
        }
    }
}