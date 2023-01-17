using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
using Salesforce.Common.Models.Json;

namespace Salesforce.Common
{
    public class AuthenticationClient : IAuthenticationClient, IDisposable
    {
        public string InstanceUrl { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string Id { get; set; }
        public string ApiVersion { get; set; }

        private const string UserAgent = "forcedotcom-toolkit-dotnet";
        private readonly string TokenRequestEndpointUrl;
        //private const string TokenRequestEndpointUrl = "https://test.salesforce.com/services/oauth2/token";
        private readonly HttpClient _httpClient;
        private readonly bool _disposeHttpClient;
    private JsonSerializerOptions JsonOptions;

        public AuthenticationClient(string apiVersion = "v56.0", string TokenEndpoint = "https://login.salesforce.com/services/oauth2/token")
            : this(new HttpClient(), apiVersion, false, TokenEndpoint)
        {
      TokenRequestEndpointUrl=TokenEndpoint;
      JsonOptions=JsonOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web);
			JsonOptions.DefaultIgnoreCondition=JsonIgnoreCondition.Never;
      JsonOptions.IncludeFields=true;
			JsonOptions.Converters.Add(new JsonStringEnumConverter());
			}

        public AuthenticationClient(HttpClient httpClient, string apiVersion  = "v36.0", bool callerWillDisposeHttpClient = false, string TokenEndpoint = "https://login.salesforce.com/services/oauth2/token")
        {
            if (httpClient == null) throw new ArgumentNullException("httpClient");
      TokenRequestEndpointUrl=TokenEndpoint;

            _httpClient = httpClient;
            _disposeHttpClient = !callerWillDisposeHttpClient;
            ApiVersion = apiVersion;
      JsonOptions=JsonOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web);
			JsonOptions.DefaultIgnoreCondition=JsonIgnoreCondition.Never;
			JsonOptions.IncludeFields=true;
			JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        public Task UsernamePasswordAsync(string clientId, string clientSecret, string username, string password)
        {
            return UsernamePasswordAsync(clientId, clientSecret, username, password, TokenRequestEndpointUrl);
        }

