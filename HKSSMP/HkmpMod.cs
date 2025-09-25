using BepInEx;
using HarmonyLib;
using Hkmp.Game.Settings;
using Hkmp.Logging;
using Hkmp.Util;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp;

/// <summary>
/// Mod class for the HKMP mod.
/// </summary>
[BepInPlugin("cc341f8b-2427-4311-894a-b81804d841f8", "HKSSMP", "0.0.0")]
public class HkmpMod : BaseUnityPlugin {

    private void Awake() {
        // Put your initialization logic here
        Logger.LogInfo($"ItemChanger has loaded!");
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Harmony.CreateAndPatchAll(Assembly.GetAssembly(typeof(tk2dSpriteAnimator)));
    }
    /// <summary>
    /// Dictionary containing preloaded objects by scene name and object path.
    /// </summary>
    public static Dictionary<string, Dictionary<string, GameObject>> PreloadedObjects;
    
    /// <summary>
    /// Statically create Settings object, so it can be accessed early.
    /// </summary>
    private ModSettings ModSettings = new ModSettings();
}
