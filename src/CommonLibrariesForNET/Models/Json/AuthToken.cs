using Newtonsoft.Json;

namespace Salesforce.Common.Models.Json
{
    public class AuthToken
    {
        [JsonProperty(PropertyName = "id")]
        public string Id;

        [JsonProperty(PropertyName = "issued_at")]
        public string Issued_At;

        [JsonProperty(PropertyName = "instance_url")]
        public string Instance_Url;

        [JsonProperty(PropertyName = "signature")]
        public string Signature;

        [JsonProperty(PropertyName = "access_token")]
        public string Access_Token;

        [JsonProperty(PropertyName = "refresh_token")]
        public string Refresh_Token;

    [JsonProperty("token_type")]
    public string Token_Type;
    }
}
