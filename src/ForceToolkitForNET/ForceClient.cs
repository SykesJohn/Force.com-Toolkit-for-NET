﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Reflection;
using Salesforce.Common;
using Salesforce.Common.Models;
using Salesforce.Common.Soql;
using Salesforce.Common.Models.Json;
using Salesforce.Common.Models.Xml;
using System.Dynamic;
using System.Collections;
using System.Text.Json;

namespace Salesforce.Force
{
    public class ForceClient : IForceClient, IDisposable
    {
        protected readonly XmlHttpClient _xmlHttpClient;
        protected readonly JsonHttpClient _jsonHttpClient;

	    public ISelectListResolver SelectListResolver { get; set; }

        public ForceClient(string instanceUrl, string accessToken, string apiVersion, JsonSerializerOptions Options = null)
            : this(instanceUrl, accessToken, apiVersion, new HttpClient(), new HttpClient(),false, Options)
        {
        }

        public ForceClient(string instanceUrl, string accessToken, string apiVersion, HttpClient httpClientForJson, HttpClient httpClientForXml, bool callerWillDisposeHttpClients = false, JsonSerializerOptions Options = null)
        {
            if (string.IsNullOrEmpty(instanceUrl)) throw new ArgumentNullException("instanceUrl");
            if (string.IsNullOrEmpty(accessToken)) throw new ArgumentNullException("accessToken");
            if (string.IsNullOrEmpty(apiVersion)) throw new ArgumentNullException("apiVersion");
            if (httpClientForJson == null) throw new ArgumentNullException("httpClientForJson");
            if (httpClientForXml == null) throw new ArgumentNullException("httpClientForXml");

            _jsonHttpClient = new JsonHttpClient(instanceUrl, apiVersion, accessToken, httpClientForJson, Options, callerWillDisposeHttpClients);
            _xmlHttpClient = new XmlHttpClient(instanceUrl, apiVersion, accessToken, httpClientForXml, callerWillDisposeHttpClients);
            
            SelectListResolver = new DataMemberSelectListResolver();
        }

        // STANDARD METHODS

        public Task<QueryResult<T>> QueryAsync<T>(string query)
        {
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException("query");

            return _jsonHttpClient.HttpGetAsync<QueryResult<T>>(string.Format("query?q={0}", Uri.EscapeDataString(query)));
        }

        public Task<QueryResult<T>> QueryContinuationAsync<T>(string nextRecordsUrl)
        {
            if (string.IsNullOrEmpty(nextRecordsUrl)) throw new ArgumentNullException("nextRecordsUrl");

            return _jsonHttpClient.HttpGetAsync<QueryResult<T>>(nextRecordsUrl);
        }

        public Task<QueryResult<T>> QueryAllAsync<T>(string query)
        {
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException("query");

            return _jsonHttpClient.HttpGetAsync<QueryResult<T>>(string.Format("queryAll/?q={0}", Uri.EscapeDataString(query)));
        }

        public async Task<System.IO.Stream> GetBlobAsync(String objectName, String objectId, String fieldName)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException(nameof(objectName));
            if (string.IsNullOrEmpty(objectId)) throw new ArgumentNullException(nameof(objectId));
            if (string.IsNullOrEmpty(fieldName)) throw new ArgumentNullException(nameof(fieldName));

            return await _jsonHttpClient.HttpGetBlobAsync($"sobjects/{objectName}/{objectId}/{fieldName}");
        }

        public async Task<T> ExecuteRestApiAsync<T>(string apiName)
        {
            if (string.IsNullOrEmpty(apiName)) throw new ArgumentNullException("apiName");

            var response = await _jsonHttpClient.HttpGetRestApiAsync<T>(apiName);
            return response;
        }

        public async Task<T> ExecuteRestApiAsync<T>(string apiName, object inputObject)
        {
            if (string.IsNullOrEmpty(apiName)) throw new ArgumentNullException("apiName");
            if (inputObject == null) throw new ArgumentNullException("inputObject");

            var response = await _jsonHttpClient.HttpPostRestApiAsync<T>(apiName, inputObject);
            return response;
        }

