using System;
using Getaway.Integration;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class DowntownVerification
{
    static void Check(bool pass, string message) { if (!pass) throw new Exception("Downtown verification: " + message); }
    public static string Geometry(RouteDefinition suppliedRoute = null)
    {
        var route = suppliedRoute != null ? suppliedRoute : AssetDatabase.LoadAssetAtPath<RouteDefinition>(DowntownRouteAuthoring.AssetPath);
        Check(route != null && route.IsBaked && route.DistrictCount == 1,"route asset");
        for(int i=1;i<route.Curves.Length;i++)
        {
            var a=route.Curves[i-1];var b=route.Curves[i];
            Check((a.P3-b.P0).sqrMagnitude<.000001f&&Vector3.Angle(a.P3-a.P2,b.P1-b.P0)<.05f,"authored Bezier endpoint/tangent");
        }
        foreach(var section in route.Sections)
        {
            var a=route.Sample(section.Start-.001);var b=route.Sample(section.Start+.001);
            Check(b.RelativeTo(a).magnitude<.003&&Quaternion.Angle(a.Rotation,b.Rotation)<.01,"authored section seam");
        }
        float maximumCurvature = 0;
        for (int i = 1; i < route.Points.Length; i++)
        {
            Check(route.Points[i].Distance-route.Points[i-1].Distance <= 1.001,"sample spacing");
            Check(Quaternion.Angle(route.Points[i-1].Rotation,route.Points[i].Rotation)<1,"continuous headings");
            maximumCurvature = Mathf.Max(maximumCurvature,Mathf.Abs(route.Points[i].Curvature));
        }
        Check(maximumCurvature <= 1f/120,"minimum bend radius");
        foreach (double distance in new[]{0d,100,300,500,600,800,1100,1300,1400,1600,route.Length,route.Length*1000000})
        {
            var a=route.Sample(distance-.001);var b=route.Sample(distance+.001);
            Check(b.RelativeTo(a).magnitude<.003 && Quaternion.Angle(a.Rotation,b.Rotation)<.01,"position/heading seam "+distance);
        }
        for(int seed=0;seed<128;seed++)for(int circuit=0;circuit<4;circuit++)
        {
            Check(EncounterDirector.Validate(route,seed,circuit,out var reason),reason);
            for(int i=0;i<route.Encounters.Length;i++)Check(EncounterDirector.Threat(route,seed,circuit,i,0).Equals(EncounterDirector.Threat(route,seed,circuit,i,0)),"encounter determinism");
        }
        var threat=new RoadThreat{Active=true,Kind=ThreatKind.Traffic,RouteDistance=100};
        Check(RouteGeometry.CrossesTruck(route,threat,threat,80,120,0,0),"forward sweep");
        Check(RouteGeometry.CrossesTruck(route,threat,threat,100,100,-5.2f,5.2f),"lateral sweep");
        threat.LaneOffset=5.2f;Check(!RouteGeometry.CrossesTruck(route,threat,threat,99,100,0,0),"adjacent lane");
        threat.LaneOffset=0;threat.RouteDistance=90.1;Check(RouteGeometry.CrossesTruck(route,threat,threat,100,100,0,0),"rear overhang");
        threat.RouteDistance=89.7;Check(!RouteGeometry.CrossesTruck(route,threat,threat,100,100,0,0),"already passed");
        foreach(double d in new[]{350d,700,1190,route.Length+400,10000000400d})
        {
            threat.RouteDistance=d;Check(RouteGeometry.CrossesTruck(route,threat,threat,d-.84,d,0,0),"curved collision");
            threat.RouteDistance=d+35;var goal=route.Sample(threat.RouteDistance).RelativeTo(route.Sample(d))+Vector3.up;
            var muzzle=RouteGeometry.Muzzle(route,d,0,0);var ray=new Ray(muzzle,(goal-muzzle).normalized);
            Check(RouteGeometry.Raycast(route,threat,d,ray,out var hit)&&hit>0,"curved target ray");
            Check(!RouteGeometry.Raycast(route,threat,d,new Ray(muzzle,-ray.direction),out _),"reverse ray miss");
        }
        // Fixed-step binary keyboard steering with look-ahead damping, not fractional hardware input.
        float x=0,velocity=0,maxX=0;
        for(double d=0;d<route.Length;d+=42*.02)
        {
            float steer=x+velocity*.35f>0?-1:1;
            velocity=Mathf.MoveTowards(velocity,steer*8,22*.02f);
            x+=(velocity-RouteGeometry.CurveDrift(route,d,42))*.02f;
            maxX=Mathf.Max(maxX,Mathf.Abs(x));Check(Mathf.Abs(x)<RouteGeometry.TruckLateralLimit(route,d),"keyboard curve authority");
        }
        route.Sample(1000);long allocated=GC.GetAllocatedBytesForCurrentThread();double checksum=0;
        for(int i=0;i<10000;i++)checksum+=route.Sample(i*50d).X;
        Check(GC.GetAllocatedBytesForCurrentThread()==allocated,"route sampling allocations");
        var state=new RoundState{RouteSeed=7919,Distance=10000000000,Hull=5,Round=2};
        using(var writer=new FastBufferWriter(1024,Allocator.Temp))
        {
            writer.WriteNetworkSerializable(state);writer.WriteNetworkSerializable(threat);
            using(var reader=new FastBufferReader(writer,Allocator.Temp))
            {reader.ReadNetworkSerializable(out RoundState copy);reader.ReadNetworkSerializable(out RoadThreat target);Check(copy.Equals(state)&&target.Equals(threat),"network route-coordinate round trip");}
        }
        return "PASS: route seams/radius, 512 deterministic authoring cases, oriented collision/rays, network round-trip, zero sample allocations; binary-keyboard max offset="+maxX.ToString("F3")+"m";
    }
    public static string Pool()
    {
        var route=AssetDatabase.LoadAssetAtPath<RouteDefinition>(DowntownRouteAuthoring.AssetPath);
        var art=AssetDatabase.LoadAssetAtPath<DowntownEnvironmentDefinition>(DowntownArtAuthoring.Root+"/DowntownEnvironment.asset");
        foreach(var module in art.Modules)Check(module!=null&&module.GetComponentsInChildren<Collider>(true).Length==0,"decorative modules");
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Pool verification");SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var environment=root.AddComponent<DowntownEnvironment>();environment.Initialize(route,art);environment.Present(0,7919);
            int count=root.GetComponentsInChildren<Transform>(true).Length;
            for(int i=1;i<=100;i++)environment.Present(i*180,7919);
            Check(count==root.GetComponentsInChildren<Transform>(true).Length&&environment.ActiveTiles==7,"bounded 18 km recycle");
            return "PASS: seven tiles; fixed "+count+" transforms over 18 km; zero scenery colliders";
        }
        finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static string Scene()
    {
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/IntegratedGetaway.unity");Check(scene.IsValid()&&scene.isLoaded,"open integration scene for reference checks");
        IntegratedWorld world=null;int eventSystems=0;
        foreach(var root in scene.GetRootGameObjects())
        {world=world!=null?world:root.GetComponentInChildren<IntegratedWorld>(true);eventSystems+=root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length;}
        Check(world!=null&&world.UseDowntownRoute&&world.Route!=null&&world.CityArtwork!=null,"enabled route and artwork");
        Check(world.TruckArtwork!=null&&world.ShooterArtwork!=null,"crew artwork references");
        Check(eventSystems==1,"single EventSystem");
        var art=world.CityArtwork;Check(art.Low.shadowDistance==40&&art.Balanced.shadowDistance==70&&art.Low.shadowCascadeCount==2&&art.Balanced.shadowCascadeCount==2,"quality settings");
        Check(!art.Low.supportsAdditionalLightShadows&&!art.Balanced.supportsAdditionalLightShadows,"additional shadows off");
        return "PASS: downtown enabled, crew art, one EventSystem, isolated quality assets";
    }
}
