using System;
using System.Net;

namespace DevPilotAI.Application.Common.Exceptions;

public class ChatProviderException : Exception
{
    public string Provider { get; }
    public string Endpoint { get; }
    public HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }

    public ChatProviderException(string provider, string endpoint, HttpStatusCode statusCode, string responseBody)
        : base($"Failed to call {provider} API at {endpoint}. Status: {statusCode}. Response: {responseBody}")
    {
        Provider = provider;
        Endpoint = endpoint;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
