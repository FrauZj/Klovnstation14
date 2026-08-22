using Content.Client.Audio;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions;
using Robust.Client.Player;
using Robust.Shared.GameStates;

namespace Content.Client._KS14.Planets;

public sealed class PlanetSystem : SharedSalvageSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ContentAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnPlayAmbientMusic);
        SubscribeLocalEvent<PlanetComponent, ComponentHandleState>(OnPlanetHandleState);
    }

    private void OnPlanetHandleState(EntityUid uid, PlanetComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not PlanetComponentState state)
            return;

        component.Stage = state.Stage;

        if (component.Stage >= PlanetStage.MusicCountdown)
        {
            _audio.DisableAmbientMusic();
        }
    }

    private void OnPlayAmbientMusic(ref PlayAmbientMusicEvent ev)
    {
        if (ev.Cancelled)
            return;

        var player = _playerManager.LocalEntity;

        if (!TryComp(player, out TransformComponent? xform) ||
            !TryComp<PlanetComponent>(xform.MapUid, out var planet) ||
            planet.Stage < PlanetStage.MusicCountdown)
        {
            return;
        }

        ev.Cancelled = true;
    }
}
