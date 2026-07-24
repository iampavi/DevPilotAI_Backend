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

public class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;

    public string ProviderName => "OpenAI";

    public OpenAIEmbeddingProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var apiKey = configuration["EmbeddingSettings:ApiKey"] ?? string.Empty;
        var baseUrl = configuration["EmbeddingSettings:BaseUrl"] ?? "https://api.openai.com/v1/";
        
        if (!baseUrl.EndsWith("/"))
        {
            baseUrl += "/";
        }
        
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, string model, CancellationToken cancellationToken = default)
    {
        var response = await GenerateEmbeddingsAsync(new List<string> { text }, model, cancellationToken);
        return response[0];
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, string model, CancellationToken cancellationToken = default)
    {
        var request = new OpenAiEmbeddingRequest
        {
            Model = model,
            Input = texts
        };

        var response = await _httpClient.PostAsJsonAsync("embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAiEmbeddingResponse>(cancellationToken: cancellationToken);
        if (result?.Data == null || result.Data.Count == 0)
        {
            throw new Exception("Received empty response from OpenAI Embeddings API.");
        }

        var list = new List<float[]>();
        foreach (var item in result.Data)
        {
            list.Add(item.Embedding);
        }

        return list;
    }

    private class OpenAiEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public List<string> Input { get; set; } = new();
    }

    private class OpenAiEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAiEmbeddingData> Data { get; set; } = new();
    }

    private class OpenAiEmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
