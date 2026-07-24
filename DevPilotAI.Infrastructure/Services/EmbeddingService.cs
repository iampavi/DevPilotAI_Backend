using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevPilotAI.Infrastructure.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingProvider _provider;
    private readonly string _model;
    private readonly int _dimensions;

    public string ConfiguredProvider => _provider.ProviderName;
    public string ConfiguredModel => _model;
    public int Dimensions => _dimensions;

    public EmbeddingService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var providerType = configuration["EmbeddingSettings:Provider"] ?? "Mock";
        _model = configuration["EmbeddingSettings:Model"] ?? "text-embedding-3-small";
        
        if (!int.TryParse(configuration["EmbeddingSettings:Dimensions"], out _dimensions))
        {
            _dimensions = _model.Contains("small") || _model.Contains("ada") ? 1536 : 384;
        }

        var providers = serviceProvider.GetServices<IEmbeddingProvider>();
        _provider = providers.FirstOrDefault(p => p.ProviderName.Equals(providerType, StringComparison.OrdinalIgnoreCase))
                    ?? providers.FirstOrDefault(p => p.ProviderName == "Mock")
                    ?? throw new Exception("No embedding provider registered in the system.");
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        return await _provider.GenerateEmbeddingAsync(text, _model, cancellationToken);
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        return await _provider.GenerateEmbeddingsAsync(texts, _model, cancellationToken);
    }
}