    public async Task<HttpResponseMessage> ExecuteRestApiVoidAsync(string apiName, object inputObject)
      {
			if(string.IsNullOrEmpty(apiName)) throw new ArgumentNullException("apiName");
			if(inputObject == null) throw new ArgumentNullException(nameof(inputObject));
			var response = await _jsonHttpClient.HttpPostRestApiVoidAsync(apiName, inputObject);
			return response;
			}

		public async Task<T> QueryByIdAsync<T>(string objectName, string recordId)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (string.IsNullOrEmpty(recordId)) throw new ArgumentNullException("recordId");

            var fields = SelectListResolver.GetFieldsList<T>();
            var query = string.Format("SELECT {0} FROM {1} WHERE Id = '{2}'", fields, objectName, recordId);
            var results = await QueryAsync<T>(query).ConfigureAwait(false);

            return results.Records.FirstOrDefault();
        }

        public async Task<T> QueryAllFieldsByIdAsync<T>(string objectName, string recordId)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (string.IsNullOrEmpty(recordId)) throw new ArgumentNullException("recordId");

            var fields = await GetFieldsCommaSeparatedListAsync(objectName);
            var query = string.Format("SELECT {0} FROM {1} WHERE Id = '{2}'", fields, objectName, recordId);
            var results = await QueryAsync<T>(query).ConfigureAwait(false);

            return results.Records.FirstOrDefault();
        }

        public async Task<T> QueryAllFieldsByExternalIdAsync<T>(string objectName, string externalIdFieldName, string externalId)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (string.IsNullOrEmpty(externalIdFieldName)) throw new ArgumentNullException("externalIdFieldName");
            if (string.IsNullOrEmpty(externalId)) throw new ArgumentNullException("externalId");

            var fields = await GetFieldsCommaSeparatedListAsync(objectName);
            var query = string.Format("SELECT {0} FROM {1} WHERE {2} = '{3}'", fields, objectName, externalIdFieldName, externalId);
            var results = await QueryAsync<T>(query).ConfigureAwait(false);

            return results.Records.FirstOrDefault();
        }

        public async Task<SuccessResponse> CreateAsync(string objectName, object record)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (record == null) throw new ArgumentNullException("record");

            return await _jsonHttpClient.HttpPostAsync<SuccessResponse>(record, string.Format("sobjects/{0}", objectName)).ConfigureAwait(false);
        }

        public async Task<SaveResponse> CreateAsync(string objectName, CreateRequest request)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (request == null)
                throw new ArgumentNullException("request");

            var result = await _jsonHttpClient.HttpPostAsync<SaveResponse>(request, string.Format("composite/tree/{0}", objectName)).ConfigureAwait(false);

            return result;
        }

        public Task<SuccessResponse> UpdateAsync(string objectName, string recordId, object record)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (string.IsNullOrEmpty(recordId)) throw new ArgumentNullException("recordId");
            if (record == null) throw new ArgumentNullException("record");

            return _jsonHttpClient.HttpPatchAsync(record, string.Format("sobjects/{0}/{1}", objectName, recordId));
        }

        public Task<SuccessResponse> UpsertExternalAsync(string objectName, string externalFieldName, string externalId, object record)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (string.IsNullOrEmpty(externalFieldName)) throw new ArgumentNullException("externalFieldName");
            if (string.IsNullOrEmpty(externalId)) throw new ArgumentNullException("externalId");
            if (record == null) throw new ArgumentNullException("record");

            return _jsonHttpClient.HttpPatchAsync(record, string.Format("sobjects/{0}/{1}/{2}", objectName, externalFieldName, externalId));
        }

        public Task<SuccessResponse> UpsertExternalAsync(string objectName, string externalFieldName, string externalId, object record, bool ignoreNull)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (string.IsNullOrEmpty(externalFieldName)) throw new ArgumentNullException("externalFieldName");
            if (string.IsNullOrEmpty(externalId)) throw new ArgumentNullException("externalId");
            if (record == null) throw new ArgumentNullException("record");

            return _jsonHttpClient.HttpPatchAsync(record, string.Format("sobjects/{0}/{1}/{2}", objectName, externalFieldName, externalId), ignoreNull);
        }


        public Task<bool> DeleteAsync(string objectName, string recordId)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (string.IsNullOrEmpty(recordId)) throw new ArgumentNullException("recordId");

            return _jsonHttpClient.HttpDeleteAsync(string.Format("sobjects/{0}/{1}", objectName, recordId));
        }

        public Task<bool> DeleteExternalAsync(string objectName, string externalFieldName, string externalId)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (string.IsNullOrEmpty(externalFieldName)) throw new ArgumentNullException("externalFieldName");
            if (string.IsNullOrEmpty(externalId)) throw new ArgumentNullException("externalId");

            return _jsonHttpClient.HttpDeleteAsync(string.Format("sobjects/{0}/{1}/{2}", objectName, externalFieldName, externalId));
        }

        public Task<DescribeGlobalResult<T>> GetObjectsAsync<T>()
        {
            return _jsonHttpClient.HttpGetAsync<DescribeGlobalResult<T>>("sobjects");
        }

        public Task<T> BasicInformationAsync<T>(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");

            return _jsonHttpClient.HttpGetAsync<T>(string.Format("sobjects/{0}", objectName));
        }

        public Task<T> DescribeAsync<T>(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");

            return _jsonHttpClient.HttpGetAsync<T>(string.Format("sobjects/{0}/describe/", objectName));
        }

        public Task<T> GetDeleted<T>(string objectName, DateTime startDateTime, DateTime endDateTime)
        {
            var sdt = Uri.EscapeDataString(startDateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss+00:00", System.Globalization.CultureInfo.InvariantCulture));
            var edt = Uri.EscapeDataString(endDateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss+00:00", System.Globalization.CultureInfo.InvariantCulture));

            return _jsonHttpClient.HttpGetAsync<T>(string.Format("sobjects/{0}/deleted/?start={1}&end={2}", objectName, sdt, edt));
        }

        public Task<T> GetUpdated<T>(string objectName, DateTime startDateTime, DateTime endDateTime)
        {
            var sdt = Uri.EscapeDataString(startDateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss+00:00", System.Globalization.CultureInfo.InvariantCulture));
            var edt = Uri.EscapeDataString(endDateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss+00:00", System.Globalization.CultureInfo.InvariantCulture));

            return _jsonHttpClient.HttpGetAsync<T>(string.Format("sobjects/{0}/updated/?start={1}&end={2}", objectName, sdt, edt));
        }

        public Task<T> DescribeLayoutAsync<T>(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            return _jsonHttpClient.HttpGetAsync<T>(string.Format("sobjects/{0}/describe/layouts/", objectName));
        }

        public Task<T> DescribeLayoutAsync<T>(string objectName, string recordTypeId)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException("objectName");
            if (string.IsNullOrEmpty(recordTypeId)) throw new ArgumentNullException("recordTypeId");

            return _jsonHttpClient.HttpGetAsync<T>(string.Format("sobjects/{0}/describe/layouts/{1}", objectName, recordTypeId));
        }

        public Task<T> RecentAsync<T>(int limit = 200)
        {
            return _jsonHttpClient.HttpGetAsync<T>(string.Format("recent/?limit={0}", limit));
        }

        public Task<List<T>> SearchAsync<T>(string query)
        {
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException("query");
            if (!query.Contains("FIND")) throw new ArgumentException("query does not contain FIND");
            if (!query.Contains("{") || !query.Contains("}")) throw new ArgumentException("search term must be wrapped in braces");

            return _jsonHttpClient.HttpGetAsync<List<T>>(string.Format("search?q={0}", Uri.EscapeDataString(query)));
        }

        public async Task<T> UserInfo<T>(string url)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentNullException("url");
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute)) throw new FormatException("url");

            var response = await _jsonHttpClient.HttpGetAsync<T>(new Uri(url));
            return response;
        }

        public async Task<string> GetFieldsCommaSeparatedListAsync(string objectName)
        {
            IDictionary<string, object> objectProperties = await DescribeAsync<ExpandoObject>(objectName);
            objectProperties.TryGetValue("fields", out object fields);
            List<string> objectDescription = new List<string>();
            foreach (ExpandoObject field in fields as IEnumerable)
            {
                objectDescription.Add((field).FirstOrDefault(x => x.Key == "name").Value.ToString());                
            }
            return string.Join(", ", objectDescription);
        }
        
        public Task<T> ExecuteAnonymousAsync<T>(string apex)
        {
            if (string.IsNullOrEmpty(apex)) throw new ArgumentNullException("apex");
            return _jsonHttpClient.HttpGetAsync<T>(string.Format("tooling/executeAnonymous/?anonymousBody={0}", Uri.EscapeDataString(apex)));
        }

        // BULK METHODS

        public async Task<List<BatchInfoResult>> RunJobAsync<T>(string objectName, BulkConstants.OperationType operationType,
            IEnumerable<ISObjectList<T>> recordsLists)
        {
            return await RunJobAsync(objectName, null, operationType, recordsLists);
        }

        public async Task<List<BatchInfoResult>> RunJobAsync<T>(string objectName, string externalIdFieldName,
            BulkConstants.OperationType operationType, IEnumerable<ISObjectList<T>> recordsLists)
        {
            if (recordsLists == null) throw new ArgumentNullException("recordsLists");

            if (operationType == BulkConstants.OperationType.Upsert && string.IsNullOrEmpty(externalIdFieldName)) throw new ArgumentNullException(nameof(externalIdFieldName));

            var jobInfoResult = await CreateJobAsync(objectName, externalIdFieldName, operationType);
            var batchResults = new List<BatchInfoResult>();
            foreach (var recordList in recordsLists)
            {
                batchResults.Add(await CreateJobBatchAsync(jobInfoResult, recordList));
            }
            await CloseJobAsync(jobInfoResult);
            return batchResults;
        }

        public async Task<List<BatchResultList>> RunJobAndPollAsync<T>(string objectName, BulkConstants.OperationType operationType,
            IEnumerable<ISObjectList<T>> recordsLists)
        {
            return await RunJobAndPollAsync(objectName, null, operationType, recordsLists);
        }

        public async Task<List<BatchResultList>> RunJobAndPollAsync<T>(string objectName, string externalIdFieldName, BulkConstants.OperationType operationType,
            IEnumerable<ISObjectList<T>> recordsLists)
        {
            const float pollingStart = 1000;
            const float pollingIncrease = 2.0f;

            var batchInfoResults = await RunJobAsync(objectName, externalIdFieldName, operationType, recordsLists);

            var currentPoll = pollingStart;
            var finishedBatchInfoResults = new List<BatchInfoResult>();
            while (batchInfoResults.Count > 0)
            {
                var removeList = new List<BatchInfoResult>();
                foreach (var batchInfoResult in batchInfoResults)
                {
                    var batchInfoResultNew = await PollBatchAsync(batchInfoResult);
                    if (batchInfoResultNew.State.Equals(BulkConstants.BatchState.Completed.Value()) ||
                        batchInfoResultNew.State.Equals(BulkConstants.BatchState.Failed.Value()) ||
                        batchInfoResultNew.State.Equals(BulkConstants.BatchState.NotProcessed.Value()))
                    {
                        finishedBatchInfoResults.Add(batchInfoResultNew);
                        removeList.Add(batchInfoResult);
                    }
                }
                foreach (var removeItem in removeList)
                {
                    batchInfoResults.Remove(removeItem);
                }

                await Task.Delay((int)currentPoll);
                currentPoll *= pollingIncrease;
            }


            var batchResults = new List<BatchResultList>();
            foreach (var batchInfoResultComplete in finishedBatchInfoResults)
            {
                batchResults.Add(await GetBatchResultAsync(batchInfoResultComplete));
            }
            return batchResults;
        }

        public async Task<JobInfoResult> CreateJobAsync(string objectName, BulkConstants.OperationType operationType)
        {
            return await CreateJobAsync(objectName, null, operationType);
        }

        public async Task<JobInfoResult> CreateJobAsync(string objectName, string externalIdFieldName, BulkConstants.OperationType operationType)
        {
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentNullException(nameof(objectName));

            if (operationType == BulkConstants.OperationType.Upsert && string.IsNullOrEmpty(externalIdFieldName)) throw new ArgumentNullException(nameof(externalIdFieldName));

            var jobInfo = new JobInfo
            {
                ContentType = "XML",
                Object = objectName,
                ExternalIdFieldName = externalIdFieldName,
                Operation = operationType.Value()
            };

            return await _xmlHttpClient.HttpPostAsync<JobInfoResult>(jobInfo, "/services/async/{0}/job");
        }

        public async Task<BatchInfoResult> CreateJobBatchAsync<T>(JobInfoResult jobInfo, ISObjectList<T> recordsList)
        {
            if (jobInfo == null) throw new ArgumentNullException("jobInfo");
            return await CreateJobBatchAsync(jobInfo.Id, recordsList).ConfigureAwait(false);
        }

        public async Task<BatchInfoResult> CreateJobBatchAsync<T>(string jobId, ISObjectList<T> recordsObject)
        {
            if (string.IsNullOrEmpty(jobId)) throw new ArgumentNullException("jobId");
            if (recordsObject == null) throw new ArgumentNullException("recordsObject");

            return await _xmlHttpClient.HttpPostAsync<BatchInfoResult>(recordsObject, string.Format("/services/async/{{0}}/job/{0}/batch", jobId))
                .ConfigureAwait(false);
        }

        public async Task<JobInfoResult> CloseJobAsync(JobInfoResult jobInfo)
        {
            if (jobInfo == null) throw new ArgumentNullException("jobInfo");
            return await CloseJobAsync(jobInfo.Id);
        }

        public async Task<JobInfoResult> CloseJobAsync(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) throw new ArgumentNullException("jobId");

            var state = new JobInfoState { State = "Closed" };
            return await _xmlHttpClient.HttpPostAsync<JobInfoResult>(state, string.Format("/services/async/{{0}}/job/{0}", jobId))
                .ConfigureAwait(false);
        }

        public async Task<JobInfoResult> PollJobAsync(JobInfoResult jobInfo)
        {
            if (jobInfo == null) throw new ArgumentNullException("jobInfo");
            return await PollJobAsync(jobInfo.Id);
        }

        public async Task<JobInfoResult> PollJobAsync(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) throw new ArgumentNullException("jobId");

            return await _xmlHttpClient.HttpGetAsync<JobInfoResult>(string.Format("/services/async/{{0}}/job/{0}", jobId))
                .ConfigureAwait(false);
        }

        public async Task<BatchInfoResult> PollBatchAsync(BatchInfoResult batchInfo)
        {
            if (batchInfo == null) throw new ArgumentNullException("batchInfo");
            return await PollBatchAsync(batchInfo.Id, batchInfo.JobId);
        }

        public async Task<BatchInfoResult> PollBatchAsync(string batchId, string jobId)
        {
            if (string.IsNullOrEmpty(batchId)) throw new ArgumentNullException("batchId");
            if (string.IsNullOrEmpty(jobId)) throw new ArgumentNullException("jobId");

            return await _xmlHttpClient.HttpGetAsync<BatchInfoResult>(string.Format("/services/async/{{0}}/job/{0}/batch/{1}", jobId, batchId))
                .ConfigureAwait(false);
        }

        public async Task<BatchResultList> GetBatchResultAsync(BatchInfoResult batchInfo)
        {
            if (batchInfo == null) throw new ArgumentNullException("batchInfo");
            return await GetBatchResultAsync(batchInfo.Id, batchInfo.JobId);
        }

        public async Task<BatchResultList> GetBatchResultAsync(string batchId, string jobId)
        {
            if (string.IsNullOrEmpty(batchId)) throw new ArgumentNullException("batchId");
            if (string.IsNullOrEmpty(jobId)) throw new ArgumentNullException("jobId");

            return await _xmlHttpClient.HttpGetAsync<BatchResultList>(string.Format("/services/async/{{0}}/job/{0}/batch/{1}/result", jobId, batchId))
               .ConfigureAwait(false);
        }

        public void Dispose()
        {
            _jsonHttpClient.Dispose();
            _xmlHttpClient.Dispose();
        }
    }
}
