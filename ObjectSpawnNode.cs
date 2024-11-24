using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;
using Warudo.Core.Utils;
using Warudo.Plugins.Core.Assets;
using Warudo.Plugins.Core.Assets.Character;
using Warudo.Plugins.Core.Events;
using Warudo.Plugins.Core.Utils;
using Warudo.Plugins.Interactions.Mixins;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

[NodeType(
    Id = "41135db6-982d-4e90-8094-933123eb781f",
    Title = "オブジェクト生成 ObjectSpawn",
    Category = "CATEGORY_INTERACTIONS"
)]
public class ObjectSpawnNode : Node
{
    [DataInput]
    [Label("PROP_SOURCE")]
    [PreviewGallery]
    [AutoCompleteResource("Prop")]
    public string PropSource;

    // [DataInput]
    // [Label("IMPACT_PARTICLE_SOURCE")]
    // [PreviewGallery]
    // [AutoCompleteResource("Particle")]
    // public string ImpactParticleSource;

    [DataInput]
    [Label("SCALE")]
    [FloatSlider(0.1f, 3f)]
    public float Scale = 1f;

    [DataInput]
    [FloatSlider(1f, 100f)]
    [Label("MASS")]
    public float Mass = 25f;

    [DataInput]
    [FloatSlider(1f, 10f)]
    [Label("SPEED")]
    public float Speed = 5f;

    [DataInput]
    [Label("GRAVITY")]
    public bool Gravity = true;

    [DataInput]
    [FloatSlider(0f, 10f)]
    [Label("LAUNCH_TORQUE")]
    public float LaunchTorque = 0f;

    [DataInput]
    [Label("RANDOMIZE_LAUNCH_ROTATION")]
    public bool RandomizeLaunchRotation = true;

    [DataInput]
    [Label("ALIVE_TIME")]
    [Description("ALIVE_TIME_DESCRIPTION")]
    public float AliveTime = 5f;


    [DataInput]
    [Label("FROM_WORLD_POSITION")]
    private Vector3 FromWorldPosition;

    [DataInput]
    [Label("目標ワールド座標")]
    private Vector3 ToWorldPosition;



    private GameObject impactParticle;
    private AudioClip launchSound;
    private AudioClip impactSound;
    private float particleAliveTime;

    protected override void OnCreate()
    {
        base.OnCreate();
        // FromTo.CharacterGetter = () => Character;
        // Watch(nameof(ImpactParticleSource), () =>
        // {
        //     if (impactParticle != null)
        //     {
        //         Object.Destroy(impactParticle);
        //         impactParticle = null;
        //     }
        //     if (ImpactParticleSource != null)
        //     {
        //         impactParticle = Context.ResourceManager.ResolveResourceUri<GameObject>(ImpactParticleSource);
        //         impactParticle.SetActive(false);

        //         particleAliveTime = 0f;
        //         foreach (var particleSystem in impactParticle.GetComponentsInChildren<ParticleSystem>())
        //         {
        //             particleAliveTime = Mathf.Max(particleAliveTime, particleSystem.main.duration);
        //         }
        //         particleAliveTime += 5f;
        //     }
        // });
        // Watch(nameof(LaunchSoundSource), () =>
        // {
        //     launchSound = null;
        //     if (!LaunchSoundSource.IsNullOrWhiteSpace())
        //     {
        //         launchSound = Context.ResourceManager.ResolveResourceUri<AudioClip>(LaunchSoundSource);
        //     }
        // });
        // Watch(nameof(ImpactSoundSource), () =>
        // {
        //     impactSound = null;
        //     if (!ImpactSoundSource.IsNullOrWhiteSpace())
        //     {
        //         impactSound = Context.ResourceManager.ResolveResourceUri<AudioClip>(ImpactSoundSource);
        //     }
        // });
    }
    public override void Destroy()
    {
        base.Destroy();
        if (impactParticle != null)
        {
            Object.DestroyImmediate(impactParticle);
            impactParticle = null;
        }
        launchSound = null;
        impactSound = null;
    }

