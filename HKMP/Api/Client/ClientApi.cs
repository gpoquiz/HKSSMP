using Hkmp.Api.Client.Networking;
using Hkmp.Api.Eventing;
using Hkmp.Eventing;

namespace Hkmp.Api.Client;

/// <summary>
/// Client API interface implementation.
/// </summary>
internal class ClientApi : IClientApi {

    /// <inheritdoc/>
    public INetClient NetClient { get; }

    /// <inheritdoc/>
    public IEventAggregator EventAggregator { get; }

    public ClientApi(
        INetClient netClient
    ) {
        NetClient = netClient;
        EventAggregator = new EventAggregator();
    }
}
