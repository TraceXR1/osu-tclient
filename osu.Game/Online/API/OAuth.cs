// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using osu.Framework.Bindables;

namespace osu.Game.Online.API
{
    public class OAuth
    {
        private string clientId;
        private string clientSecret;
        private readonly string endpoint;

        public readonly Bindable<OAuthToken> Token = new Bindable<OAuthToken>();

        public string TokenString
        {
            get => Token.Value?.ToString();
            set => Token.Value = string.IsNullOrEmpty(value) ? null : OAuthToken.Parse(value);
        }

        internal OAuth(string clientId, string clientSecret, string endpoint)
        {
            Debug.Assert(clientId != null);
            Debug.Assert(clientSecret != null);
            Debug.Assert(endpoint != null);

            this.clientId = clientId;
            this.clientSecret = clientSecret;
            this.endpoint = endpoint;
        }

        internal void SetCredentials(string clientId, string clientSecret)
        {
            this.clientId = clientId;
            this.clientSecret = clientSecret;
            Token.Value = null;
        }

        internal void AuthenticateWithLogin(string username, string password)
        {
            if (string.IsNullOrEmpty(username)) throw new ArgumentException("Missing username.");
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Missing password.");

            var accessTokenRequest = new AccessTokenRequestPassword(username, password)
            {
                Url = $@"{endpoint}/oauth/token",
                Method = HttpMethod.Post,
                ClientId = clientId,
                ClientSecret = clientSecret
            };

            using (accessTokenRequest)
            {
                try
                {
                    accessTokenRequest.Perform();
                }
                catch (Exception ex)
                {
                    Token.Value = null;

                    var throwableException = ex;

                    try
                    {
                        // attempt to decode a displayable error string.
                        var error = JsonConvert.DeserializeObject<OAuthError>(accessTokenRequest.GetResponseString() ?? string.Empty);
                        if (error != null)
                            throwableException = new APIException(error.UserDisplayableError, ex, accessTokenRequest.ResponseStatusCode);
                    }
                    catch
                    {
                    }

                    throw throwableException;
                }

                Token.Value = accessTokenRequest.ResponseObject;
            }
        }

        internal void AuthenticateWithClientCredentials()
        {
            var accessTokenRequest = new AccessTokenRequestClientCredentials
            {
                Url = $@"{endpoint}/oauth/token",
                Method = HttpMethod.Post,
                ClientId = clientId,
                ClientSecret = clientSecret
            };

            using (accessTokenRequest)
            {
                accessTokenRequest.Perform();
                Token.Value = accessTokenRequest.ResponseObject;
            }
        }

        internal void AuthenticateWithAuthorizationCode(string redirectUri, string scope, CancellationToken cancellationToken)
        {
            string state = Guid.NewGuid().ToString("N");
            string authorizationUrl = $"{endpoint}/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString(scope)}&state={state}";
            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri.EndsWith('/') ? redirectUri : redirectUri + "/");
            listener.Start();
            Process.Start(new ProcessStartInfo(authorizationUrl) { UseShellExecute = true });

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            HttpListenerContext context;
            try
            {
                context = listener.GetContextAsync().WaitAsync(timeout.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("OAuth authorization timed out. Press Login to try again.");
            }
            var query = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in context.Request.Url!.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                query[Uri.UnescapeDataString(pair[0])] = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
            }
            byte[] response = Encoding.UTF8.GetBytes("Authorization received. You can return to the tournament client.");
            context.Response.OutputStream.Write(response, 0, response.Length);
            context.Response.Close();

            if (!query.TryGetValue("state", out var returnedState) || returnedState != state)
                throw new InvalidOperationException("OAuth authorization state validation failed.");
            if (query.TryGetValue("error", out var error))
                throw new APIException($"OAuth authorization failed: {error}", null);
            if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
                throw new APIException("OAuth authorization did not return a code.", null);

            var request = new AccessTokenRequestAuthorizationCode(code, redirectUri)
            {
                Url = $@"{endpoint}/oauth/token", Method = HttpMethod.Post,
                ClientId = clientId, ClientSecret = clientSecret
            };
            using (request)
            {
                request.Perform();
                Token.Value = request.ResponseObject;
            }
        }