    [FlowInput]
    public Continuation Enter()
    {
        if (!PropSource.IsNullOrWhiteSpace())
        {
            Throw();
        }

        return Exit;
    }

    [FlowOutput]
    public Continuation Exit;

    public async void Throw()
    {
        var gameObject = Context.ResourceManager.ResolveResourceUri<GameObject>(PropSource);
        if (gameObject == null)
        {
            throw new Exception("Failed to load resource: " + PropSource);
        }
        gameObject.transform.localScale *= Scale;
        gameObject.transform.SetLayerRecursively(LayerMask.NameToLayer("Prop"));
        gameObject.AddComponent<GameObjectTags>().tags = new[] { "ThrownProp" };

        // TODO: This is necessary or not?
        if (Application.isEditor)
        {
            // Wait for shaders to compile
            gameObject.SetActive(false);
            await UniTask.DelayFrame(5);
            gameObject.SetActive(true);
        }

        // Character.DisableTemporaryRagdollTime = Time.realtimeSinceStartup + AliveTime;

        var rigidbody = gameObject.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        rigidbody.mass = Mass;

        var startPos = this.FromWorldPosition;
        var endPos = this.ToWorldPosition;
        gameObject.transform.position = startPos;
        if (RandomizeLaunchRotation)
        {
            gameObject.transform.localRotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
        }
        // var behavior = gameObject.AddComponent<ThrownPropBehavior>();
        // behavior.Parent = this;

        var collider = gameObject.GetComponentInChildren<Collider>();
        if (collider != null)
        {
            var mb = gameObject.AddComponent<ReceiveTriggerEventsBehavior>();
            var receiverName = "ThrownProp:" + Id + ":" + Guid.NewGuid();
            mb.CollisionEnter = sender =>
            {
                var collisionEnteredEvent = new TriggerEnteredEvent(sender, collider, receiverName);
                Context.EventBus.Broadcast(collisionEnteredEvent);
            };
            mb.CollisionStay = sender =>
            {
                var collisionStayedEvent = new TriggerStayedEvent(sender, collider, receiverName);
                Context.EventBus.Broadcast(collisionStayedEvent);
            };
            mb.CollisionExit = sender =>
            {
                var collisionExitedEvent = new TriggerExitedEvent(sender, collider, receiverName);
                Context.EventBus.Broadcast(collisionExitedEvent);
            };
        }

        if (Gravity)
        {
            // Calculate the displacement vector and its horizontal distance
            var deltaPosition = endPos - startPos;

            var t = 1 / Speed;

            // Calculate the horizontal and vertical distances
            float horizontalDistance = new Vector2(deltaPosition.x, deltaPosition.z).magnitude;
            float verticalDistance = deltaPosition.y;

            // Calculate the initial horizontal and vertical velocities
            float initialHorizontalVelocity = horizontalDistance / t;
            float gravity = Physics.gravity.y;
            float initialVerticalVelocity = verticalDistance / t + 0.5f * Mathf.Abs(gravity) * t;

            // Calculate the initial velocity vector
            Vector3 initialVelocity = new Vector3(deltaPosition.x, 0, deltaPosition.z).normalized * initialHorizontalVelocity;
            initialVelocity.y = initialVerticalVelocity;

            // Calculate the force vector
            Vector3 force = Mass * initialVelocity;

            // Apply the force to the rigidbody
            rigidbody.AddForce(force, ForceMode.Impulse);
        }
        else
        {
            var deltaPosition = endPos - startPos;
            var direction = deltaPosition.normalized;
            rigidbody.velocity = direction * Speed;
            rigidbody.useGravity = false;
        }

        rigidbody.maxAngularVelocity = Mathf.Max(7f, LaunchTorque * LaunchTorque);
        rigidbody.AddTorque(Random.insideUnitSphere * LaunchTorque, ForceMode.Impulse);

        // behavior.PlayLaunchSound();

        Object.Destroy(gameObject, AliveTime);
    }
}
