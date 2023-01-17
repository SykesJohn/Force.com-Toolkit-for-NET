using Newtonsoft.Json;

namespace Salesforce.Common.Models.Json
{
    public class AuthErrorResponse
    {
        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }
    }
}