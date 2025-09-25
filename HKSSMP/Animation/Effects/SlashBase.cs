using System.Collections.Generic;
using Hkmp.Util;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hkmp.Animation.Effects;

/// <summary>
/// Abstract base class for the animation effect of nail slashes.
/// </summary>
internal abstract class SlashBase : ParryableEffect {
    /// <summary>
    /// Base X and Y scales for the various slash types.
    /// </summary>
    private static readonly Dictionary<SlashType, Vector2> _baseScales = new() {
        { SlashType.Normal, new Vector2(1.6011f, 1.6452f) },
        { SlashType.Alt, new Vector2(1.257f, 1.4224f) },
        { SlashType.Down, new Vector2(1.125f, 1.28f) },
        { SlashType.Up, new Vector2(1.15f, 1.4f) },
        { SlashType.Wall, new Vector2(1.62f, 1.6452f) }
    };
    
    /// <inheritdoc/>
    public abstract override void Play(GameObject playerObject, bool[] effectInfo);

    /// <inheritdoc/>
    public override bool[] GetEffectInfo() {
        var playerData = PlayerData.instance;

        return new[] {
            playerData.GetInt(nameof(PlayerData.health)) == 1,
            playerData.GetInt(nameof(PlayerData.health)) == playerData.GetInt(nameof(PlayerData.maxHealth)),
        };
    }

    /// <summary>
    /// Plays the slash animation for the given player.
    /// </summary>
    /// <param name="playerObject">The GameObject representing the player.</param>
    /// <param name="effectInfo">A boolean array containing effect info.</param>
    /// <param name="slash"></param>
    protected void Play(GameObject playerObject, bool[] effectInfo, NailSlash slash) {
        slash.StartSlash();
    }

    /// <summary>
    /// Plays the slash animation for the given player.
    /// </summary>
    /// <param name="playerObject">The GameObject representing the player.</param>
    /// <param name="effectInfo">A boolean array containing effect info.</param>
    /// <param name="prefab">The nail slash prefab object.</param>
    /// <param name="type">The type of nail slash.</param>
    protected void Play(GameObject playerObject, bool[] effectInfo, GameObject prefab, SlashType type) {
        // Read all needed information to do this effect from the packet
        var isOnOneHealth = effectInfo[0];
        var isOnFullHealth = effectInfo[1];

        // Get the attacks gameObject from the player object
        var playerAttacks = playerObject.FindGameObjectInChildren("Attacks");

        // Instantiate the slash gameObject from the given prefab
        // and use the attack gameObject as transform reference
        var slash = Object.Instantiate(prefab, playerAttacks.transform);
        slash.layer = 22;
        
        // Set the base scale of the slash based on the slash type, this prevents remote nail slashes to occur
        // larger than they should be if they are based on the prefab from Long Nail/Mark of Pride/both slash
        var baseScale = _baseScales[type];
        slash.transform.localScale = new Vector3(
            baseScale.x,
            baseScale.y,
            0f
        );
        
        // Get the NailSlash component and destroy it, since we don't want to interfere with the local player
        var originalNailSlash = slash.GetComponent<NailSlash>();
        Object.Destroy(originalNailSlash);

        slash.SetActive(true);

        // Get the slash audio source and its clip
        var slashAudioSource = slash.GetComponent<AudioSource>();
        // Remove original audio source to prevent double audio
        Object.Destroy(slashAudioSource);
        var slashClip = slashAudioSource.clip;

        // Obtain the Nail Arts FSM from the Hero Controller
        var nailArts = HeroController.instance.gameObject.LocateMyFSM("Nail Arts");

        // Obtain the AudioSource from the AudioPlayerOneShotSingle action in the nail arts FSM
        var audioAction = nailArts.GetFirstAction<AudioPlayerOneShotSingle>("Play Audio");
        var audioPlayerObj = audioAction.audioPlayer.Value;
        var audioPlayer = audioPlayerObj.Spawn(playerObject.transform);
        var audioSource = audioPlayer.GetComponent<AudioSource>();

        // Play the slash clip with this newly spawned AudioSource
        audioSource.PlayOneShot(slashClip);

        // Store a boolean indicating whether the Fury of the fallen effect is active


        var slashAnimator = slash.GetComponent<tk2dSpriteAnimator>();
        // Figure out the name of the animation clip based on the slash type
        var clipName = "";
        // Down and Up prefixes
        if (type.Equals(SlashType.Down)) {
            clipName += "Down";
        }

        if (type.Equals(SlashType.Up)) {
            clipName += "Up";
        }

        // The body of the animation clip name
        clipName += "SlashEffect";

        // Alt suffix
        if (type.Equals(SlashType.Alt)) {
            clipName += "Alt";
        }



        // Finally play the animation clip with the constructed name
        slashAnimator.PlayFromFrame(clipName, 0);

        slash.GetComponent<MeshRenderer>().enabled = true;

        var polygonCollider = slash.GetComponent<PolygonCollider2D>();

        polygonCollider.enabled = true;

        // Instantiate additional game object that can interact with enemies so remote enemies can be hit
        GameObject enemySlash;
        {
            enemySlash = Object.Instantiate(prefab, playerAttacks.transform);
            enemySlash.layer = 17;
            enemySlash.name = "Enemy Slash";
            enemySlash.transform.localScale = slash.transform.localScale;

            var typesToRemove = new[] {
                typeof(MeshFilter), typeof(MeshRenderer), typeof(tk2dSprite), typeof(tk2dSpriteAnimator),
                typeof(NailSlash),
                typeof(AudioSource)
            };
            foreach (var typeToRemove in typesToRemove) {
                Object.Destroy(enemySlash.GetComponent(typeToRemove));
            }

            for (var i = 0; i < enemySlash.transform.childCount; i++) {
                Object.Destroy(enemySlash.transform.GetChild(i));
            }

            polygonCollider = enemySlash.GetComponent<PolygonCollider2D>();
            polygonCollider.enabled = true;

            var damagesEnemyFsm = slash.LocateMyFSM("damages_enemy");
            Object.Destroy(damagesEnemyFsm);

            ChangeAttackTypeOfFsm(enemySlash);
        }

        var damage = ServerSettings.NailDamage;
        if (ServerSettings.IsPvpEnabled && ShouldDoDamage) {
            if (ServerSettings.AllowParries) {
                AddParryFsm(slash);
            }

            if (damage != 0) {
                slash.AddComponent<DamageHero>().damageDealt = damage;
            }
        }

        // After the animation is finished, we can destroy the slash object
        var animationDuration = slashAnimator.CurrentClip.Duration;
        Object.Destroy(slash, animationDuration);
        Object.Destroy(enemySlash, animationDuration);
    }

    /// <summary>
    /// Enumeration of nail slash types.
    /// </summary>
    protected enum SlashType {
        Normal,
        Alt,
        Down,
        Up,
        Wall
    }
}
