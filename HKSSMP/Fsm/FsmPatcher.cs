using HarmonyLib;
using Hkmp.Util;
using HutongGames.PlayMaker.Actions;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Fsm;

/// <summary>
/// Class for patching functionality of PlayMaker FSMs.
/// </summary>
internal class FsmPatcher {
    /// <summary>
    /// Callback method for the PlayMakerFSM#OnEnable hook.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="__instance">The PlayMakerFSM instance that the hooked method was called on.</param>
    [HarmonyPatch(typeof(PlayMakerFSM), nameof(PlayMakerFSM.OnEnable))]
    [HarmonyPostfix]
    private void PostfixOnEnable(PlayMakerFSM __instance) {
        // Check if it is a FSM for picking up shiny items
        if (__instance.name.Equals("Inspect Region") && __instance.Fsm.Name.Equals("inspect")) {
            // Find the action that checks whether the player enters the pickup area
            var triggerAction = __instance.GetFirstAction<Trigger2dEvent>("Out Of Range");
            if (triggerAction == null) {
                Logger.Warn("Could not patch inspect FSM, because trigger action does not exist");
                return;
            }

            // Add a collide tag to the action to ensure it only triggers on the local player
            triggerAction.collideTag.Value = "Player";
            triggerAction.collideTag.UseVariable = false;
        }
    }
}
