using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Misc;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class ButchersHookComponent : Component
{
    /// <summary>
    /// Hook's reeling speed when there's no resistance (manual reeling).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ReelRate = 1f;

    /// <summary>
    /// Auto-reel speed - much faster than manual reeling.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AutoReelSpeed = 6f;  // 6x faster than manual

    /// <summary>
    /// Force multiplier for auto-reel - makes it pull harder.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AutoReelForceMultiplier = 1f;

    /// <summary>
    /// Amount of force to use while reeling.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ReelForce = 4000f;

    /// <summary>
    /// Highest mass that can be reeled in without resistance
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ReelMassCoefficient = 80f;

    /// <summary>
    /// Margin between max length and the butcher's hook when reeling the hook in.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RopeMargin = 0.2f;

    /// <summary>
    /// Margin from the min length for the rope to be considered fully reeled-in.
    /// Lowered to bring target closer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RopeFullyReeledMargin = 0.1f;  // Reduced from 0.22 to bring target closer

    /// <summary>
    /// Minimum length for the butcher's hook's rope.
    /// Reduced to bring target right next to attacker.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RopeMinLength = 0.2f;  // Reduced from 1.0 to bring target closer

    /// <summary>
    /// Maximum length the hook can actually be.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? RopeMaxLength;

    /// <summary>
    /// Stiffness of the rope, in N/m
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RopeStiffness = 1f;

    /// <summary>
    /// Amount of force, in newtons, needed to snap the rope
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RopeBreakPoint = 50000f;

    /// <summary>
    /// Entity UID of the butcher's hook projectile
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Projectile;

    /// <summary>
    /// Whether or not the butcher's hook is currently reeling in
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Reeling;

    /// <summary>
    /// Time in seconds before auto-reel starts after embedding
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AutoReelDelay = 0.5f;

    /// <summary>
    /// Timer for auto-reel delay
    /// </summary>
    [DataField]
    public float AutoReelTimer = 0f;

    /// <summary>
    /// Whether auto-reel has been triggered
    /// </summary>
    [DataField]
    public bool AutoReelTriggered = false;

    /// <summary>
    /// Looping sound used while the butcher's hook is reeling
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? ReelSound = new SoundPathSpecifier("/Audio/Weapons/reel.ogg")
    {
        Params = AudioParams.Default.WithLoop(true)
    };

    /// <summary>
    /// Sound that plays when the user cycles the butcher's hook by using it in their hand
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? CycleSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/kinetic_reload.ogg");

    /// <summary>
    /// Sound that plays when the rope breaks due to physics
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? BreakSound = new SoundPathSpecifier("/Audio/Items/snap.ogg");

    /// <summary>
    /// Sprite specifier for the rope, used to visualize the joint
    /// </summary>
    [DataField, AutoNetworkedField]
    public SpriteSpecifier RopeSprite =
        new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Launchers/grappling_gun.rsi"), "rope");

    /// <summary>
    /// Entity UID for the audio stream, which plays <see cref="ReelSound"/>.
    /// </summary>
    [ViewVariables]
    public EntityUid? Stream;
}
