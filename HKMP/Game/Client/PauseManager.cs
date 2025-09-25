using System;
using GlobalEnums;
using HarmonyLib;
using Hkmp.Networking.Client;
using UnityEngine;

namespace Hkmp.Game.Client;

/// <summary>
/// Handles pause related things to prevent player being invincible in pause menu while connected to a server.
/// </summary>
internal static class PauseManager {
    
    /// <inheritdoc />
    public static event Action<float> SetTimeScaleEvent;
    /// <summary>
    /// The net client instance.
    /// </summary>
    private static NetClient _netClient;

    public static void Initialize(NetClient netClient) {
        _netClient = netClient;
    }
    /// <summary>
    /// Callback method for the UIManager#TogglePauseGame method.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="__instance">The UIManager instance.</param>
    [HarmonyPatch(typeof(UIManager), nameof(UIManager.TogglePauseGame))]
    [HarmonyPrefix]
    private static void PrefixTogglePauseGame(UIManager __instance, out bool __state) {
        __state = false;
        if (!_netClient.IsConnected) {
            return;
        }

        // First evaluate whether the original method would have started the coroutine:
        // GameManager#PauseGameToggleByMenu
        __state = __instance.ignoreUnpause;
    }

    [HarmonyPatch(typeof(UIManager), nameof(UIManager.TogglePauseGame))]
    [HarmonyPostfix]
    private static void PostfixTogglePauseGame(UIManager __instance, bool __state) {
        // If we evaluated that the coroutine was started, we can now reset the timescale back to 1 again
        if (__state) {
            SetTimeScale(1f);
        }
    }

    /// <summary>
    /// Callback method for the InputHandler#Update method.
    /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="__instance">The InputHandler instance.</param>
        [HarmonyPatch(typeof(InputHandler), nameof(InputHandler.Update))]
    [HarmonyPrefix]
    private static void PrefixOnUpdate(InputHandler __instance, out bool __state) {

        // First evaluate whether the original method would have started the coroutine:
        // GameManager#PauseGameToggleByMenu
        __state = false;
        if (!_netClient.IsConnected || 
            !__instance.acceptingInput ||
            !__instance.inputActions.Pause.WasPressed ||
            !__instance.PauseAllowed ||
            PlayerData.instance.GetBool(nameof(PlayerData.disablePause))) return;

        var state = global::GameManager.instance.GameState;
        if (state is GameState.PLAYING or GameState.PAUSED) {
            __state = true;
        }
    }

    [HarmonyPatch(typeof(InputHandler), nameof(InputHandler.Update))]
    [HarmonyPostfix]
    private static void PostfixOnUpdate(bool __state) {
        if (__state)
            SetTimeScale(1f);
    }

    /// <summary>
    /// Callback method for when the player dies.
    /// If we are paused while the player dies, the game enters a state where the cursor is visible
    /// while not in the pause menu, but not being able to give any input apart from opening the pause menu.
    /// Therefore, we unpause immediately before dying to prevent this.
    /// </summary>
    /// 
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.OnDeath))]
    [HarmonyPrefix]
    private static void PrefixOnDeath(HeroController __instance) {
        ImmediateUnpauseIfPaused(__instance.gm);
    }

    /// <summary>
    /// Callback method for the HeroController#DieFromHazard method.
    /// If we have a hazard respawn while in the pause menu it soft-locks the menu, so we unpause it first.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="__instance">The HeroController instance.</param>
    /// <param name="hazardType">The type of hazard the player dies from.</param>
    /// <param name="angle">The angle of entering the hazard.</param>
    /// <returns>An enumerator for the coroutine.</returns>
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.DieFromHazard))]
    [HarmonyPrefix]
    private static void PrefixDieFromHazard(HeroController __instance) {
        ImmediateUnpauseIfPaused(__instance.gm);
    }

    /// <summary>
    /// Callback method for the TransitionPoint#OnTriggerEnter2D method.
    /// If we go through a transition while being paused, we can only let the transition occur if we
    /// unpause first and then let the original method continue.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="__instance">The TransitionPoint instance.</param>
    /// <param name="obj">The collider that enters the trigger.</param>
    [HarmonyPatch(typeof(TransitionPoint), nameof(TransitionPoint.OnTriggerEnter2D))]
    [HarmonyPrefix]
    private static void PrefixOnTriggerEnter2D(
        TransitionPoint __instance,
        Collider2D obj
    ) {
        // Skip this if the transition point is a door, since it isn't a enter-and-teleport transition,
        // but requires input to transition, so it can't happen in the pause menu
        if (!__instance.isADoor) {
            ImmediateUnpauseIfPaused(__instance.gm);
        }
    }

    /// <summary>
    /// Callback method for the HeroController#OnPause method.
    /// If we don't reset the input of the hero when pausing, we might continue sliding across the floor
    /// due to the timescale not being set to 0.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="__instance">The HeroController instance.</param>
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.Pause))]
    [HarmonyPostfix]
    private static void PostfixPause(HeroController __instance) {
        if (!_netClient.IsConnected) {
            return;
        }

        __instance.ResetInput();
    }

    /// <summary>
    /// Unpauses the game immediately if it was paused.
    /// </summary>
    private static void ImmediateUnpauseIfPaused(global::GameManager gm) {
        if (UIManager.instance != null) {
            if (UIManager.instance.uiState.Equals(UIState.PAUSED)) {

                gm.gameCams.ResumeCameraShake();
                gm.inputHandler.PreventPause();
                gm.actorSnapshotUnpaused.TransitionTo(0f);
                gm.isPaused = false;
                gm.ui.AudioGoToGameplay(0.2f);
                gm.ui.SetState(UIState.PLAYING);
                gm.SetState(GameState.PLAYING);
                if (HeroController.instance != null) {
                    HeroController.instance.UnPause();
                }

                MenuButtonList.ClearAllLastSelected();
                gm.inputHandler.AllowPause();
            }
        }
    }

    /// <summary>
    /// Sets the time scale similarly to the method GameManager#SetTimeScale.
    /// </summary>
    /// <param name="timeScale">The new time scale.</param>
    public static void SetTimeScale(float timeScale) {
        timeScale = timeScale > 0.00999999977648258 ? timeScale : 0.0f;
        TimeManager.TimeScale = timeScale;
        SetTimeScaleEvent?.Invoke(timeScale);
    }
}
