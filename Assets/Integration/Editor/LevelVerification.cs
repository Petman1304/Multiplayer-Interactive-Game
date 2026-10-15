using System;
using Getaway.Integration;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>Explicitly invoked pure checks. Does not enter Play, build, or run a profiler.</summary>
public static class LevelVerification
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception("Level verification: " + message); }
    public static string Run(RouteDefinition suppliedRoute = null)
    {
        var route = suppliedRoute != null ? suppliedRoute : AssetDatabase.LoadAssetAtPath<RouteDefinition>(DowntownRouteAuthoring.AssetPath);
        Check(route != null && route.IsBaked, "baked route");
        var scratch = new RoadThreat[20]; var previous = new bool[43]; var next = new bool[43];
        int admitted = 0, rejected = 0, changing = 0, staggered = 0; var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (float speed in new[] { 18f, 22, 30, 42 })
            for (int seed = 0; seed < 8; seed++) for (int circuit = 0; circuit < 3; circuit++)
                for (int index = 0; index < route.Encounters.Length; index++)
                {
                    var e = route.Encounters[index]; if (e.Kind == EncounterKind.Police || e.Kind == EncounterKind.Recovery) continue;
                    double at = circuit * route.Length + Math.Max(0, e.Distance - speed * 6);
                    int count = EncounterDirector.Count(e, circuit);
                    for (int part = 0; part < count; part++)
                    {
                        scratch[part] = EncounterDirector.Activate(route, seed, circuit, index, part, at, speed);
                        Check(scratch[part].Equals(EncounterDirector.Activate(route, seed, circuit, index, part, at, speed)), "seeded activation");
                        var firstConflict = EncounterMotion.Predict(scratch[part], 4);
                        float half = (GetawayRules.TruckCollisionSize.z + GetawayRules.ThreatSize(scratch[part].Kind).z) * .5f;
                        Check(firstConflict.RouteDistance - (at + EncounterMotion.Travel(speed, 4)) > half, "four-second minimum warning");
                    }
                    bool safe = EncounterMotion.SafeWave(route, at, 0, speed, scratch, count, 36, previous, next);
                    if (safe) { admitted++; if (e.Kind == EncounterKind.LaneChange) changing++; if (e.Kind == EncounterKind.Staggered) staggered++; } else rejected++;
                }
        Check(admitted > 0 && changing > 0 && staggered > 0, "all authored behaviours have reachable paths");
        var wave = new RoadThreat[2];
        var round = new RoundState { Distance=100, Speed=22, Elapsed=5, RouteSeed=7919 };
        var decision = EncounterDirector.Schedule(route,round,0,0,scratch,0,wave,previous,next,0,out int proposed);
        Check(decision==WaveAdmission.Ready && proposed==1, "first overtaking encounter admitted");
        var firstWave=wave[0];
        Check(EncounterDirector.Schedule(route,round,0,0,scratch,0,wave,previous,next,0,out proposed)==decision && firstWave.Equals(wave[0]), "scheduler determinism");
        for(int i=0;i<4;i++)scratch[i]=new RoadThreat{Active=true,Kind=ThreatKind.Repair};
        Check(EncounterDirector.Schedule(route,round,0,0,scratch,4,wave,previous,next,0,out proposed)==WaveAdmission.Pending,"full budget defers whole wave");
        round.Distance=400;
        Check(EncounterDirector.Schedule(route,round,0,0,scratch,0,wave,previous,next,0,out proposed)==WaveAdmission.Skip,"expired wave skipped");
        round.Distance=330;
        Check(EncounterDirector.Schedule(route,round,0,1,scratch,0,wave,previous,next,0,out proposed)==WaveAdmission.Pending,"bend quiet zone defers hazards");
        int policeIndex=Array.FindIndex(route.Encounters,e=>e.Kind==EncounterKind.Police);
        round.Distance=980;round.Elapsed=40;round.Speed=24;
        Check(EncounterDirector.Schedule(route,round,0,policeIndex,scratch,0,wave,previous,next,60,out proposed)==WaveAdmission.Pending,"police cooldown");
        Check(EncounterDirector.Schedule(route,round,0,policeIndex,scratch,0,wave,previous,next,0,out proposed)==WaveAdmission.Ready && wave[0].Health==6 && wave[0].Z==-60,"rear pursuer activation");
        scratch[0]=wave[0];
        Check(EncounterDirector.Schedule(route,round,0,policeIndex,scratch,1,wave,previous,next,0,out proposed)==WaveAdmission.Pending,"one pursuer limit");
        round.Distance=700;round.LateralSpeed=RouteGeometry.CurveDrift(route,700,24);
        Check(EncounterDirector.Schedule(route,round,0,2,scratch,1,wave,previous,next,0,out proposed)==WaveAdmission.Pending,"lane changes suppressed during pursuit");
        bool signalled=false;
        for(double at=655;at<745 && !signalled;at+=10)
        {
            round.Distance=at;round.Elapsed=30;round.Speed=24;round.LateralSpeed=RouteGeometry.CurveDrift(route,at,24);
            var result=EncounterDirector.Schedule(route,round,0,2,scratch,0,wave,previous,next,0,out proposed);
            signalled=result==WaveAdmission.Ready && wave[0].Phase==ThreatPhase.Signalling;
        }
        Check(signalled,"first-circuit lane change actually admitted between bend quiet zones");
        var quietPolice=new RoadThreat{Active=true,Kind=ThreatKind.Police,Phase=ThreatPhase.Approach,Age=3,RouteDistance=270,Speed=24};
        Check(EncounterMotion.PoliceStep(route,ref quietPolice,300,0,24,.02f)==PursuitEvent.None && quietPolice.Phase==ThreatPhase.Approach,"pressure phase does not begin at bend entry");
        // Impossible wall, including swept paths, must be rejected.
        for (int i = 0; i < 3; i++) scratch[i] = new RoadThreat { Active = true, Kind = ThreatKind.Barricade, LaneOffset = (i-1)*5.2f, RouteDistance = 120 };
        Check(!EncounterMotion.SafeWave(route, 0, 0, 22, scratch, 3, 8, previous, next), "full road wall rejected");
        Check(EncounterMotion.Budget(0) == 4 && EncounterMotion.Budget(1) == 6 && EncounterMotion.Budget(100) == 8, "bounded difficulty");
        var traffic = new RoadThreat { Active = true, Kind = ThreatKind.Traffic, Speed = 20, StartLane = -5.2f, TargetLane = 0, LaneOffset = -5.2f, Phase = ThreatPhase.Signalling, SignalSeconds = 2, ChangeSeconds = 3 };
        EncounterMotion.TrafficStep(ref traffic, 1); Check(traffic.LaneOffset == -5.2f, "signal before moving");
        EncounterMotion.TrafficStep(ref traffic, 2.5f); Check(Mathf.Abs(traffic.LaneOffset + 2.6f) < .001f, "smooth lane midpoint");
        EncounterMotion.TrafficStep(ref traffic, 1.5f); Check(traffic.Phase == ThreatPhase.Cruise && traffic.LaneOffset == 0, "committed destination");
        var police = new RoadThreat { Active = true, Kind = ThreatKind.Police, RouteDistance = 940, Speed = 24, Phase = ThreatPhase.Approach };
        double distance = 1000; bool pursued = false;
        for (int i=0;i<1500;i++)
        {
            distance += 24*.02; var message = EncounterMotion.PoliceStep(route, ref police, distance, 0, 24, .02f);
            Check(police.RouteDistance <= distance - 21.999, "police never rams");
            if (message == PursuitEvent.Pursuing) { pursued = true; Check(!route.NearBendBoundary(distance), "quiet pressure entry"); }
            if (!police.Active) break;
        }
        Check(pursued && !police.Active, "bounded pursuit lifecycle");
        police = new RoadThreat { Active = true, Kind = ThreatKind.Police, RouteDistance = -60, Speed = 34, Phase = ThreatPhase.Approach };
        distance=0; PursuitEvent outcome=PursuitEvent.None;
        for(int i=0;i<600 && police.Active;i++) { distance+=42*.02; outcome=EncounterMotion.PoliceStep(route,ref police,distance,0,42,.02f); }
        Check(!police.Active && outcome == PursuitEvent.Escaped, "five-second escape");
        Check(Mathf.Abs(Mathf.DeltaAngle(GetawayRules.NormalizeYaw(179), GetawayRules.NormalizeYaw(181))-2)<.001, "yaw seam");
        Check(Mathf.Abs(Mathf.DeltaAngle(179,-179)-2)<.001, "controller yaw seam");
        var rear = new RoadThreat { Active=true, Kind=ThreatKind.Police, RouteDistance=940, Phase=ThreatPhase.Pursuing, Health=6, Age=3, EscapeTime=1, SignalSeconds=2, ChangeSeconds=3, TargetLane=5.2f };
        var muzzle=RouteGeometry.Muzzle(route,1000,0,0); var target=route.Sample(940).RelativeTo(route.Sample(1000))+Vector3.up;
        Check(RouteGeometry.Raycast(route,rear,1000,new Ray(muzzle,(target-muzzle).normalized),out var hit)&&hit>0,"rear police shot");
        var state = new RoundState { Yaw=-179, PoliceEvent=PursuitEvent.Escaped, PoliceEventUntil=12, RouteSeed=7919 };
        using(var writer=new FastBufferWriter(2048,Allocator.Temp))
        { writer.WriteNetworkSerializable(rear);writer.WriteNetworkSerializable(state);
            using(var reader=new FastBufferReader(writer,Allocator.Temp)) {reader.ReadNetworkSerializable(out RoadThreat copy);reader.ReadNetworkSerializable(out RoundState copiedState);Check(copy.Equals(rear)&&copiedState.Equals(state),"v3 serialization");} }
        EncounterMotion.TrafficStep(ref traffic,.02f); long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++) EncounterMotion.TrafficStep(ref traffic,.02f);
        Check(GC.GetAllocatedBytesForCurrentThread()==bytes,"traffic movement allocations");
        return $"PASS: {admitted} reachable candidates / {rejected} conservatively rejected; lane changes={changing}, staggered={staggered}; warning, scheduler deferral/skip/budgets/cooldown, wall rejection, movement, pursuit, escape, yaw, rear shots, v3 serialization, zero traffic-step allocations; {clock.ElapsedMilliseconds}ms";
    }
}
