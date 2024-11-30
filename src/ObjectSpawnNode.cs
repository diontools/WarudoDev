using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    private readonly HashSet<GameObject> _spawnedObjects = new(128);

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
    [Label("重さ")]
    public float Mass = 25f;

    [DataInput]
    [FloatSlider(0f, 10f)]
    [Label("初速")]
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
    [IntegerSlider(1, 100)]
    [Label("生成数")]
    public int SpawnCount = 1;

    [DataInput]
    [FloatSlider(0f, 3f)]
    [Label("生成間隔")]
    public float SpawnInterval = 1f;


    [DataInput]
    [Label("FROM_WORLD_POSITION")]
    private Vector3 FromWorldPosition;

    [DataInput]
    [Label("目標ワールド座標")]
    private Vector3 ToWorldPosition;


    [DataInput]
    [Label("LAUNCH_SOUND_SOURCE")]
    [AutoCompleteResource("Sound")]
    public string LaunchSoundSource;

    [DataInput]
    [Label("SOUND_VOLUME")]
    [FloatSlider(0f, 1f)]
    public float SoundVolume = 0.1f;


    [DataOutput]
    [Label("オブジェクト数")]
    public int ObjectCount() => this._spawnedObjects.Count;


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
        Watch(nameof(LaunchSoundSource), () =>
        {
            launchSound = null;
            if (!LaunchSoundSource.IsNullOrWhiteSpace())
            {
                launchSound = Context.ResourceManager.ResolveResourceUri<AudioClip>(LaunchSoundSource);
            }
        });
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

    [FlowOutput]
    [Label("LOOP_BODY")]
    public Continuation LoopBody;

    [FlowOutput]
    [Label("ON_LOOP_END")]
    public Continuation OnLoopEnd;


    [Trigger]
    [Label("生成したオブジェクトをクリア")]
    public void ClearObjects()
    {
        foreach (var obj in _spawnedObjects)
        {
            Object.Destroy(obj);
        }
    }

    public async void Throw()
    {
        var propSource = this.PropSource;
        var scale = this.Scale;
        var mass = this.Mass;
        var aliveTime = this.AliveTime;

        var spawnCount = this.SpawnCount;
        var spawnInterval = this.SpawnInterval;

        for (var i = 0; i < spawnCount; i++)
        {
            if (i > 0)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(spawnInterval));
            }

            InvokeFlow(nameof(LoopBody));
            await ThrowOne(propSource, scale, mass, aliveTime);
        }

        InvokeFlow(nameof(OnLoopEnd));
    }

    private async Task ThrowOne(string propSource, float scale, float mass, float aliveTime)
    {
        var gameObject = Context.ResourceManager.ResolveResourceUri<GameObject>(propSource);
        if (gameObject == null)
        {
            throw new Exception("Failed to load resource: " + propSource);
        }
        gameObject.transform.localScale *= scale;
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
        rigidbody.mass = mass;

        var startPos = FromWorldPosition;
        var endPos = ToWorldPosition;
        gameObject.transform.position = startPos;
        if (RandomizeLaunchRotation)
        {
            gameObject.transform.localRotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
        }

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

        var deltaPosition = endPos - startPos;

        if (Gravity)
        {
            var initialVelocity = deltaPosition.normalized * Speed;
            var force = mass * initialVelocity;
            rigidbody.AddForce(force, ForceMode.Impulse);
        }
        else
        {
            var direction = deltaPosition.normalized;
            rigidbody.velocity = direction * Speed;
            rigidbody.useGravity = false;
        }

        rigidbody.maxAngularVelocity = Mathf.Max(7f, LaunchTorque * LaunchTorque);
        rigidbody.AddTorque(Random.insideUnitSphere * LaunchTorque, ForceMode.Impulse);

        var behavior = gameObject.AddComponent<ThrownPropBehavior>();
        behavior.Parent = this;
        behavior.PlayLaunchSound();

        Object.Destroy(gameObject, aliveTime);
        _spawnedObjects.Add(gameObject);
    }

    class ThrownPropBehavior : MonoBehaviour
    {
        public ObjectSpawnNode Parent { get; set; }

        public bool Collided { get; set; }

        private AudioSource audioSource;

        public void PlayLaunchSound()
        {
            if (Parent.launchSound != null)
            {
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = Parent.launchSound;
                audioSource.volume = Parent.SoundVolume;
                audioSource.Play();
            }
        }

        void OnDestroy()
        {
            this.Parent._spawnedObjects.Remove(this.gameObject);
        }

        // void OnCollisionEnter(Collision collision)
        // {
        //     if (Collided || Parent.Character.IsNullOrInactiveOrDisabled() || !collision.collider.transform.IsChildOf(Parent.Character.PuppetMaster.transform)) return;
        //     Collided = true;
        //     var collisionPosition = collision.contacts[0].point;
        //     Parent.lastCollisionPosition = collisionPosition;
        //     Parent.InvokeFlow(nameof(Parent.OnCollide));
        //     if (Parent.impactParticle != null)
        //     {
        //         var particle = Instantiate(Parent.impactParticle, collisionPosition, Parent.impactParticle.transform.rotation);
        //         particle.SetActive(true);
        //         particle.transform.localScale *= Parent.ImpactParticleScale;
        //         particle.GetComponents<PSSizeControl>().ForEach(it => it.scaleNumber = Parent.ImpactParticleScale);

        //         Destroy(particle, Parent.particleAliveTime);
        //     }
        //     if (Parent.impactSound != null)
        //     {
        //         if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        //         audioSource.clip = Parent.impactSound;
        //         audioSource.volume = Parent.SoundVolume;
        //         audioSource.Play();
        //         Destroy(audioSource, audioSource.clip.length);
        //     }
        //     if (Parent.DespawnOnImpact)
        //     {
        //         Destroy(gameObject);
        //     }
        // }
    }
}