        public async Task UsernamePasswordAsync(string clientId, string clientSecret, string username, string password, string tokenRequestEndpointUrl)
        {
            if (string.IsNullOrEmpty(clientId)) throw new ArgumentNullException("clientId");
            if (string.IsNullOrEmpty(clientSecret)) throw new ArgumentNullException("clientSecret");
            if (string.IsNullOrEmpty(username)) throw new ArgumentNullException("username");
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException("password");
            if (string.IsNullOrEmpty(tokenRequestEndpointUrl)) throw new ArgumentNullException("tokenRequestEndpointUrl");
            if (!Uri.IsWellFormedUriString(tokenRequestEndpointUrl, UriKind.Absolute)) throw new FormatException("tokenRequestEndpointUrl");

            var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", password)
                });

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(tokenRequestEndpointUrl),
				Content = content
            };

			request.Headers.UserAgent.ParseAdd(string.Concat(UserAgent, "/", ApiVersion));

            var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                var authToken = JsonSerializer.Deserialize<AuthToken>(response, JsonOptions);

                AccessToken = authToken.Access_Token;
                InstanceUrl = authToken.Instance_Url;
                Id = authToken.Id;
            }
            else
            {
                var errorResponse = JsonSerializer.Deserialize<AuthErrorResponse>(response, JsonOptions);
                throw new ForceAuthException(errorResponse.Error, errorResponse.ErrorDescription, responseMessage.StatusCode);
            }
        }

        public Task WebServerAsync(string clientId, string clientSecret, string redirectUri, string code)
        {
            return WebServerAsync(clientId, clientSecret, redirectUri, code, TokenRequestEndpointUrl);
        }

        public async Task WebServerAsync(string clientId, string clientSecret, string redirectUri, string code, string tokenRequestEndpointUrl)
        {
            if (string.IsNullOrEmpty(clientId)) throw new ArgumentNullException("clientId");
            if (string.IsNullOrEmpty(clientSecret)) throw new ArgumentNullException("clientSecret");
            if (string.IsNullOrEmpty(redirectUri)) throw new ArgumentNullException("redirectUri");
            if (!Uri.IsWellFormedUriString(redirectUri, UriKind.Absolute)) throw new FormatException("redirectUri");
            if (string.IsNullOrEmpty(code)) throw new ArgumentNullException("code");
            if (string.IsNullOrEmpty(tokenRequestEndpointUrl)) throw new ArgumentNullException("tokenRequestEndpointUrl");
            if (!Uri.IsWellFormedUriString(tokenRequestEndpointUrl, UriKind.Absolute)) throw new FormatException("tokenRequestEndpointUrl");

            var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri),
                    new KeyValuePair<string, string>("code", code)
                });

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(tokenRequestEndpointUrl),
                Content = content
            };

			request.Headers.UserAgent.ParseAdd(string.Concat(UserAgent, "/", ApiVersion));

            var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                var authToken = JsonSerializer.Deserialize<AuthToken>(response, JsonOptions);

                AccessToken = authToken.Access_Token;
                InstanceUrl = authToken.Instance_Url;
                Id = authToken.Id;
                RefreshToken = authToken.Refresh_Token;
            }
            else
            {
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<AuthErrorResponse>(response, JsonOptions);
                    throw new ForceAuthException(errorResponse.Error, errorResponse.ErrorDescription);
                }
                catch (Exception ex)
                {
                    throw new ForceAuthException(Error.UnknownException, ex.Message);
                }
                
            }
        }

        public Task TokenRefreshAsync(string clientId, string refreshToken, string clientSecret = "")
        {
            return TokenRefreshAsync(clientId, refreshToken, clientSecret, TokenRequestEndpointUrl);
        }

        public async Task TokenRefreshAsync(string clientId, string refreshToken, string clientSecret, string tokenRequestEndpointUrl)
        {
            var url = Common.FormatRefreshTokenUrl(
                tokenRequestEndpointUrl,
                clientId,
                refreshToken,
                clientSecret);

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url)
            };

			request.Headers.UserAgent.ParseAdd(string.Concat(UserAgent, "/", ApiVersion));

            var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                var authToken = JsonSerializer.Deserialize<AuthToken>(response, JsonOptions);

                AccessToken = authToken.Access_Token;
                RefreshToken = refreshToken;
                InstanceUrl = authToken.Instance_Url;
                Id = authToken.Id;
            }
            else
            {
                var errorResponse = JsonSerializer.Deserialize<AuthErrorResponse>(response, JsonOptions);
                throw new ForceException(errorResponse.Error, errorResponse.ErrorDescription);
            }
        }


        public async Task GetLatestVersionAsync()
        {
            try
            {
                string serviceURL = InstanceUrl + @"/services/data/";
                HttpResponseMessage responseMessage = await _httpClient.GetAsync(serviceURL);

                var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (responseMessage.IsSuccessStatusCode)
                {
                    try
                    {
                        var jToken = JsonDocument.Parse(response).RootElement;
                        if (jToken.ValueKind == JsonValueKind.Array)
                        {
                            //var jArray = JArray.Parse(response);
                            //List<Models.Json.Version> versionList = JsonSerializer.Deserialize<List<Models.Json.Version>>(jArray.ToString());
              var versionList = jToken.Deserialize<List<Models.Json.Version>>(JsonOptions);
                            if (versionList != null && versionList.Count > 0)
                            {
                                versionList.Sort();
                                if (!string.IsNullOrEmpty(versionList.Last().version))
                                    ApiVersion = "v" + versionList.Last().version;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        throw new ForceException(Error.UnknownException, e.Message);
                    }
                }
                else
                {
                    var errorResponse = JsonSerializer.Deserialize<AuthErrorResponse>(response, JsonOptions);
                    throw new ForceException(errorResponse.Error, errorResponse.ErrorDescription);
                }
            }
            catch (Exception ex)
            {
                throw new ForceAuthException(Error.UnknownException, ex.Message);
            }
        }

        public void Dispose()
        {
            if (_disposeHttpClient)
            {
                _httpClient.Dispose();
            }
        }
    }
}
