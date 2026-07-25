using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Chat;

namespace DevPilotAI.Infrastructure.Services.ChatProviders;

public class MockChatProvider : IChatProvider
{
    public string ProviderName => "Mock";

    public async IAsyncEnumerable<string> StreamResponseAsync(
        List<ChatMessageDto> messages,
        ChatSettingsDto settings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var responseText = GetMockResponseText(messages);
        var words = responseText.Split(' ');
        var random = new Random();

        foreach (var word in words)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return word + " ";
            await Task.Delay(random.Next(20, 60), cancellationToken);
        }
    }

    public Task<ChatResponseDto> GetResponseAsync(
        List<ChatMessageDto> messages,
        ChatSettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        var content = GetMockResponseText(messages);
        return Task.FromResult(new ChatResponseDto
        {
            Content = content,
            TokenCount = content.Length / 4
        });
    }

    private string GetMockResponseText(List<ChatMessageDto> messages)
    {
        var lastMessage = messages.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content.ToLowerInvariant() ?? string.Empty;

        if (lastMessage.Contains("explain"))
        {
            return "Based on the provided code context, this class orchestrates orders processing. It features validation triggers, logging, and database operations. The main flow validates the items, calls the database to save, and publishes events.";
        }
        if (lastMessage.Contains("test"))
        {
            return "Here are the suggested unit tests for this class:\n\n```csharp\n[Fact]\npublic async Task ProcessOrder_ShouldSucceed_WhenRequestIsValid()\n{\n    // Assert\n}\n```";
        }
        if (lastMessage.Contains("refactor"))
        {
            return "I suggest the following refactoring improvements:\n1. Extract validation logic into a separate validator class.\n2. Use constructor injection for dependencies instead of creating them inline.\n3. Add concurrency checks on critical updates.";
        }

        return "This is a mock RAG assistant response simulating chat generation with context code chunks.";
    }
}
