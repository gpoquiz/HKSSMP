using BepInEx.Bootstrap;
using HarmonyLib;
using Hkmp.Game.Command.Server;
using Hkmp.Game.Settings;
using Hkmp.Networking.Packet;
using Hkmp.Networking.Server;
using Hkmp.Ui;

namespace Hkmp.Game.Server;

/// <summary>
/// Specialization of <see cref="ServerManager"/> that adds handlers for the mod specific things.
/// </summary>
internal class ModServerManager : ServerManager {

    private static ServerManager _currentServer;
    public ModServerManager(
        NetServer netServer,
        ServerSettings serverSettings,
        PacketManager packetManager
    ) : base(netServer, serverSettings, packetManager) {
        _currentServer = this;
        // Start addon loading once all mods have finished loading

        // Register handlers for UI events
        UiManager.ConnectInterface.StartHostButtonPressed += Start;
        UiManager.ConnectInterface.StopHostButtonPressed += Stop;

    }

    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Start))]
    [HarmonyFinalizer]
    private static void FinalizeChainloader() {
        if (_currentServer is ModServerManager modServer)
            modServer.AddonManager.LoadAddons();
    }
    [HarmonyPatch(typeof(global::GameManager), nameof(global::GameManager.OnApplicationQuit))]
    [HarmonyPostfix]
    private static void PostfixQuit() {
        _currentServer.Stop();
    }
    /// <inheritdoc />
    protected override void RegisterCommands() {
        base.RegisterCommands();

        CommandManager.RegisterCommand(new SettingsCommand(this, InternalServerSettings));
    }
}
