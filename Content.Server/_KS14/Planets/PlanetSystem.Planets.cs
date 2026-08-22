using System.Linq;
using System.Threading;
using Content.Server._KS14.Planets;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Content.Shared._KS14.Planets;
using Content.Shared.Shuttles.Components;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.GameStates;

namespace Content.Server._KS14.Planets;

public sealed partial class PlanetSystem
{
    /*
     * Handles setup / teardown of planets.
     */

    private const int PlanetLimit = 8;

    private readonly JobQueue _planetQueue = new();
    private readonly List<(SpawnPlanetJob Job, CancellationTokenSource CancelToken)> _planetJobs = new();
    private const double PlanetTime = 0.002;


    private void InitializePlanets()
    {
        SubscribeLocalEvent<PlanetComponent, MapInitEvent>(OnPlanetMapInit);
        SubscribeLocalEvent<PlanetComponent, ComponentShutdown>(OnPlanetShutdown);
        SubscribeLocalEvent<PlanetComponent, ComponentGetState>(OnPlanetGetState);
    }

    private void OnPlanetGetState(EntityUid uid, PlanetComponent component, ref ComponentGetState args)
    {
        args.State = new PlanetComponentState()
        {
            Stage = component.Stage
        };
    }

    private void OnPlanetMapInit(EntityUid uid, PlanetComponent component, MapInitEvent args)
    {
        component.SelectedSong = _audio.ResolveSound(component.Sound);
    }

    private void OnPlanetShutdown(EntityUid uid, PlanetComponent component, ComponentShutdown args)
    {
        component.Stream = _audio.Stop(component.Stream);

        foreach (var (job, cancelToken) in _planetJobs.ToArray())
        {
            if (job.Station == component.Station)
            {
                cancelToken.Cancel();
                _planetJobs.Remove((job, cancelToken));
            }
        }

        if (Deleted(component.Station))
            return;

        // Finish mission
        if (TryComp<PlanetDataComponent>(component.Station, out var data))
        {
            FinishPlanet((component.Station, data), uid);
        }
    }

    private void UpdatePlanets()
    {
        var currentTime = _timing.CurTime;
        _planetQueue.Process();

        foreach (var (job, cancelToken) in _planetJobs.ToArray())
        {
            switch (job.Status)
            {
                case JobStatus.Finished:
                    _planetJobs.Remove((job, cancelToken));
                    break;
            }
        }

        var query = EntityQueryEnumerator<PlanetDataComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Update offers
            if (comp.NextOffer > currentTime || comp.Claimed)
                continue;

            GeneratePlanets(comp);
        }
    }

    private void FinishPlanet(Entity<PlanetDataComponent> Planet, EntityUid uid)
    {
        var component = Planet.Comp;
        Announce(uid, Loc.GetString("salvage-Planet-completed"));
    }

    private void GeneratePlanets(PlanetDataComponent component)
    {
        component.Missions.Clear();

        for (var i = 0; i < PlanetLimit; i++)
        {
            var mission = new PlanetParams
            {
                Index = component.NextIndex,
                Seed = _random.Next(),
                Difficulty = "Moderate",
            };

            component.Missions[component.NextIndex++] = mission;
        }
    }

    private SalvagePlanetConsoleState GetState(PlanetDataComponent component)
    {
        var missions = component.Missions.Values.ToList();
        return new SalvagePlanetConsoleState(component.NextOffer, component.Claimed, component.Cooldown, component.ActiveMission, missions);
    }

    private void SpawnMission(PlanetParams missionParams, EntityUid station, EntityUid? coordinatesDisk)
    {
        var cancelToken = new CancellationTokenSource();
        var job = new SpawnPlanetJob(
            SalvageJobTime,
            EntityManager,
            _timing,
            _logManager,
            _prototypeManager,
            _anchorable,
            _biome,
            _dungeon,
            _metaData,
            _mapSystem,
            station,
            missionParams,
            cancelToken.Token);

        _planetJobs.Add((job, cancelToken));
        _planetQueue.EnqueueJob(job);
    }
}
