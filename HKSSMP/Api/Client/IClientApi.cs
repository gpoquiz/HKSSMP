using Hkmp.Api.Client.Networking;
using Hkmp.Api.Command.Client;
using Hkmp.Api.Eventing;

namespace Hkmp.Api.Client;

/// <summary>
/// The client API.
/// </summary>
public interface IClientApi {
    /// <summary>
    /// The net client for all network-related interaction.
    /// </summary>
    INetClient NetClient { get; }
}
