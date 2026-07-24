using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IEmbeddingProvider
{
    string ProviderName { get; }
    Task<float[]> GenerateEmbeddingAsync(string text, string model, CancellationToken cancellationToken = default);
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, string model, CancellationToken cancellationToken = default);
}
