using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DevPilotAI.Infrastructure.Services.EmbeddingProviders;

public class AzureOpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;

    public string ProviderName => "AzureOpenAI";

    public AzureOpenAIEmbeddingProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var apiKey = configuration["EmbeddingSettings:ApiKey"] ?? string.Empty;
        var endpoint = configuration["EmbeddingSettings:BaseUrl"] ?? string.Empty;
        
        if (!string.IsNullOrEmpty(endpoint))
        {
            if (!endpoint.EndsWith("/"))
            {
                endpoint += "/";
            }
            _httpClient.BaseAddress = new Uri(endpoint);
        }
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, string model, CancellationToken cancellationToken = default)
    {
        var response = await GenerateEmbeddingsAsync(new List<string> { text }, model, cancellationToken);
        return response[0];
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, string model, CancellationToken cancellationToken = default)
    {
        var apiVersion = "2023-05-15";
        var url = $"openai/deployments/{model}/embeddings?api-version={apiVersion}";

        var request = new AzureEmbeddingRequest
        {
            Input = texts
        };

        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AzureEmbeddingResponse>(cancellationToken: cancellationToken);
        if (result?.Data == null || result.Data.Count == 0)
        {
            throw new Exception("Received empty response from Azure OpenAI Embeddings API.");
        }

        var list = new List<float[]>();
        foreach (var item in result.Data)
        {
            list.Add(item.Embedding);
        }

        return list;
    }

    private class AzureEmbeddingRequest
    {
        [JsonPropertyName("input")]
        public List<string> Input { get; set; } = new();
    }

    private class AzureEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<AzureEmbeddingData> Data { get; set; } = new();
    }

    private class AzureEmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
