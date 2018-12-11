using Microsoft.VisualStudio.Services.WebApi;

namespace Agent.Plugins.TestResultParser.Plugin
{
    public interface IClientFactory
    {
        T GetClient<T>() where T : VssHttpClientBase;
    }

    public class ClientFactory : IClientFactory
    {
        public ClientFactory(VssConnection vssConnection)
        {
            _vssConnection = vssConnection;
        }

        public T GetClient<T>() where T : VssHttpClientBase
        {
            return _vssConnection.GetClient<T>();
        }

        private readonly VssConnection _vssConnection;
    }

}
