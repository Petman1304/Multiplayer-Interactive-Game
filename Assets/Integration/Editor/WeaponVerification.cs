using System;
using Getaway.Integration;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WeaponVerification
{
    static void Check(bool value,string message){if(!value)throw new Exception("Weapon verification: "+message);}
    public static string Run()
    {
        if(UnityEditor.EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play for pure visual checks.");
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Weapon verification");SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var viewObject=new GameObject("Local weapon");viewObject.transform.SetParent(root.transform,false);
            var view=viewObject.AddComponent<ShooterWeaponView>();view.Initialize();view.CreateWorld(root.transform);
            view.Pose(true,false,0,0,4,1);
            Check(view.Visible && view.Muzzle.position.z>.5f,"hip-fire muzzle in front of camera");
            var geometry=view.GetComponentsInChildren<MeshFilter>(true);
            Check(geometry.Length==2 && geometry[0].sharedMesh.subMeshCount==3 && geometry[0].sharedMesh.vertexCount<5000,"bounded combined geometry");
            Check(view.WorldWeapon.GetComponent<MeshFilter>().sharedMesh==geometry[0].sharedMesh,"shared world/view mesh");
            Check(root.GetComponentsInChildren<Collider>(true).Length==0,"cosmetic weapon has no physics");
            int count=root.GetComponentsInChildren<Transform>(true).Length;
            for(int i=0;i<1000;i++){if(i%10==0)view.Fire();view.Pose(true,i%100<50,i%200<20?1:0,(i%360)-180,4,.016f);}
            Check(root.GetComponentsInChildren<Transform>(true).Length==count,"recoil and flash reuse objects");
            for(int i=0;i<120;i++)view.Pose(true,true,0,180,4,.016f);
            Check(Mathf.Abs(geometry[0].transform.localPosition.x)<.01f,"aim pose centres weapon");
            var cameraObject=new GameObject("Sight projection camera",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);
            var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.pixelRect=new Rect(0,0,1280,720);camera.nearClipPlane=.03f;
            viewObject.transform.SetParent(camera.transform,false);
            foreach(float yaw in new[]{0f,90,180,359})foreach(float range in new[]{10f,40,190})foreach(float aspect in new[]{4f/3,16f/9,21f/9})foreach(float fov in new[]{48f,75})
            {
                camera.fieldOfView=fov;camera.aspect=aspect;camera.transform.SetPositionAndRotation(new Vector3(10,4,50),Quaternion.Euler(-12,yaw,0));
                var aimLocal=new Vector3(.5f,-.12f,range);var target=camera.transform.TransformPoint(aimLocal);
                view.Pose(true,true,0,yaw,4,1,aimLocal);view.Fire();view.Pose(true,true,0,yaw+1,4,.016f,aimLocal);
                var targetPixel=camera.WorldToScreenPoint(target);var rearPixel=camera.WorldToScreenPoint(view.RearSightPosition);var frontPixel=camera.WorldToScreenPoint(view.FrontSightPosition);
                Check(Vector2.Distance(new Vector2(targetPixel.x,targetPixel.y),new Vector2(rearPixel.x,rearPixel.y))<.25f
                    && Vector2.Distance(new Vector2(targetPixel.x,targetPixel.y),new Vector2(frontPixel.x,frontPixel.y))<.25f,"sights and aim point agree through recoil, yaw, FOV and aspect changes");
            }
            view.Pose(false,false,0,0,4,.016f);Check(!view.Visible && !geometry[0].gameObject.activeSelf,"hidden when leaving shooter POV");
            var start=new Vector3(.2f,3.7f,1);var end=new Vector3(0,1,180);
            for(int i=0;i<100;i++)
            {
                ShotTrace.Sample(start,end,i*.003f,out var tail,out var head,out var width);
                Check(Vector3.Distance(tail,head)<=ShotTrace.Length+.001f,"bounded moving streak");
                Check(Vector3.Distance(start,head)<=Vector3.Distance(start,end)+.001f && width>=0 && width<=1,"streak never overshoots confirmed endpoint");
            }
            ShotTrace.Sample(start,end,.05f,out var a,out var b,out _);
            var shift=new Vector3(20,0,-40);ShotTrace.Sample(start+shift,end+shift,.05f,out var translatedA,out var translatedB,out _);
            Check((translatedA-a-shift).magnitude<.001f && (translatedB-b-shift).magnitude<.001f,"floating-origin translation");
            ShotTrace.Sample(start,start,0,out a,out b,out _);Check(GetawayRules.Finite(a.x)&&GetawayRules.Finite(b.z),"zero-length shot");
            ShotTrace.Sample(start,end,1,out a,out b,out float expired);Check(expired==0 && a==end && b==end,"trace expires at endpoint");
            ShotTrace.Sample(start,end,0,out _,out _,out _);long bytes=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<10000;i++)ShotTrace.Sample(start,end,.05f,out _,out _,out _);
            Check(GC.GetAllocatedBytesForCurrentThread()==bytes,"allocation-free streak sampling");
            return "PASS: visible muzzle, three shared mesh/material groups, zero colliders, stable object count across 100 shots / 1,000 animation steps, aim/role visibility, 144 sight projections within 0.25 pixels during recoil, 5 m streak bound, endpoints, floating origin, zero-length shots, zero trace-sampling allocations";
        }
        finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