        internal bool AuthenticateWithRefresh(string refresh)
        {
            try
            {
                var refreshRequest = new AccessTokenRequestRefresh(refresh)
                {
                    Url = $@"{endpoint}/oauth/token",
                    Method = HttpMethod.Post,
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };

                using (refreshRequest)
                {
                    refreshRequest.Perform();

                    Token.Value = refreshRequest.ResponseObject;
                    return true;
                }
            }
            catch (SocketException)
            {
                // Network failure.
                return false;
            }
            catch (HttpRequestException)
            {
                // Network failure.
                return false;
            }
            catch
            {
                // Force a full re-authentication.
                Token.Value = null;
                return false;
            }
        }

        private static readonly Lock access_token_retrieval_lock = new Lock();

        /// <summary>
        /// Should be run before any API request to make sure we have a valid key.
        /// </summary>
        private bool ensureAccessToken()
        {
            // if we already have a valid access token, let's use it.
            if (accessTokenValid) return true;

            // if not, let's try using our refresh token to request a new access token.
            if (!string.IsNullOrEmpty(Token.Value?.RefreshToken))
                // ReSharper disable once PossibleNullReferenceException
                AuthenticateWithRefresh(Token.Value.RefreshToken);

            return accessTokenValid;
        }

        private bool accessTokenValid => Token.Value?.IsValid ?? false;

        internal bool HasValidAccessToken => RequestAccessToken() != null;

        internal string RequestAccessToken()
        {
            lock (access_token_retrieval_lock)
            {
                if (!ensureAccessToken()) return null;

                return Token.Value.AccessToken;
            }
        }

        internal void Clear()
        {
            lock (access_token_retrieval_lock)
                Token.Value = null;
        }

        private class AccessTokenRequestRefresh : AccessTokenRequest
        {
            internal readonly string RefreshToken;

            internal AccessTokenRequestRefresh(string refreshToken)
            {
                RefreshToken = refreshToken;
                GrantType = @"refresh_token";
            }

            protected override void PrePerform()
            {
                AddParameter("refresh_token", RefreshToken);

                base.PrePerform();
            }
        }

        private class AccessTokenRequestPassword : AccessTokenRequest
        {
            internal readonly string Username;
            internal readonly string Password;

            internal AccessTokenRequestPassword(string username, string password)
            {
                Username = username;
                Password = password;
                GrantType = @"password";
            }

            protected override void PrePerform()
            {
                AddParameter("username", Username);
                AddParameter("password", Password);

                base.PrePerform();
            }
        }

        private class AccessTokenRequestClientCredentials : AccessTokenRequest
        {
            internal AccessTokenRequestClientCredentials()
            {
                GrantType = @"client_credentials";
                Scope = @"public";
            }
        }

        private class AccessTokenRequestAuthorizationCode : AccessTokenRequest
        {
            private readonly string code;
            private readonly string redirectUri;
            internal AccessTokenRequestAuthorizationCode(string code, string redirectUri)
            {
                this.code = code; this.redirectUri = redirectUri; GrantType = @"authorization_code";
            }
            protected override void PrePerform()
            {
                AddParameter("code", code);
                AddParameter("redirect_uri", redirectUri);
                base.PrePerform();
            }
        }

        private class AccessTokenRequest : OsuJsonWebRequest<OAuthToken>
        {
            protected string GrantType;
            protected string Scope = @"*";

            internal string ClientId;
            internal string ClientSecret;

            protected override void PrePerform()
            {
                AddParameter("grant_type", GrantType);
                AddParameter("client_id", ClientId);
                AddParameter("client_secret", ClientSecret);
                AddParameter("scope", Scope);

                base.PrePerform();
            }
        }

        private class OAuthError
        {
            public string UserDisplayableError => !string.IsNullOrEmpty(Hint) ? Hint : ErrorIdentifier;

            [JsonProperty("error")]
            public string ErrorIdentifier { get; set; }

            [JsonProperty("hint")]
            public string Hint { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }
        }
    }
}
