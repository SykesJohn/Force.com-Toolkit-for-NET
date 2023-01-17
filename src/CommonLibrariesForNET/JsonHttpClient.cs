using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
using Salesforce.Common.Internals;
using Salesforce.Common.Models.Json;
using Salesforce.Common.Serializer;

namespace Salesforce.Common
	{
	public class JsonHttpClient:BaseHttpClient, IJsonHttpClient
		{
		private const string DateFormat = "s";
		public static JsonSerializerOptions JsonOptions;

		public JsonHttpClient(string instanceUrl, string apiVersion, string accessToken, HttpClient httpClient,
			JsonSerializerOptions Options = null, bool callerWillDisposeHttpClient = false)
				: base(instanceUrl, apiVersion, "application/json", httpClient, callerWillDisposeHttpClient)
			{
			HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
			if(Options == null)
				{
				JsonOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web);
				JsonOptions.DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull;
				JsonOptions.IncludeFields=true;
				JsonOptions.Converters.Add(new JsonStringEnumConverter());
				}
			}

		private static ForceException ParseForceException(string responseMessage)
			{
			var errorResponse = JsonSerializer.Deserialize<ErrorResponses>(responseMessage, JsonOptions);
			return new ForceException(errorResponse[0].ErrorCode, errorResponse[0].Message);
			}

		// GET

		public async Task<T> HttpGetAsync<T>(string urlSuffix)
			{
			var url = Common.FormatUrl(urlSuffix, InstanceUrl, ApiVersion);
			return await HttpGetAsync<T>(url);
			}

		public async Task<T> HttpGetAsync<T>(Uri uri)
			{
			try
				{
				var response = await HttpGetAsync(uri);
				var obj = JsonSerializer.Deserialize<T>(response, JsonOptions);
				return obj;
				//      var jToken = JsonDocument.Parse(response).RootElement;
				//      if (jToken.ValueKind == JsonValueKind.Array)
				//      {
				//return jToken.Deserialize<T>(JsonOptions);
				//          //var jArray = JArray.Parse(response);
				//          //return JsonSerializer.Deserialize<T>(jArray.ToString());
				//      }
				//      // else
				//      try
				//      {
				//          var jObject = JsonDocument.Parse(response).RootElement;
				//          return jObject.Deserialize<T>(JsonOptions);
				//      }
				//      catch
				//      {
				//          return JsonSerializer.Deserialize<T>(response, JsonOptions);
				//      }
				}
			catch(BaseHttpClientException e)
				{
				throw ParseForceException(e.Message);
				}
			}

		internal class ListWrapper<T>
			{
			public string NextRecordsUrl { get; set; }
			public IList<T> NodeName { get; set; }
			}
		public async Task<IList<T>> HttpGetAsync<T>(string urlSuffix, string nodeName)
			{
			string next = null;
			var records = new List<T>();
			var url = Common.FormatUrl(urlSuffix, InstanceUrl, ApiVersion);
			do
				{
				if(next != null)
					{
					url = Common.FormatUrl(string.Format("query/{0}", next.Split('/').Last()), InstanceUrl, ApiVersion);
					}
				try
					{
					var response = await HttpGetAsync(url);
					var obj = JsonSerializer.Deserialize<ExpandoObject>(response, JsonOptions);
					//               var jToken = obj.GetProperty(nodeName);
					var objDict = obj as IDictionary<string, object>;
					next = (string)objDict["nextRecordsUrl"];
					//               //next = (nextRec != null) ? nextRec.GetString() : null;
					records.AddRange((IList<T>)objDict[nodeName]);
					}
				catch(BaseHttpClientException e)
					{
					throw ParseForceException(e.Message);
					}
				}
			while(!string.IsNullOrEmpty(next));

			return records;
			}


		public async Task<System.IO.Stream> HttpGetBlobAsync(string urlSuffix)
			{
			var url = Common.FormatUrl(urlSuffix, InstanceUrl, ApiVersion);

			var request = new HttpRequestMessage
				{
				RequestUri = url,
				Method = HttpMethod.Get
				};

			var responseMessage = await HttpClient.SendAsync(request).ConfigureAwait(false);
			var response = await responseMessage.Content.ReadAsStreamAsync();

			if(responseMessage.IsSuccessStatusCode)
				{
				return response;
				}
			else
				{
				return new System.IO.MemoryStream();
				}
			}

		public async Task<T> HttpGetRestApiAsync<T>(string apiName)
			{
			var url = Common.FormatRestApiUrl(apiName, InstanceUrl);
			return await HttpGetAsync<T>(url);
			}

		// POST

		public async Task<T> HttpPostAsync<T>(object inputObject, string urlSuffix)
			{
			var url = Common.FormatUrl(urlSuffix, InstanceUrl, ApiVersion);
			return await HttpPostAsync<T>(inputObject, url);
			}

		public async Task<T> HttpPostAsync<T>(object inputObject, Uri uri)
			{
			var json = JsonSerializer.Serialize(inputObject, JsonOptions);
			try
				{
				var response = await HttpPostAsync(json, uri);
				return JsonSerializer.Deserialize<T>(response, JsonOptions);
				}
			catch(BaseHttpClientException e)
				{
				throw ParseForceException(e.Message);
				}
			}

		public async Task<HttpResponseMessage> HttpPostVoidAsync(object inputObject, Uri uri)
			{
			var json = JsonSerializer.Serialize(inputObject, JsonOptions);
			try
				{
				return await base.HttpPostVoidAsync(json, uri);
				}
			catch(BaseHttpClientException e)
				{
				throw ParseForceException(e.Message);
				}
			}

		public async Task<T> HttpPostRestApiAsync<T>(string apiName, object inputObject)
			{
			var url = Common.FormatRestApiUrl(apiName, InstanceUrl);
			return await HttpPostAsync<T>(inputObject, url);
			}

		public async Task<HttpResponseMessage> HttpPostRestApiVoidAsync(string apiName, object inputObject)
			{
			var url = Common.FormatRestApiUrl(apiName, InstanceUrl);
			return await HttpPostVoidAsync(inputObject, url);
			}

		public async Task<T> HttpBinaryDataPostAsync<T>(string urlSuffix, object inputObject, byte[] fileContents, string headerName, string fileName)
			{
			// BRAD: I think we should probably, in time, refactor multipart and binary support to the BaseHttpClient.
			// For now though, I just left this in here.

			var uri = Common.FormatUrl(urlSuffix, InstanceUrl, ApiVersion);
			var jopts = new JsonSerializerOptions(JsonOptions);
			jopts.DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull;
			var json = JsonSerializer.Serialize(inputObject, jopts);

			var content = new MultipartFormDataContent();

			var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
			stringContent.Headers.Add("Content-Disposition", "form-data; name=\"json\"");
			content.Add(stringContent);

			var byteArrayContent = new ByteArrayContent(fileContents);
			byteArrayContent.Headers.Add("Content-Type", "application/octet-stream");
			byteArrayContent.Headers.Add("Content-Disposition", string.Format("form-data; name=\"{0}\"; filename=\"{1}\"", headerName, fileName));
			content.Add(byteArrayContent, headerName, fileName);

			var responseMessage = await HttpClient.PostAsync(uri, content).ConfigureAwait(false);
			var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

			if(responseMessage.IsSuccessStatusCode)
				{
				return JsonSerializer.Deserialize<T>(response, JsonOptions);
				}

			throw ParseForceException(response);
			}

		// PATCH

		public async Task<SuccessResponse> HttpPatchAsync(object inputObject, string urlSuffix)
			{
			var url = Common.FormatUrl(urlSuffix, InstanceUrl, ApiVersion);
			return await HttpPatchAsync(inputObject, url);
			}

		public async Task<SuccessResponse> HttpPatchAsync(object inputObject, string urlSuffix, bool ignoreNull)
			{
			var url = Common.FormatUrl(urlSuffix, InstanceUrl, ApiVersion);
			if(ignoreNull == true)
				{
				return await HttpPatchAsync(inputObject, url);
				}
			else
				{
				var jopts = new JsonSerializerOptions(JsonOptions);
				jopts.DefaultIgnoreCondition=JsonIgnoreCondition.Never;
				return await HttpPatchAsync(inputObject, url, jopts);
				}
			}


		public async Task<SuccessResponse> HttpPatchAsync(object inputObject, Uri uri)
			{
			var json = JsonSerializer.Serialize(inputObject, JsonOptions);
			try
				{
				var response = await base.HttpPatchAsync(json, uri);
				return string.IsNullOrEmpty(response) ?
						new SuccessResponse { Id = "", Errors = "", Success = true } :
						JsonSerializer.Deserialize<SuccessResponse>(response, JsonOptions);
				}
			catch(BaseHttpClientException e)
				{
				throw ParseForceException(e.Message);
				}
			}

		public async Task<SuccessResponse> HttpPatchAsync(object inputObject, Uri uri, JsonSerializerOptions nullValueHandling)
			{

			var json = JsonSerializer.Serialize(inputObject, nullValueHandling);

			try
				{
				var response = await base.HttpPatchAsync(json, uri);
				return string.IsNullOrEmpty(response) ?
						new SuccessResponse { Id = "", Errors = "", Success = true } :
						JsonSerializer.Deserialize<SuccessResponse>(response, JsonOptions);
				}
			catch(BaseHttpClientException e)
				{
				throw ParseForceException(e.Message);
				}
			}

		// DELETE

		public async Task<bool> HttpDeleteAsync(string urlSuffix)
			{
			var url = Common.FormatUrl(urlSuffix, InstanceUrl, ApiVersion);
			return await HttpDeleteAsync(url);
			}

		public new async Task<bool> HttpDeleteAsync(Uri uri)
			{
			try
				{
				await base.HttpDeleteAsync(uri);
				return true;
				}
			catch(BaseHttpClientException e)
				{
				throw ParseForceException(e.Message);
				}
			}

		public Task<SuccessResponse> HttpPutAsync(object inputObject, string urlSuffix)
			{
			throw new NotImplementedException();
			}

		public Task<SuccessResponse> HttpPutAsync(object inputObject, Uri uri)
			{
			throw new NotImplementedException();
			}

		public Task<SuccessResponse> HttpPutAsync(object inputObject, string urlSuffix, bool ignoreNull)
			{
			throw new NotImplementedException();
			}

		public Task<SuccessResponse> HttpPutAsync(object inputObject, Uri uri, JsonSerializerOptions nullValueHandling)
			{
			throw new NotImplementedException();
			}
		}
	}
