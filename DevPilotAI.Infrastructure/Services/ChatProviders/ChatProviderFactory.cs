using System;
using System.Collections.Generic;
using System.Linq;
using DevPilotAI.Application.Common.Interfaces;

namespace DevPilotAI.Infrastructure.Services.ChatProviders;

public class ChatProviderFactory : IChatProviderFactory
{
    private readonly IEnumerable<IChatProvider> _providers;

    public ChatProviderFactory(IEnumerable<IChatProvider> providers)
    {
        _providers = providers;
    }

    public IChatProvider GetProvider(string providerName)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
        {
            throw new ArgumentException($"Chat provider '{providerName}' is not registered or supported.", nameof(providerName));
        }
        return provider;
    }
}
