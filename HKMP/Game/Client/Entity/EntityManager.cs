using HarmonyLib;
using Hkmp.Networking.Client;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Logger = Hkmp.Logging.Logger;
using Vector2 = Hkmp.Math.Vector2;

namespace Hkmp.Game.Client.Entity;

internal static class EntityManager {

    private static readonly Dictionary<(EntityType, byte), IEntity> Entities = new();

    private static bool _isSceneHost;


    [HarmonyPatch(typeof(SceneManager), nameof(SceneManager.Internal_ActiveSceneChanged))]
    [HarmonyPostfix]
    public static void OnBecomeSceneHost() {
        Logger.Info("Releasing control of all registered entities");

        _isSceneHost = true;

        foreach (var entity in Entities.Values) {
            if (entity.IsControlled) {
                entity.ReleaseControl();
            }

            entity.AllowEventSending = true;
        }
    }

    public static void OnBecomeSceneClient() {
        Logger.Info("Taking control of all registered entities");

        _isSceneHost = false;

        foreach (var entity in Entities.Values) {
            if (!entity.IsControlled) {
                entity.TakeControl();
            }

            entity.AllowEventSending = false;
        }
    }

    private static void OnSceneChanged(Scene oldScene, Scene newScene) {
        Logger.Info("Clearing all registered entities");

        foreach (var entity in Entities.Values) {
            entity.Destroy();
        }

        Entities.Clear();
    }

    public static void UpdateEntityPosition(EntityType entityType, byte id, Vector2 position) {
        if (!Entities.TryGetValue((entityType, id), out var entity)) {
            Logger.Info(
                $"Tried to update entity position for (type, ID) = ({entityType}, {id}), but there was no entry");
            return;
        }

        // Check whether the entity is already controlled, and if not
        // take control of it
        if (!entity.IsControlled) {
            entity.TakeControl();
        }

        entity.UpdatePosition(position);
    }

    public static void UpdateEntityState(EntityType entityType, byte id, byte stateIndex, List<byte> variables) {
        if (!Entities.TryGetValue((entityType, id), out var entity)) {
            Logger.Info(
                $"Tried to update entity state for (type, ID) = ({entityType}, {id}), but there was no entry");
            return;
        }

        // Check whether the entity is already controlled, and if not
        // take control of it
        if (!entity.IsControlled) {
            entity.TakeControl();
        }

        // Simply update the state with this new index
        entity.UpdateState(stateIndex, variables);
    }
}
