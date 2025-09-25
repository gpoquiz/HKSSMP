using HarmonyLib;
using System.Collections.Generic;
using Hkmp.Api.Client;
using Hkmp.Game.Settings;
using Hkmp.Networking.Client;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;
using Vector2 = Hkmp.Math.Vector2;
using Vector3 = Hkmp.Math.Vector3;

namespace Hkmp.Game.Client;

/// <summary>
/// A class that manages player locations on the in-game map.
/// </summary>
internal static class MapManager {
    /// <summary>
    /// The net client instance.
    /// </summary>
    private static NetClient _netClient;

    /// <summary>
    /// The current server settings.
    /// </summary>
    private static ServerSettings _serverSettings;

    /// <summary>
    /// Dictionary containing map icon objects per player ID.
    /// </summary>
    private static readonly Dictionary<ushort, PlayerMapEntry> _mapEntries;

    /// <summary>
    /// The last sent map position.
    /// </summary>
    private static Vector2 _lastPosition;

    /// <summary>
    /// The value of the last sent whether the map icon was active. If true, we have sent to the server
    /// that we have a map icon active. Otherwise, we have sent to the server that we don't have a map
    /// icon active.
    /// </summary>
    private static bool _lastSentMapIcon;

    /// <summary>
    /// Whether we should display the map icons. True if the map is opened, false otherwise.
    /// </summary>
    private static bool _displayingIcons;

    public static void Initialize(NetClient netClient, ServerSettings serverSettings) {
        _netClient = netClient;
        _serverSettings = serverSettings;


        _netClient.DisconnectEvent += OnDisconnect;

    }

