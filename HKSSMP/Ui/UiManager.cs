using GlobalEnums;
using HarmonyLib;
using Hkmp.Api.Client;
using Hkmp.Game.Settings;
using Hkmp.Networking.Client;
using Hkmp.Ui.Chat;
using Hkmp.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static CutsceneHelper;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Ui;

/// <inheritdoc />
internal static class UiManager {
    private static ComponentGroup _inGameGroup;
    private static EventSystem _eventSystem;
    private static ComponentGroup _pauseMenuGroup;

    #region Internal UI manager variables and properties

    /// <summary>
    /// The font size of header text.
    /// </summary>
    public const int HeaderFontSize = 34;

    /// <summary>
    /// The font size of normal text.
    /// </summary>
    public const int NormalFontSize = 24;

    /// <summary>
    /// The font size of the chat text.
    /// </summary>
    public const int ChatFontSize = 22;

    /// <summary>
    /// The font size of sub text.
    /// </summary>
    public const int SubTextFontSize = 22;

    /// <summary>
    /// The global GameObject in which all UI is created.
    /// </summary>
    internal static GameObject UiGameObject;

    /// <summary>
    /// The chat box instance.
    /// </summary>
    internal static ChatBox InternalChatBox;

    /// <summary>
    /// The connect interface.
    /// </summary>
    public static ConnectInterface ConnectInterface;

    /// <summary>
    /// The client settings interface.
    /// </summary>
    public static ClientSettingsInterface SettingsInterface;

    /// <summary>
    /// The mod settings.
    /// </summary>
    private static ModSettings _modSettings;

    /// <summary>
    /// The ping interface.
    /// </summary>
    private static PingInterface _pingInterface;

    /// <summary>
    /// Whether the UI is hidden by the key-bind.
    /// </summary>
    private static bool _isUiHiddenByKeyBind;

    /// <summary>
    /// Whether the game is in a state where we normally show the pause menu UI for example in a gameplay
    /// scene in the HK pause menu.
    /// </summary>
    private static bool _canShowPauseUi;

    #endregion

    #region IUiManager properties

    /// <inheritdoc />
    public static IChatBox ChatBox => InternalChatBox;

    #endregion

    public static void Initialize(
        ServerSettings clientServerSettings,
        ModSettings modSettings,
        NetClient netClient
    ) {
        _modSettings = modSettings;

        // First we create a gameObject that will hold all other objects of the UI
        UiGameObject = new GameObject();

        // Create event system object
        var eventSystemObj = new GameObject("EventSystem");

        _eventSystem = eventSystemObj.AddComponent<EventSystem>();
        _eventSystem.sendNavigationEvents = true;
        _eventSystem.pixelDragThreshold = 10;

        eventSystemObj.AddComponent<StandaloneInputModule>();

        Object.DontDestroyOnLoad(eventSystemObj);

        // Make sure that our UI is an overlay on the screen
        UiGameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        // Also scale the UI with the screen size
        var canvasScaler = UiGameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);

        UiGameObject.AddComponent<GraphicRaycaster>();

        Object.DontDestroyOnLoad(UiGameObject);

        var uiGroup = new ComponentGroup();

        _pauseMenuGroup = new ComponentGroup(false, uiGroup);

        var connectGroup = new ComponentGroup(parent: _pauseMenuGroup);

        var settingsGroup = new ComponentGroup(parent: _pauseMenuGroup);

        ConnectInterface = new ConnectInterface(
            modSettings,
            connectGroup,
            settingsGroup
        );

        _inGameGroup = new ComponentGroup(parent: uiGroup);

        var infoBoxGroup = new ComponentGroup(parent: _inGameGroup);

        InternalChatBox = new ChatBox(infoBoxGroup, modSettings);

        var pingGroup = new ComponentGroup(parent: _inGameGroup);

        _pingInterface = new PingInterface(
            pingGroup,
            modSettings,
            netClient
        );

        SettingsInterface = new ClientSettingsInterface(
            modSettings,
            clientServerSettings,
            settingsGroup,
            connectGroup,
            _pingInterface
        );

        // The game is automatically unpaused when the knight dies, so we need
        // to disable the UI menu manually
        // TODO: this still gives issues, since it displays the cursor while we are supposed to be unpaused

