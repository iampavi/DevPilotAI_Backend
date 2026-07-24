using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IEmbeddingService
{
    string ConfiguredProvider { get; }
    string ConfiguredModel { get; }
    int Dimensions { get; }

    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);
}
