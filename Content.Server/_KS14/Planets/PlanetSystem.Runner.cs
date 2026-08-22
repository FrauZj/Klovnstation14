using System.Numerics;
using Content.Server._KS14.Planets;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._KS14.Planets;
using Content.Shared.Shuttles.Components;
using Content.Shared.Localizations;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._KS14.Planets;

public sealed partial class PlanetSystem
{
    /*
     * Handles actively running planet.
     */

    [Dependency] private readonly MobStateSystem _mobState = default!;

    private void InitializeRunner()
    {
        SubscribeLocalEvent<FTLRequestEvent>(OnFTLRequest);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnConsoleFTLAttempt);
    }

    private void OnConsoleFTLAttempt(ref ConsoleFTLAttemptEvent ev)
    {
        if (!TryComp(ev.Uid, out TransformComponent? xform) ||
            !TryComp<PlanetComponent>(xform.MapUid, out var planet))
        {
            return;
        }

        // TODO: This is terrible but need bluespace harnesses or something.
        // var query = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent, TransformComponent>();

        // while (query.MoveNext(out var uid, out _, out var mobState, out var mobXform))
        // {
        //     if (mobXform.MapUid != xform.MapUid)
        //         continue;

        //     // Don't count unidentified humans (loot) or anyone you murdered so you can still maroon them once dead.
        //     if (_mobState.IsDead(uid, mobState))
        //         continue;

        //     // Okay they're on salvage, so are they on the shuttle.
        //     if (mobXform.GridUid != ev.Uid)
        //     {
        //         ev.Cancelled = true;
        //         ev.Reason = Loc.GetString("salvage-expedition-not-all-present");
        //         return;
        //     }
        // }
    }

    /// <summary>
    /// Announces status updates to salvage crewmembers on the state of the expedition.
    /// </summary>
    private void Announce(EntityUid mapUid, string text)
    {
        var mapId = Comp<MapComponent>(mapUid).MapId;

        // I love TComms and chat!!!
        _chat.ChatMessageToManyFiltered(
            Filter.BroadcastMap(mapId),
            ChatChannel.Radio,
            text,
            text,
            _mapSystem.GetMapOrInvalid(mapId),
            false,
            true,
            null);
    }

    private void OnFTLRequest(ref FTLRequestEvent ev)
    {
        if (!HasComp<PlanetComponent>(ev.MapUid) ||
            !TryComp<FTLDestinationComponent>(ev.MapUid, out var dest))
        {
            return;
        }

        // Only one shuttle can occupy a planet for now.
        dest.Enabled = false;
        _shuttleConsoles.RefreshShuttleConsoles();
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        if (!TryComp<PlanetComponent>(args.MapUid, out var component))
            return;

        // Someone FTLd there so start announcement
        if (component.Stage != PlanetStage.Added)
            return;

        Announce(args.MapUid, Loc.GetString("salvage-expedition-announcement-countdown-minutes", ("duration", (component.EndTime - _timing.CurTime).Minutes)));

        var directionLocalization = ContentLocalizationManager.FormatDirection(component.DungeonLocation.GetDir()).ToLower();

        if (component.DungeonLocation != Vector2.Zero)
            Announce(args.MapUid, Loc.GetString("salvage-expedition-announcement-dungeon", ("direction", directionLocalization)));

        component.Stage = PlanetStage.Running;
        Dirty(args.MapUid, component);
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        if (!TryComp<PlanetComponent>(ev.FromMapUid, out var expedition) ||
            !TryComp<PlanetDataComponent>(expedition.Station, out var station))
        {
            return;
        }

        // Check if any shuttles remain.
        var query = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();

        while (query.MoveNext(out _, out var xform))
        {
            if (xform.MapUid == ev.FromMapUid)
                return;
        }

        // Dont end the planet on shuttle leaving.
        // QueueDel(ev.FromMapUid.Value);
    }

    // Runs the planet
    private void UpdateRunner()
    {
        // Generic missions
        var query = EntityQueryEnumerator<PlanetComponent>();

        // Run the basic mission timers (e.g. announcements, auto-FTL, completion, etc)
        while (query.MoveNext(out var uid, out var comp))
        {
            var remaining = comp.EndTime - _timing.CurTime;
            var audioLength = _audio.GetAudioLength(comp.SelectedSong);

            if (comp.Stage < PlanetStage.FinalCountdown && remaining < TimeSpan.FromSeconds(45))
            {
                comp.Stage = PlanetStage.FinalCountdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("salvage-expedition-announcement-countdown-seconds", ("duration", TimeSpan.FromSeconds(45).Seconds)));
            }
            else if (comp.Stream == null && remaining < audioLength)
            {
                var audio = _audio.PlayPvs(comp.Sound, uid);
                comp.Stream = audio?.Entity;
                _audio.SetMapAudio(audio);
                comp.Stage = PlanetStage.MusicCountdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("salvage-expedition-announcement-countdown-minutes", ("duration", audioLength.Minutes)));
            }
            else if (comp.Stage < PlanetStage.Countdown && remaining < TimeSpan.FromMinutes(4))
            {
                comp.Stage = PlanetStage.Countdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("salvage-expedition-announcement-countdown-minutes", ("duration", TimeSpan.FromMinutes(5).Minutes)));
            }
            // Auto-FTL out any shuttles
            else if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime) + TimeSpan.FromSeconds(0.5))
            {
                var ftlTime = (float)remaining.TotalSeconds;

                if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime))
                {
                    ftlTime = MathF.Max(0, (float)remaining.TotalSeconds - 0.5f);
                }

                ftlTime = MathF.Min(ftlTime, _shuttle.DefaultStartupTime);
                var shuttleQuery = AllEntityQuery<ShuttleComponent, TransformComponent>();

                if (TryComp<StationDataComponent>(comp.Station, out var data))
                {
                    foreach (var member in data.Grids)
                    {
                        while (shuttleQuery.MoveNext(out var shuttleUid, out var shuttle, out var shuttleXform))
                        {
                            if (shuttleXform.MapUid != uid || HasComp<FTLComponent>(shuttleUid))
                                continue;

                            _shuttle.FTLToDock(shuttleUid, shuttle, member, ftlTime);
                        }

                        break;
                    }
                }
            }

            if (remaining < TimeSpan.Zero)
            {
                QueueDel(uid);
            }
        }
    }
}
