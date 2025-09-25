using Hkmp.Util;
using HutongGames.PlayMaker.Actions;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

namespace Hkmp.Animation.Effects;

/// <summary>
/// Abstract base class for the animation effect of dashing.
/// </summary>
internal abstract class DashBase : DamageAnimationEffect {
    /// <inheritdoc/>
    public abstract override void Play(GameObject playerObject, bool[] effectInfo);

    /// <summary>
    /// Plays the dash animation for the given player object with the given effect info and booleans
    /// denoting what kind of dash it is.
    /// </summary>
    /// <param name="playerObject">The GameObject representing the player.</param>
    /// <param name="effectInfo">A boolean array containing effect info.</param>
    /// <param name="shadowDash">Whether this dash is a shadow dash.</param>
    /// <param name="sharpShadow">Whether this dash is a sharp shadow dash.</param>
    /// <param name="dashDown">Whether this is a downwards dash.</param>
    protected void Play(GameObject playerObject, bool[] effectInfo, bool shadowDash, bool sharpShadow,
        bool dashDown) {
        // Obtain the dash audio clip
        var heroAudioController = HeroController.instance.gameObject.GetComponent<HeroAudioController>();
        var dashAudioClip = heroAudioController.dash.clip;

        // Get a new audio source and play the clip
        var dashAudioSourceObject = AudioUtil.GetAudioSourceObject(playerObject);
        var dashAudioSource = dashAudioSourceObject.GetComponent<AudioSource>();
        dashAudioSource.clip = dashAudioClip;
        dashAudioSource.Play();

        // Destroy the audio object after the clip is finished
        Object.Destroy(dashAudioSourceObject, dashAudioClip.length);

        var playerEffects = playerObject.FindGameObjectInChildren("Effects");

        // Store the transform and scale, because we need it later
        var playerTransform = playerObject.transform;
        var playerScale = playerTransform.localScale;

   
        // Instantiate the dash burst relative to the player effects
        var dashBurstObject = HeroController.instance.dashBurst.gameObject;
        var dashBurst = Object.Instantiate(
            dashBurstObject,
            playerEffects.transform
        );

        // Destroy the original FSM to prevent it from taking control of the animation
        Object.Destroy(dashBurst.LocateMyFSM("Effect Control"));
        dashBurst.SetActive(true);

        var dashBurstTransform = dashBurst.transform;

        // Set the position and rotation of the dash burst
        // These are all values from the HeroController HeroDash method
        if (dashDown) {
            dashBurstTransform.localPosition = new Vector3(-0.07f, 3.74f, 0.01f);
            dashBurstTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
        } else {
            dashBurstTransform.localPosition = new Vector3(4.11f, -0.55f, 0.001f);
            dashBurstTransform.localEulerAngles = new Vector3(0f, 0f, 0f);
        }

        // Enable the mesh renderer
        dashBurst.GetComponent<MeshRenderer>().enabled = true;

        // Get the sprite animator and play the dash effect clip
        var dashBurstAnimator = dashBurst.GetComponent<tk2dSpriteAnimator>();
        dashBurstAnimator.Play("Dash Effect");

        // Destroy the object after the clip is finished
        Object.Destroy(dashBurst, dashBurstAnimator.GetClipByName("Dash Effect").Duration);

        // Find already existing dash particles object, or create a new one
        var dashParticles = HeroController.instance.dashParticles;



        // Give it a name, so we can reference it later
        dashParticles.name = "Dash Particles";

        // Start emitting the smoke cloud particles in the trail of the knight dash
#pragma warning disable 0618
        dashParticles.enableEmission = true;
#pragma warning restore 0618

        MonoBehaviourUtil.Instance.StartCoroutine(DelayedDisable(dashParticles));

        // If we are on the ground, we also spawn the dust cloud facing away from the knight
        if (effectInfo[0]) {
            var backDashEffect = HeroController.instance.backDashPrefab.Spawn(
                playerObject.transform.position
            );
            backDashEffect.transform.localScale = new Vector3(
                playerScale.x * -1f,
                playerScale.y,
                playerScale.z
            );
        }
    
    }

    private IEnumerator DelayedDisable(ParticleSystem dashParticles) {
        yield return new WaitForSeconds(0.9f);

        dashParticles.enableEmission = false;
    }

    /// <inheritdoc/>
    public override bool[] GetEffectInfo() {
        return new[] { HeroController.instance.cState.onGround };
    }
}
