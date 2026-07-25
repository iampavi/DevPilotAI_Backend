namespace DevPilotAI.Application.Common.Interfaces;

public interface IChatProviderFactory
{
    IChatProvider GetProvider(string providerName);
}
