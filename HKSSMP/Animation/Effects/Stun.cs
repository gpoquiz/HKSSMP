using System.Collections.Generic;
using Hkmp.Util;
using HutongGames.PlayMaker.Actions;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hkmp.Animation.Effects;

/// <summary>
/// Animation effect class for getting hit (which is also getting stunned).
/// </summary>
internal class Stun : AnimationEffect {
    /// <inheritdoc/>
    public override void Play(GameObject playerObject, bool[] effectInfo) {
        RemoveExistingEffects(playerObject);

        CancelFocusEffect(playerObject);

        // Check whether the carefree melody charm activated for the player
        var carefreeActivated = effectInfo[0];

        // Get the player effects object to put new effects in
        var playerEffects = playerObject.FindGameObjectInChildren("Effects");

        PlayDamageEffects(playerEffects);

        PlayHitSound(playerObject);
    
    }

    /// <summary>
    /// Remove all existing effect for the given player object.
    /// </summary>
    /// <param name="playerObject">The GameObject representing the player.</param>
    private void RemoveExistingEffects(GameObject playerObject) {
        // Remove all effects/attacks/spells related animations
        MonoBehaviourUtil.DestroyAllChildren(playerObject.FindGameObjectInChildren("Attacks"));
        // Since we still need the baldur shell animation to play, we don't want to destroy it yet
        MonoBehaviourUtil.DestroyAllChildren(
            playerObject.FindGameObjectInChildren("Effects"),
            new List<string>(new[] {
                "Shell Animation",
                "Shell Animation Last"
            })
        );
        MonoBehaviourUtil.DestroyAllChildren(playerObject.FindGameObjectInChildren("Spells"));
    }

    /// <summary>
    /// Cancel the focus effect for the given player object if it exists.
    /// </summary>
    /// <param name="playerObject">The GameObject representing the player.</param>
    private void CancelFocusEffect(GameObject playerObject) {
        // If either the charge audio or the lines animation objects exist,
        // the player was probably focussing, so we start the Focus End effect
        if (playerObject.FindGameObjectInChildren("Charge Audio") != null ||
            playerObject.FindGameObjectInChildren("Lines Anim") != null) {
            throw new NotImplementedException("CancelFocusEffect");
        }
    }

    /// <summary>
    /// Play damage effects for getting hit.
    /// </summary>
    /// <param name="playerEffects">The GameObject for the player effects.</param>
    private void PlayDamageEffects(GameObject playerEffects) {
        // Obtain the gameObject containing damage effects
        var damageEffect = HeroController.instance.gameObject.FindGameObjectInChildren("Damage Effect");

        // Instantiate a hit crack effect
        var hitCrack = Object.Instantiate(
            damageEffect.FindGameObjectInChildren("Hit Crack"),
            playerEffects.transform
        );
        hitCrack.SetActive(true);

        // Instantiate a object responsible for particle effects
        var hitPt1 = Object.Instantiate(
            damageEffect.FindGameObjectInChildren("Hit Pt 1"),
            playerEffects.transform
        );
        hitPt1.SetActive(true);
        // Play the particle effect
        hitPt1.GetComponent<ParticleSystem>().Play();

        // Instantiate a object responsible for particle effects
        var hitPt2 = Object.Instantiate(
            damageEffect.FindGameObjectInChildren("Hit Pt 2"),
            playerEffects.transform
        );
        hitPt2.SetActive(true);
        // Play the particle effect
        hitPt2.GetComponent<ParticleSystem>().Play();

        // Destroy all objects after 1 second
        Object.Destroy(hitCrack, 1);
        Object.Destroy(hitPt1, 1);
        Object.Destroy(hitPt2, 1);
    }

    /// <summary>
    /// Play the getting hit sound.
    /// </summary>
    /// <param name="playerObject">The GameObject representing the player.</param>
    private void PlayHitSound(GameObject playerObject) {
        // TODO: maybe add an option for playing the hit sound as it is very uncanny
        // Being used to only hearing this when you get hit

        // Obtain the hit audio clip
        var heroAudioController = HeroController.instance.gameObject.GetComponent<HeroAudioController>();
        var takeHitClip = heroAudioController.takeHit.clip;

        // Get a new audio source and play the clip
        var takeHitAudioObject = AudioUtil.GetAudioSourceObject(playerObject);
        var takeHitAudioSource = takeHitAudioObject.GetComponent<AudioSource>();
        takeHitAudioSource.clip = takeHitClip;
        // Decrease volume, since otherwise it is quite loud in contrast to the local player hit sound
        takeHitAudioSource.volume = 0.5f;
        takeHitAudioSource.Play();

        Object.Destroy(takeHitAudioObject, 3.0f);
    }


    /// <inheritdoc/>
    public override bool[] GetEffectInfo() {
        // Whether the Carefree Melody charm effect is currently active

        return [];
    }
}
