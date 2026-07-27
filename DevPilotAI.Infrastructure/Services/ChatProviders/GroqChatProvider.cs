using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DevPilotAI.Infrastructure.Services.ChatProviders;

public class GroqChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroqChatProvider> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public string ProviderName => "Groq";

    public GroqChatProvider(HttpClient httpClient, IConfiguration configuration, ILogger<GroqChatProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = configuration["ChatSettings:ApiKey"] ?? string.Empty;
        _baseUrl = configuration["ChatSettings:BaseUrl"] ?? "https://api.groq.com/openai/v1";

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
        // 1. Validate runtime configurations
        if (string.IsNullOrEmpty(settings.Provider))
        {
            throw new InvalidOperationException("Groq provider is not configured.");
        }
        if (string.IsNullOrEmpty(settings.Model))
        {
            throw new InvalidOperationException("Groq model is not configured.");
        }
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("Groq ApiKey is not configured.");
        }
        if (string.IsNullOrEmpty(_baseUrl))
        {
            throw new InvalidOperationException("Groq BaseUrl is not configured.");
        }

        var endpoint = $"{_baseUrl.TrimEnd('/')}/chat/completions";

        try
        {
            return await _resiliencePipeline.ExecuteAsync(async token =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
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

                var payloadString = JsonSerializer.Serialize(body);

                // 2. Log request details
                _logger.LogInformation("Groq URL: {Url}", endpoint);
                _logger.LogInformation("Groq Model: {Model}", settings.Model);
                _logger.LogInformation("Groq Temperature: {Temperature}", settings.Temperature);
                _logger.LogInformation("Groq MaxTokens: {MaxTokens}", settings.MaxTokens);
                _logger.LogInformation("Groq Request Payload: {Payload}", payloadString);

                request.Content = new StringContent(payloadString, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request, token);

                if (!response.IsSuccessStatusCode)
                {
                    // 3. Capture response body on failure
                    var errorBody = await response.Content.ReadAsStringAsync(token);
                    _logger.LogError("Failed to call Groq Chat API. Status: {Status}. Body: {Body}", response.StatusCode, errorBody);
                    throw new ChatProviderException("Groq", endpoint, response.StatusCode, errorBody);
                }

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
        catch (ChatProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Groq Chat API.");
            throw new Exception($"Failed to call Groq API: {ex.Message}", ex);
        }
    }
}
