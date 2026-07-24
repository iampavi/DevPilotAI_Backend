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

public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;

    public string ProviderName => "Ollama";

    public OllamaEmbeddingProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var baseUrl = configuration["EmbeddingSettings:BaseUrl"] ?? "http://localhost:11434/";
        
        if (!baseUrl.EndsWith("/"))
        {
            baseUrl += "/";
        }
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, string model, CancellationToken cancellationToken = default)
    {
        var request = new OllamaSingleEmbeddingRequest
        {
            Model = model,
            Prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync("api/embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaSingleEmbeddingResponse>(cancellationToken: cancellationToken);
        if (result?.Embedding == null)
        {
            throw new Exception("Received empty response from Ollama single embedding API.");
        }

        return result.Embedding;
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, string model, CancellationToken cancellationToken = default)
    {
        var list = new List<float[]>();
        foreach (var text in texts)
        {
            list.Add(await GenerateEmbeddingAsync(text, model, cancellationToken));
        }
        return list;
    }

    private class OllamaSingleEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;
    }

    private class OllamaSingleEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
