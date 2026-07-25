using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DevPilotAI.Infrastructure.Services.ChatProviders;

public class OpenAIChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIChatProvider> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public string ProviderName => "OpenAI";

    public OpenAIChatProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAIChatProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = configuration["EmbeddingSettings:ApiKey"] ?? string.Empty;
        _baseUrl = configuration["EmbeddingSettings:BaseUrl"] ?? "https://api.openai.com/v1/";

        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential
            })
            .Build();
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        List<ChatMessageDto> messages,
        ChatSettingsDto settings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, settings, cancellationToken);
        yield return response.Content;
    }

    public async Task<ChatResponseDto> GetResponseAsync(
        List<ChatMessageDto> messages,
        ChatSettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("OpenAI ApiKey is not configured.");
        }

        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/chat/completions");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var body = new
            {
                model = settings.Model,
                messages = messages.Select(m => new { role = m.Role.ToLowerInvariant(), content = m.Content }).ToList(),
                temperature = settings.Temperature,
                max_tokens = settings.MaxTokens,
                top_p = settings.TopP,
                frequency_penalty = settings.FrequencyPenalty,
                presence_penalty = settings.PresencePenalty
            };

            request.Content = JsonContent.Create(body);

            var response = await _httpClient.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
            var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            var usage = json.GetProperty("usage");
            var totalTokens = usage.GetProperty("total_tokens").GetInt32();

            return new ChatResponseDto
            {
                Content = content,
                TokenCount = totalTokens
            };
        }, cancellationToken);
    }
}