    // Copied from source:
    public static Vector2 GetMapPosition(GameMap map,
        UnityEngine.Vector2 positionInScene,
        GameMapScene scene,
        UnityEngine.GameObject sceneObj,
        UnityEngine.Vector2 scenePos,
        UnityEngine.Vector2 sceneSize) {
        if (sceneObj == null)
            return new(-1000f, -1000f);
        if (!(bool) (scene) || !(bool) scene.BoundsSprite)
            return (Math.Vector2)scenePos;

        var vector2 = scene.BoundsSprite.bounds.size * (UnityEngine.Vector2) scene.transform.localScale;
        var localScale = map.transform.localScale;
        return new(
            (float) (
                scenePos.x - 
                vector2.x / 2.0 + 
                positionInScene.x / 
                (double) sceneSize.x *
                (vector2.x * (double) localScale.x) /
                localScale.x)
            ,
            (float) (
                scenePos.y - 
                vector2.y / 2.0 + 
                positionInScene.y / 
                (double) sceneSize.y *
                (vector2.y * (double) localScale.y) / 
                localScale.y));
    }
    /// <summary>
    /// Callback method for the HeroController#Update method.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="__instance">The HeroController instance.</param>
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.Update))]
    private static void PostfixUpdate(HeroController __instance) {

        _currentMap = __instance.gm.gameMap;
        // If we are not connect, we don't have to send anything
        if (!_netClient.IsConnected) {
            return;
        }

        var map = __instance.gm.gameMap;
        // Check whether the player has a map location for an icon
        var newPosition = GetMapPosition(__instance.gm.gameMap, __instance.transform.position, map.currentScene, map.currentSceneObj, map.currentScenePos, map.currentSceneSize);
        // Whether we have a map icon active
        var hasMapIcon = newPosition != Vector2.Zero;
        if (!_serverSettings.AlwaysShowMapIcons) {
            if (!_serverSettings.OnlyBroadcastMapIconWithWaywardCompass) {
                hasMapIcon = false;
            } else {
                // We do not always show map icons, but only when we are wearing wayward compass
                // So we need to check whether we are wearing wayward compass
                if (!__instance.gm.gameMap.displayingCompass) {
                    hasMapIcon = false;
                }
            }
        }

        if (hasMapIcon != _lastSentMapIcon) {
            _lastSentMapIcon = hasMapIcon;

            _netClient.UpdateManager.UpdatePlayerMapIcon(hasMapIcon);

            // If we don't have a map icon anymore, we reset the last position so that
            // if we have an icon again, we will immediately also send a map position update
            if (!hasMapIcon) {
                _lastPosition = Vector2.Zero;
            }
        }

        // If we don't currently have a map icon active or if we are in a scene transition,
        // we don't send map position updates
        if (!hasMapIcon || global::GameManager._instance.IsInSceneTransition) {
            return;
        }

        // Only send update if the position changed
        if (newPosition != _lastPosition) {
            var vec2 = new Vector2(newPosition.X, newPosition.Y);

            _netClient.UpdateManager.UpdatePlayerMapPosition(vec2);

            // Update the last position, since it changed
            _lastPosition = newPosition;
        }
    }


    /// <summary>
    /// Update whether the given player has an active map icon.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    /// <param name="hasMapIcon">Whether the player has an active map icon.</param>
    public static void UpdatePlayerHasIcon(ushort id, bool hasMapIcon) {
        // If there does not exist an entry for this ID yet, we create it
        if (!_mapEntries.TryGetValue(id, out var mapEntry)) {
            _mapEntries[id] = mapEntry = new PlayerMapEntry();
        }

        if (mapEntry.HasMapIcon) {
            if (!hasMapIcon) {
                // If the player had an active map icon, but we receive that they do not anymore
                // we destroy the map icon object if it exists
                if (mapEntry.GameObject != null) {
                    Object.Destroy(mapEntry.GameObject);
                }
            }
        } else {
            if (hasMapIcon) {
                // If the player did not have an active map icon, but we receive that they do we
                // create an icon
                CreatePlayerIcon(id, mapEntry.Position);
            }
        }

        mapEntry.HasMapIcon = hasMapIcon;
    }

    /// <summary>
    /// Update the map icon of a given player with the given position.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    /// <param name="position">The new position on the map.</param>
    public static void UpdatePlayerIcon(ushort id, Vector2 position) {
        // If there does not exist an entry for this id yet, we create it
        if (!_mapEntries.TryGetValue(id, out var mapEntry)) {
            _mapEntries[id] = mapEntry = new PlayerMapEntry();
        }

        // Always store the position in case we later get an active map icon without position
        mapEntry.Position = position;

        // If the player does not have an active map icon
        if (!mapEntry.HasMapIcon) {
            return;
        }

        // Check whether the object still exists
        var mapObject = mapEntry.GameObject;
        if (mapObject == null) {
            CreatePlayerIcon(id, position);
            return;
        }

        // Check if the transform is still valid and otherwise destroy the object
        // This is possible since whenever we receive a new update packet, we
        // will just create a new map icon
        var transform = mapObject.transform;
        if (transform == null) {
            Object.Destroy(mapObject);
            return;
        }

        var unityPosition = new UnityEngine.Vector3(
            position.X,
            position.Y,
            transform.localPosition.z
        );

        // Update the position of the player icon
        transform.localPosition = unityPosition;
    }

    /// <summary>
    /// Callback method on the GameMap#CloseQuickMap method.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="__instance">The GameMap instance.</param>
    [HarmonyPatch(typeof(GameMap), nameof(GameMap.CloseQuickMap))]
    [HarmonyPostfix]
    private static void PostfixCloseQuickMap(GameMap __instance) {
        // We have closed the map, so we can disable the icons
        _displayingIcons = false;
        UpdateMapIconsActive();
    }

    /// <summary>
    /// Callback method on the GameMap#TryOpenQuickMap method.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="__instance">The GameMap instance.</param>
    /// <param name="posShade">The boolean value whether to position the shade.</param>
    [HarmonyPatch(typeof(GameMap), nameof(GameMap.TryOpenQuickMap))]
    [HarmonyPostfix]
    private static void PostfixTryOpenQuickMap(bool __result) {
        if (!__result)
            return;
        _displayingIcons = true;
        UpdateMapIconsActive();
    }

    /// <summary>
    /// Update all existing map icons based on whether they should be active according to server settings.
    /// </summary>
    private static void UpdateMapIconsActive() {
        foreach (var mapEntry in _mapEntries.Values) {
            if (mapEntry.HasMapIcon && mapEntry.GameObject != null) {
                mapEntry.GameObject.SetActive(_displayingIcons);
            }
        }
    }

    private static GameMap _currentMap;
    /// <summary>
    /// Create a map icon for a player and store it in the mapping.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    /// <param name="position">The position of the map icon.</param>
    private static void CreatePlayerIcon(ushort id, Vector2 position) {
        if (!_mapEntries.TryGetValue(id, out var mapEntry)) {
            return;
        }

        if (_currentMap == null) {
            return;
        }

        var compassIconPrefab = _currentMap.compassIcon;
        if (compassIconPrefab == null) {
            Logger.Warn("CompassIcon prefab is null");
            return;
        }

        // Create a new player icon relative to the game map
        var mapIcon = Object.Instantiate(
            compassIconPrefab,
            _currentMap.gameObject.transform
        );
        mapIcon.SetActive(_displayingIcons);

        var unityPosition = new UnityEngine.Vector3(
            position.X,
            position.Y,
            compassIconPrefab.transform.localPosition.z
        );

        // Set the position of the player icon
        mapIcon.transform.localPosition = unityPosition;

        // Remove the bob effect when walking with the map
        Object.Destroy(mapIcon.LocateMyFSM("Mapwalk Bob"));

        // Put it in the list
        mapEntry.GameObject = mapIcon;
    }

    /// <summary>
    /// Remove a map entry for a player. For example, if they disconnect from the server.
    /// </summary>
    /// <param name="id">The ID of the player.</param>
    public static void RemoveEntryForPlayer(ushort id) {
        if (_mapEntries.TryGetValue(id, out var mapEntry)) {
            if (mapEntry.GameObject != null) {
                Object.Destroy(mapEntry.GameObject);
            }

            _mapEntries.Remove(id);
        }
    }

    /// <summary>
    /// Remove all map icons.
    /// </summary>
    public static void RemoveAllIcons() {
        // Destroy all existing map icons
        foreach (var mapEntry in _mapEntries.Values) {
            if (mapEntry.GameObject != null) {
                Object.Destroy(mapEntry.GameObject);
            }
        }
    }

    /// <summary>
    /// Callback method for when the local user disconnects.
    /// </summary>
    private static void OnDisconnect() {
        RemoveAllIcons();

        _mapEntries.Clear();

        // Reset variables to their initial values
        _lastPosition = Vector2.Zero;
        _lastSentMapIcon = false;
    }


    /// <inheritdoc />
    public static bool TryGetEntry(ushort id, out IPlayerMapEntry playerMapEntry) {
        var found = _mapEntries.TryGetValue(id, out var entry);
        playerMapEntry = entry;

        return found;
    }

    /// <summary>
    /// An entry for an icon of a player.
    /// </summary>
    private class PlayerMapEntry : IPlayerMapEntry {
        /// <inheritdoc />
        public bool HasMapIcon { get; set; }

        /// <inheritdoc />
        public Math.Vector2 Position { get; set; } = Math.Vector2.Zero;

        /// <summary>
        /// The game object corresponding to the map icon.
        /// </summary>
        public GameObject GameObject { get; set; }
    }
}
