using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mindrift.Online.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace Mindrift.Online.Core
{
    public sealed class ApiClient
    {
        private const string JsonMimeType = "application/json";

        private readonly ApiConfig config;

        public ApiClient(ApiConfig customConfig = null)
        {
            config = customConfig != null ? customConfig : ApiConfig.Active;
        }

        public Task<ApiRequestResult<TData>> GetAsync<TData>(string route, string bearerToken = null, CancellationToken cancellationToken = default)
        {
            return SendAsync<TData>(UnityWebRequest.kHttpVerbGET, route, null, bearerToken, cancellationToken);
        }

        public Task<ApiRequestResult<TData>> PostAsync<TPayload, TData>(string route, TPayload payload, string bearerToken = null, CancellationToken cancellationToken = default)
        {
            string body = JsonHelper.Serialize(payload, prettyPrint: false);
            return SendAsync<TData>(UnityWebRequest.kHttpVerbPOST, route, body, bearerToken, cancellationToken);
        }

        private async Task<ApiRequestResult<TData>> SendAsync<TData>(string method, string route, string jsonBody, string bearerToken, CancellationToken cancellationToken)
        {
            string url = BuildUrl(route);
            using UnityWebRequest request = BuildRequest(method, url, jsonBody, bearerToken);

            LogVerbose($"[API] {method} {url}");
            if (!string.IsNullOrWhiteSpace(jsonBody))
            {
                LogVerbose($"[API] payload: {jsonBody}");
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    return ApiRequestResult<TData>.Failed("Request cancelled.");
                }

                await Task.Yield();
            }

            long statusCode = request.responseCode;
            string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

            LogVerbose($"[API] status={statusCode} body={responseBody}");

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return ApiRequestResult<TData>.Failed("Unable to connect to server.", statusCode, responseBody);
            }

            bool unauthorized = statusCode == 401 || statusCode == 403;
            if (request.result == UnityWebRequest.Result.ProtocolError || statusCode >= 400)
            {
                string protocolMessage = ResolveMessageFromBody(responseBody, request.error);
                if (unauthorized)
                {
                    return ApiRequestResult<TData>.Unauthorized(protocolMessage, statusCode, responseBody);
                }

                return ApiRequestResult<TData>.Failed(protocolMessage, statusCode, responseBody);
            }

            if (!JsonHelper.TryDeserialize<ApiResponse<TData>>(responseBody, out ApiResponse<TData> envelope, out string parseError))
            {
                return ApiRequestResult<TData>.Failed(parseError, statusCode, responseBody);
            }

            if (envelope == null)
            {
                return ApiRequestResult<TData>.Failed("Empty server response.", statusCode, responseBody);
            }

            if (!envelope.success)
            {
                string message = string.IsNullOrWhiteSpace(envelope.message) ? "Request failed." : envelope.message;
                return ApiRequestResult<TData>.Failed(message, statusCode, responseBody);
            }

            return ApiRequestResult<TData>.Succeeded(envelope.data, statusCode, responseBody);
        }

        private UnityWebRequest BuildRequest(string method, string url, string jsonBody, string bearerToken)
        {
            UnityWebRequest request;
            if (string.Equals(method, UnityWebRequest.kHttpVerbGET, StringComparison.OrdinalIgnoreCase))
            {
                request = UnityWebRequest.Get(url);
            }
            else
            {
                byte[] payload = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody);
                request = new UnityWebRequest(url, method)
                {
                    uploadHandler = new UploadHandlerRaw(payload),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Content-Type", JsonMimeType);
            }

            request.downloadHandler ??= new DownloadHandlerBuffer();
            request.timeout = config.TimeoutSeconds;
            request.SetRequestHeader("Accept", JsonMimeType);

            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {bearerToken.Trim()}");
            }

            return request;
        }

        private string BuildUrl(string route)
        {
            string safeRoute = string.IsNullOrWhiteSpace(route) ? string.Empty : route.TrimStart('/');
            return $"{config.BaseApiUrl}/{safeRoute}";
        }

        private static string ResolveMessageFromBody(string rawBody, string fallback)
        {
            if (JsonHelper.TryDeserialize<ApiMessageEnvelope>(rawBody, out ApiMessageEnvelope envelope, out _))
            {
                if (envelope != null && !string.IsNullOrWhiteSpace(envelope.message))
                {
                    return envelope.message.Trim();
                }
            }

            return string.IsNullOrWhiteSpace(fallback) ? "Unable to connect to server." : fallback;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogVerbose(string message)
        {
            if (!config.VerboseLogging)
            {
                return;
            }

            Debug.Log(message);
        }
    }

    public sealed class ApiRequestResult<TData>
    {
        public bool Success { get; private set; }
        public bool IsUnauthorized { get; private set; }
        public string ErrorMessage { get; private set; }
        public long StatusCode { get; private set; }
        public string RawBody { get; private set; }
        public TData Data { get; private set; }

        public static ApiRequestResult<TData> Succeeded(TData data, long statusCode, string rawBody)
        {
            return new ApiRequestResult<TData>
            {
                Success = true,
                ErrorMessage = string.Empty,
                StatusCode = statusCode,
                RawBody = rawBody ?? string.Empty,
                Data = data
            };
        }

        public static ApiRequestResult<TData> Failed(string message, long statusCode = 0, string rawBody = "")
        {
            return new ApiRequestResult<TData>
            {
                Success = false,
                IsUnauthorized = false,
                ErrorMessage = string.IsNullOrWhiteSpace(message) ? "Request failed." : message,
                StatusCode = statusCode,
                RawBody = rawBody ?? string.Empty,
                Data = default
            };
        }

        public static ApiRequestResult<TData> Unauthorized(string message, long statusCode = 401, string rawBody = "")
        {
            return new ApiRequestResult<TData>
            {
                Success = false,
                IsUnauthorized = true,
                ErrorMessage = string.IsNullOrWhiteSpace(message) ? "Session expired. Please sign in again." : message,
                StatusCode = statusCode,
                RawBody = rawBody ?? string.Empty,
                Data = default
            };
        }
    }
}
