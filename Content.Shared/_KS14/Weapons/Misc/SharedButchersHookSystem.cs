using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Misc;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Misc;

public abstract class SharedButchersHookSystem : VirtualController
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public const string ButchersHookJoint = "butchershook";

    public override void Initialize()
    {
        SubscribeLocalEvent<ButchersHookProjectileComponent, ProjectileEmbedEvent>(OnHookCollide);
        SubscribeLocalEvent<ButchersHookProjectileComponent, JointRemovedEvent>(OnHookJointRemoved);
        SubscribeLocalEvent<CanWeightlessMoveEvent>(OnWeightlessMove);
        SubscribeAllEvent<RequestButchersHookReelMessage>(OnHookReel);

        SubscribeLocalEvent<ButchersHookComponent, GunShotEvent>(OnHookShot);
        SubscribeLocalEvent<ButchersHookComponent, ActivateInWorldEvent>(OnHookActivate);
        SubscribeLocalEvent<ButchersHookComponent, HandDeselectedEvent>(OnHookDeselected);

        UpdatesBefore.Add(typeof(SharedJointSystem));
        base.Initialize();
    }

    private void OnHookJointRemoved(EntityUid uid, ButchersHookProjectileComponent component, JointRemovedEvent args)
    {
        if (_netManager.IsServer)
            QueueDel(uid);
    }

    private void OnHookShot(EntityUid uid, ButchersHookComponent component, ref GunShotEvent args)
    {
        foreach (var (shotUid, _) in args.Ammo)
        {
            if (!HasComp<ButchersHookProjectileComponent>(shotUid))
                continue;

            component.Projectile = shotUid.Value;
            component.AutoReelTriggered = false;
            component.AutoReelTimer = 0f;
            DirtyField(uid, component, nameof(ButchersHookComponent.Projectile));
            var visuals = EnsureComp<JointVisualsComponent>(shotUid.Value);
            visuals.Sprite = component.RopeSprite;
            visuals.Target = uid;
            Dirty(shotUid.Value, visuals);
        }

        TryComp<AppearanceComponent>(uid, out var appearance);
        _appearance.SetData(uid, SharedTetherGunSystem.TetherVisualsStatus.Key, false, appearance);
    }

    private void OnHookDeselected(EntityUid uid, ButchersHookComponent component, HandDeselectedEvent args)
    {
        SetReeling(uid, component, false, args.User);
    }

    private void OnHookReel(RequestButchersHookReelMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!_hands.TryGetActiveItem(player, out var activeItem) ||
            !TryComp<ButchersHookComponent>(activeItem, out var hook))
        {
            return;
        }

        if (msg.Reeling &&
            (!TryComp<CombatModeComponent>(player, out var combatMode) ||
             !combatMode.IsInCombatMode))
        {
            return;
        }

        SetReeling(activeItem.Value, hook, msg.Reeling, player);
    }

    private void OnWeightlessMove(ref CanWeightlessMoveEvent ev)
    {
        if (ev.CanMove || !TryComp<JointRelayTargetComponent>(ev.Uid, out var relayComp))
            return;

        foreach (var relay in relayComp.Relayed)
        {
            if (TryComp<JointComponent>(relay, out var jointRelay) && jointRelay.GetJoints.ContainsKey(ButchersHookJoint))
            {
                ev.CanMove = true;
                return;
            }
        }
    }

    /// <summary>
    /// Unhooks the butcher's hook, destroying the hook and severing the joint
    /// </summary>
    /// <param name="hook">Entity for the butcher's hook gun</param>
    /// <param name="isBreak">Whether to play the sound for the rope breaking</param>
    /// <param name="user">The user responsible for the unhook. Optional</param>
    public void Unhook(Entity<ButchersHookComponent> hook, bool isBreak, EntityUid? user = null)
    {
        if (!Timing.IsFirstTimePredicted || hook.Comp.Projectile is not { } projectile)
            return;

        if(isBreak)
            _audio.PlayPredicted(hook.Comp.BreakSound, hook.Owner, user);

        _appearance.SetData(hook.Owner, SharedTetherGunSystem.TetherVisualsStatus.Key, true);

        if (_netManager.IsServer)
            QueueDel(projectile);

        SetReeling(hook.Owner, hook.Comp, false, user);
        hook.Comp.Projectile = null;
        hook.Comp.AutoReelTriggered = false;
        hook.Comp.AutoReelTimer = 0f;
        DirtyField(hook.Owner, hook.Comp, nameof(ButchersHookComponent.Projectile));
        _gun.ChangeBasicEntityAmmoCount(hook.Owner, 1);
    }

    private void OnHookActivate(EntityUid uid, ButchersHookComponent component, ActivateInWorldEvent args)
    {
        if (!Timing.IsFirstTimePredicted || args.Handled || !args.Complex)
            return;

        _audio.PlayPredicted(component.CycleSound, uid, args.User);
        Unhook((uid, component), false, args.User);

        args.Handled = true;
    }

    private void SetReeling(EntityUid uid, ButchersHookComponent component, bool value, EntityUid? user)
    {
        if (TryComp<JointComponent>(uid, out var jointComp) &&
            jointComp.GetJoints.TryGetValue(ButchersHookJoint, out var joint) &&
            joint is DistanceJoint distance)
        {
            // Only stop reeling if we're fully reeled in (target is close enough)
            if (distance.MaxLength <= distance.MinLength + component.RopeFullyReeledMargin)
                value = false;
        }

        if (component.Reeling == value)
            return;

        if (value)
        {
            component.Stream ??= _audio.PlayPredicted(component.ReelSound, uid, user)?.Entity;
        }
        else if (!value && component.Stream.HasValue && Timing.IsFirstTimePredicted)
        {
            component.Stream = _audio.Stop(component.Stream);
        }

        component.Reeling = value;

        DirtyField(uid, component, nameof(ButchersHookComponent.Reeling));
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<ButchersHookComponent, JointComponent>();

        while (query.MoveNext(out var uid, out var hook, out var jointComp))
        {
            if (!jointComp.GetJoints.TryGetValue(ButchersHookJoint, out var joint) ||
                joint is not DistanceJoint distance ||
                !_entities.TryGetComponent<JointComponent>(joint.BodyAUid, out var hookJointComp))
            {
                if (_netManager.IsServer)
                    Unhook((uid, hook), true);
                continue;
            }

            if (distance.Enabled == false)
            {
                Unhook((uid, hook), true);
                continue;
            }

            // Get the physical bodies (handles relays/containers)
            var physicalAttacker = jointComp.Relay.HasValue ? jointComp.Relay.Value : joint.BodyBUid;
            var physicalTarget = hookJointComp.Relay.HasValue ? hookJointComp.Relay.Value : joint.BodyAUid;

            // Prevent sleeping if on different grids
            if (_transform.GetGrid(physicalAttacker) != _transform.GetGrid(physicalTarget))
            {
                _physics.WakeBody(physicalAttacker);
                _physics.WakeBody(physicalTarget);
            }

            var attackerPos = _transform.GetWorldPosition(physicalAttacker);
            var targetPos = _transform.GetWorldPosition(physicalTarget);
            var ropeLength = (attackerPos - targetPos).Length();

            // Break if too far
            if (ropeLength >= distance.MaxLength + hook.RopeMargin)
            {
                Unhook((uid, hook), true);
                continue;
            }

            // Auto-reel logic
            if (hook.Projectile.HasValue && !hook.Reeling && !hook.AutoReelTriggered)
            {
                hook.AutoReelTimer += frameTime;
                if (hook.AutoReelTimer >= hook.AutoReelDelay)
                {
                    hook.AutoReelTriggered = true;
                    SetReeling(uid, hook, true, null);
                }
            }

            if (!hook.Reeling)
            {
                if (hook.Stream.HasValue && Timing.IsFirstTimePredicted)
                    hook.Stream = _audio.Stop(hook.Stream);
                continue;
            }

            // Calculate reel speed - use auto-reel speed if triggered automatically, otherwise use manual speed
            var reelSpeed = hook.AutoReelTriggered ? hook.AutoReelSpeed : hook.ReelRate;

            // Contract the rope
            if (distance.MaxLength >= ropeLength + hook.RopeMargin)
            {
                distance.MaxLength = MathF.Max(
                    distance.MinLength + hook.RopeMargin,
                    distance.MaxLength - reelSpeed * frameTime
                );
                distance.MaxLength = MathF.Max(ropeLength + hook.RopeMargin, distance.MaxLength);
                ropeLength = MathF.Min(distance.MaxLength, ropeLength);

                distance.Length = ropeLength;
            }

            // Only apply physics forces to the target, not the attacker
            // Stop reeling when target is close enough
            if (ropeLength <= distance.MinLength + hook.RopeFullyReeledMargin)
            {
                SetReeling(uid, hook, false, null);
                hook.AutoReelTriggered = false;
                hook.AutoReelTimer = 0f;
            }
            else if (ropeLength >= distance.MaxLength - hook.RopeMargin)
            {
                // Direction from attacker to target (pull target toward attacker)
                var pullDirection = (attackerPos - targetPos).Normalized();

                // Get the target entity (the one being pulled)
                var targetUid = _container.TryGetOuterContainer(physicalTarget, Transform(physicalTarget), out var containerTarget)
                    ? containerTarget.Owner
                    : physicalTarget;

                var targetBody = Comp<PhysicsComponent>(targetUid);

                // Apply force ONLY to the target, pulling it toward the attacker
                var massFactor = MathF.Min(targetBody.InvMass * hook.ReelMassCoefficient, 1f);
                var force = hook.ReelForce * massFactor * frameTime;

                // Use auto-reel force multiplier if auto-reeling
                if (hook.AutoReelTriggered)
                    force *= hook.AutoReelForceMultiplier;

                _physics.ApplyLinearImpulse(targetUid, pullDirection * force, body: targetBody);
            }

            Dirty(uid, jointComp);
        }
    }

    /// <summary>
    /// Checks whether the entity is hooked to something via butcher's hook.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if hooked, false otherwise.</returns>
    public bool IsEntityHooked(Entity<JointRelayTargetComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        foreach (var uid in entity.Comp.Relayed)
        {
            if (HasComp<ButchersHookComponent>(uid))
                return true;
        }

        return false;
    }

    private void OnHookCollide(EntityUid uid, ButchersHookProjectileComponent component, ref ProjectileEmbedEvent args)
    {
        if (!Timing.IsFirstTimePredicted || !args.Weapon.HasValue || !_entities.TryGetComponent<ButchersHookComponent>(args.Weapon, out var hook))
            return;

        var hookPos = _transform.GetWorldPosition(args.Weapon.Value);
        var projectilePos = _transform.GetWorldPosition(uid);
        if ((hookPos - projectilePos).Length() >= hook.RopeMaxLength)
        {
            Unhook((args.Weapon.Value, hook), true);
            return;
        }

        var joint = _joints.CreateDistanceJoint(uid, args.Weapon.Value, id: ButchersHookJoint);
        joint.MaxLength = joint.Length + hook.RopeMargin;
        joint.Stiffness = hook.RopeStiffness;
        joint.MinLength = hook.RopeMinLength;
        joint.Breakpoint = hook.RopeBreakPoint;

        var jointCompHook = _entities.GetComponent<JointComponent>(uid);
        var jointCompWeapon = _entities.GetComponent<JointComponent>(args.Weapon.Value);

        _joints.SetRelay(uid, args.Embedded, jointCompHook);
        _joints.RefreshRelay(args.Weapon.Value, jointCompWeapon);

        // Reset auto-reel timer on embed
        hook.AutoReelTriggered = false;
        hook.AutoReelTimer = 0f;
    }

    [Serializable, NetSerializable]
    protected sealed class RequestButchersHookReelMessage : EntityEventArgs
    {
        public bool Reeling;

        public RequestButchersHookReelMessage(bool reeling)
        {
            Reeling = reeling;
        }
    }
}
