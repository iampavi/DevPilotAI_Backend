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

public class OllamaChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaChatProvider> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly string _baseUrl;

    public string ProviderName => "Ollama";

    public OllamaChatProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaChatProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _baseUrl = configuration["EmbeddingSettings:BaseUrl"] ?? "http://localhost:11434/";

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
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/api/chat");
            var body = new
            {
                model = settings.Model,
                messages = messages.Select(m => new { role = m.Role.ToLowerInvariant(), content = m.Content }).ToList(),
                options = new
                {
                    temperature = settings.Temperature,
                    num_predict = settings.MaxTokens,
                    top_p = settings.TopP
                },
                stream = false
            };

            request.Content = JsonContent.Create(body);

            var response = await _httpClient.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
            var content = json.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            return new ChatResponseDto
            {
                Content = content,
                TokenCount = content.Length / 4
            };
        }, cancellationToken);
    }
}
