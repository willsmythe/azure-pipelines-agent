using Microsoft.VisualStudio.Services.WebApi;

namespace Agent.Plugins.TestResultParser.Client
{
    public class ClientFactory : IClientFactory
    {
        public ClientFactory(VssConnection vssConnection)
        {
            _vssConnection = vssConnection;
        }

        public virtual T GetClient<T>() where T : VssHttpClientBase
        {
            return _vssConnection.GetClient<T>();
        }

        private readonly VssConnection _vssConnection;
    }
}
