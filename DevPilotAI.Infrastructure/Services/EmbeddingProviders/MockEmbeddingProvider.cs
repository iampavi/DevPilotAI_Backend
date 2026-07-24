using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;

namespace DevPilotAI.Infrastructure.Services.EmbeddingProviders;

public class MockEmbeddingProvider : IEmbeddingProvider
{
    public string ProviderName => "Mock";

    public Task<float[]> GenerateEmbeddingAsync(string text, string model, CancellationToken cancellationToken = default)
    {
        int dimensions = model.Contains("small") || model.Contains("ada") ? 1536 : 384;
        var rand = new Random(text.GetHashCode());
        var vector = new float[dimensions];
        double sumSq = 0;
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] = (float)(rand.NextDouble() * 2 - 1);
            sumSq += vector[i] * vector[i];
        }
        
        // Normalize to unit length
        float length = (float)Math.Sqrt(sumSq);
        if (length > 0)
        {
            for (int i = 0; i < dimensions; i++)
            {
                vector[i] /= length;
            }
        }

        return Task.FromResult(vector);
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, string model, CancellationToken cancellationToken = default)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            results.Add(await GenerateEmbeddingAsync(text, model, cancellationToken));
        }
        return results;
    }
}
