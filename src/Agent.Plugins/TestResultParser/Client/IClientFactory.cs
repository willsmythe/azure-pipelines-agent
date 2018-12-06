using Microsoft.VisualStudio.Services.WebApi;

namespace Agent.Plugins.TestResultParser.Client
{
    public interface IClientFactory
    {
        T GetClient<T>() where T : VssHttpClientBase;
    }
}