        MonoBehaviourUtil.Instance.OnUpdateEvent += () => { CheckKeyBinds(uiGroup); };
    }

    #region Internal UI manager methods

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.OnDeath))]
    [HarmonyPostfix]
    private static void PostfixOnDeath(Scene previousActiveScene, Scene newActiveScene) {
        _pauseMenuGroup.SetActive(false);
    }

    [HarmonyPatch(typeof(SceneManager), nameof(SceneManager.Internal_ActiveSceneChanged))]
    [HarmonyPostfix]
    private static void PostfixSetState(Scene previousActiveScene, Scene newActiveScene) {
        if (SceneUtil.IsNonGameplayScene(newActiveScene.name)) {
            _eventSystem.enabled = false;

            _canShowPauseUi = false;

            _pauseMenuGroup.SetActive(false);
            _inGameGroup.SetActive(false);
        } else {
            _eventSystem.enabled = true;

            _inGameGroup.SetActive(true);
        }
    }

    [HarmonyPatch(typeof(UIManager), nameof(UIManager.SetState))]
    private static void PostfixSetState(UIState newState) {
        {
            if (newState == UIState.PAUSED) {
                // Only show UI in gameplay scenes
                if (!SceneUtil.IsNonGameplayScene(SceneUtil.GetCurrentSceneName())) {
                    _canShowPauseUi = true;

                    _pauseMenuGroup.SetActive(!_isUiHiddenByKeyBind);
                }

                _inGameGroup.SetActive(false);
            } else {
                _pauseMenuGroup.SetActive(false);

                _canShowPauseUi = false;

                // Only show chat box UI in gameplay scenes
                if (!SceneUtil.IsNonGameplayScene(SceneUtil.GetCurrentSceneName())) {
                    _inGameGroup.SetActive(true);
                }
            }
        }
    }

    /// <summary>
    /// Callback method for when the client successfully connects.
    /// </summary>
    public static void OnSuccessfulConnect() {
        ConnectInterface.OnSuccessfulConnect();
        _pingInterface.SetEnabled(true);
        SettingsInterface.OnSuccessfulConnect();
    }

    /// <summary>
    /// Callback method for when the client fails to connect.
    /// </summary>
    /// <param name="result">The result of the failed connection.</param>
    public static void OnFailedConnect(ConnectFailedResult result) {
        ConnectInterface.OnFailedConnect(result);
    }

    /// <summary>
    /// Callback method for when the client disconnects.
    /// </summary>
    public static void OnClientDisconnect() {
        ConnectInterface.OnClientDisconnect();
        _pingInterface.SetEnabled(false);
        SettingsInterface.OnDisconnect();
    }

    /// <summary>
    /// Callback method for when the team setting in the <see cref="ServerSettings"/> changes.
    /// </summary>
    public static void OnTeamSettingChange() {
        SettingsInterface.OnTeamSettingChange();
    }

    /// <summary>
    /// Check key-binds to show/hide the UI.
    /// </summary>
    /// <param name="uiGroup">The component group for the entire UI.</param>
    private static void CheckKeyBinds(ComponentGroup uiGroup) {
        if (Input.GetKeyDown((KeyCode) _modSettings.HideUiKey)) {
            // Only allow UI toggling within the pause menu, otherwise the chat input might interfere
            if (_canShowPauseUi) {
                _isUiHiddenByKeyBind = !_isUiHiddenByKeyBind;

                Logger.Debug($"UI is now {(_isUiHiddenByKeyBind ? "hidden" : "shown")}");

                uiGroup.SetActive(!_isUiHiddenByKeyBind);
            }
        }
    }

    #endregion

    #region IUiManager methods

    /// <inheritdoc />
    public static void DisableTeamSelection() {
        SettingsInterface.OnAddonSetTeamSelection(false);
    }

    /// <inheritdoc />
    public static void EnableTeamSelection() {
        SettingsInterface.OnAddonSetTeamSelection(true);
    }

    /// <inheritdoc />
    public static void DisableSkinSelection() {
        SettingsInterface.OnAddonSetSkinSelection(false);
    }

    /// <inheritdoc />
    public static void EnableSkinSelection() {
        SettingsInterface.OnAddonSetSkinSelection(true);
    }

    #endregion
}
