using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Getaway.Integration
{
    /// <summary>Local cosmetic viewmodel. All geometry is built once; shots only animate cached objects.</summary>
    public sealed class ShooterWeaponView : MonoBehaviour
    {
        public Transform Muzzle { get; private set; }
        public Transform WorldWeapon { get; private set; }
        public Transform WorldMuzzle { get; private set; }
        public bool Visible { get; private set; }
        Transform model, flash;
        Mesh weaponMesh, flashMesh;
        Material[] palette;
        Material flashMaterial;
        float recoil, flashTime, lastYaw, lastPitch, aimBlend;
        Vector3 hipPosition;
        Quaternion hipRotation=Quaternion.identity;
        public static readonly Vector3 RearSightLocal=new Vector3(0,.114f,.07f), FrontSightLocal=new Vector3(0,.114f,.61f);
        public Vector3 RearSightPosition=>model.TransformPoint(RearSightLocal);
        public Vector3 FrontSightPosition=>model.TransformPoint(FrontSightLocal);
        bool posed;
        readonly List<Mesh> temporaryMeshes = new();
        static void Release(Object item) { if(item==null)return; if(Application.isPlaying)Destroy(item);else DestroyImmediate(item); }
        static Mesh Primitive(PrimitiveType type)
        {
            var go=GameObject.CreatePrimitive(type);go.SetActive(false);var mesh=go.GetComponent<MeshFilter>().sharedMesh;Release(go);return mesh;
        }
        public void Initialize()
        {
            if(model!=null)return;
            palette=new[]{MakeMaterial("Gunmetal",new Color(.12f,.145f,.16f)),MakeMaterial("Grip and gloves",new Color(.16f,.12f,.10f)),MakeMaterial("Machined edges",new Color(.37f,.41f,.43f))};
            var cube=Primitive(PrimitiveType.Cube);var cylinder=Primitive(PrimitiveType.Cylinder);
            var groups=new[]{new List<CombineInstance>(),new List<CombineInstance>(),new List<CombineInstance>()};
            void Box(int color,Vector3 at,Vector3 size,float tilt=0)=>groups[color].Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(at,Quaternion.Euler(tilt,0,0),size)});
            void Tube(int color,Vector3 at,float radius,float length)=>groups[color].Add(new CombineInstance{mesh=cylinder,transform=Matrix4x4.TRS(at,Quaternion.Euler(90,0,0),new Vector3(radius*2,length*.5f,radius*2))});
            Box(0,new Vector3(0,0,.18f),new Vector3(.105f,.105f,.33f)); // receiver
            Box(2,new Vector3(0,.064f,.18f),new Vector3(.07f,.02f,.31f)); // upper rail
            Box(1,new Vector3(0,-.025f,-.08f),new Vector3(.075f,.12f,.22f));
            Box(0,new Vector3(0,-.035f,-.205f),new Vector3(.085f,.16f,.035f)); // stock pad
            Box(1,new Vector3(0,-.105f,.095f),new Vector3(.065f,.16f,.075f),-18);
            Box(0,new Vector3(0,-.105f,.255f),new Vector3(.065f,.19f,.09f),12); // magazine
            Box(2,new Vector3(0,-.2f,.275f),new Vector3(.073f,.025f,.1f));
            Box(1,new Vector3(0,.005f,.405f),new Vector3(.10f,.09f,.19f)); // handguard
            for(int i=0;i<4;i++)Box(2,new Vector3(0,.058f,.34f+i*.04f),new Vector3(.105f,.014f,.012f));
            Tube(0,new Vector3(0,.025f,.575f),.022f,.18f);
            Tube(2,new Vector3(0,.025f,.69f),.028f,.065f);
            Tube(0,new Vector3(0,.025f,.728f),.018f,.014f); // dark muzzle opening
            // Open iron sights leave the centre of the target unobstructed.
            for(int side=-1;side<=1;side+=2)
            {Box(0,new Vector3(side*.027f,.1f,.07f),new Vector3(.012f,.065f,.025f));Box(0,new Vector3(side*.025f,.09f,.61f),new Vector3(.01f,.06f,.018f));}
            Box(2,new Vector3(0,.089f,.61f),new Vector3(.009f,.05f,.012f));
            Box(2,new Vector3(.061f,.018f,.21f),new Vector3(.018f,.045f,.10f)); // ejection port
            // Compact gloved hands suggest a grip without requiring another character rig.
            Box(1,new Vector3(.035f,-.09f,.075f),new Vector3(.08f,.075f,.1f),-12);
            Box(1,new Vector3(-.015f,-.05f,.415f),new Vector3(.10f,.07f,.12f),8);
            Box(1,new Vector3(.075f,-.16f,-.015f),new Vector3(.085f,.1f,.16f),-25);
            var combined=new CombineInstance[groups.Length];
            for(int i=0;i<groups.Length;i++){var mesh=new Mesh{name="Weapon material group"};mesh.CombineMeshes(groups[i].ToArray(),true,true);temporaryMeshes.Add(mesh);combined[i]=new CombineInstance{mesh=mesh,transform=Matrix4x4.identity};}
            weaponMesh=new Mesh{name="Integration carbine"};weaponMesh.CombineMeshes(combined,false,true);weaponMesh.RecalculateBounds();
            foreach(var mesh in temporaryMeshes)Release(mesh);temporaryMeshes.Clear();
            model=MeshObject("First-person carbine",transform,weaponMesh,palette);
            Muzzle=new GameObject("View muzzle").transform;Muzzle.SetParent(model,false);Muzzle.localPosition=new Vector3(0,.025f,.74f);
            flashMesh=MakeFlash();flashMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Muzzle flash",color=new Color(1,.8f,.25f)};
            flash=MeshObject("Muzzle flash",Muzzle,flashMesh,new[]{flashMaterial});flash.gameObject.SetActive(false);
            model.gameObject.SetActive(false);
        }
        static Material MakeMaterial(string name,Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Smoothness",.28f);m.SetFloat("_Metallic",name=="Grip and gloves"?0:.35f);return m;
        }
        static Transform MeshObject(string name,Transform parent,Mesh mesh,Material[] materials)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterials=materials;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return go.transform;
        }
        static Mesh MakeFlash()
        {
            var vertices=new Vector3[17];var triangles=new int[96];
            for(int i=0;i<16;i++){float angle=i*Mathf.PI/8;float radius=i%2==0?.11f:.035f;vertices[i+1]=new Vector3(Mathf.Sin(angle)*radius,Mathf.Cos(angle)*radius,0);int k=i*6,j=(i+1)%16+1;triangles[k]=0;triangles[k+1]=i+1;triangles[k+2]=j;triangles[k+3]=0;triangles[k+4]=j;triangles[k+5]=i+1;}
            var mesh=new Mesh{name="Muzzle burst"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        public void CreateWorld(Transform parent)
        {
            WorldWeapon=MeshObject("Shared carbine",parent,weaponMesh,palette);WorldWeapon.localPosition=new Vector3(0,GetawayRules.WeaponHeight,0);
            WorldMuzzle=new GameObject("World muzzle").transform;WorldMuzzle.SetParent(WorldWeapon,false);WorldMuzzle.localPosition=new Vector3(0,.025f,.74f);
        }
        public void Fire(){recoil=Mathf.Min(1,recoil+.8f);flashTime=.045f;}
        public void Pose(bool visible,bool aiming,float reload,float yaw,float pitch,float dt,Vector3 aimPointLocal=default)
        {
            Initialize();bool snap=!posed || (visible && !Visible);Visible=visible;model.gameObject.SetActive(visible);
            if(!posed || !visible){lastYaw=yaw;lastPitch=pitch;posed=true;}
            recoil=Mathf.MoveTowards(recoil,0,dt*7);flashTime=Mathf.Max(0,flashTime-dt);
            float dx=Mathf.Clamp(Mathf.DeltaAngle(lastYaw,yaw),-8,8),dy=Mathf.Clamp(pitch-lastPitch,-8,8);lastYaw=yaw;lastPitch=pitch;
            var rest=new Vector3(.22f,-.25f,.42f)+new Vector3(-dx*.0015f,dy*.0015f,-recoil*.045f);
            if(reload>0)rest+=new Vector3(.04f,-.10f,0);
            var hipTarget=Quaternion.Euler(-recoil*4+dy*.12f,reload>0?12:0,reload>0?-24:-dx*.12f);
            hipPosition=snap?rest:Vector3.Lerp(hipPosition,rest,1-Mathf.Exp(-18*dt));
            hipRotation=snap?hipTarget:Quaternion.Slerp(hipRotation,hipTarget,1-Mathf.Exp(-22*dt));
            bool aligned=aiming && reload<=0;
            aimBlend=snap?(aligned?1:0):Mathf.MoveTowards(aimBlend,aligned?1:0,dt*6);
            if(aimPointLocal.z<.1f)aimPointLocal=Vector3.forward*190;
            var sightDirection=aimPointLocal.normalized;
            var sightRotation=Quaternion.LookRotation(sightDirection,Vector3.up);
            // Both sight references lie on the camera-to-reticle ray. Axial recoil preserves
            // this alignment; cosmetic hip-fire sway must not redirect the aimed sights.
            var sightPosition=sightDirection*(.53f-recoil*.045f)-sightRotation*RearSightLocal;
            float blend=aimBlend*aimBlend*(3-2*aimBlend);
            model.localPosition=Vector3.Lerp(hipPosition,sightPosition,blend);
            model.localRotation=Quaternion.Slerp(hipRotation,sightRotation,blend);
            flash.gameObject.SetActive(visible && flashTime>0);flash.localScale=Vector3.one*(.7f+flashTime*8);
        }
        void OnDestroy()
        {
            if(WorldWeapon!=null)Release(WorldWeapon.gameObject);
            Release(weaponMesh);Release(flashMesh);if(palette!=null)foreach(var m in palette)Release(m);Release(flashMaterial);
        }
    }
    public static class ShotTrace
    {
        public const float Speed=900, Length=5;
        public static float Lifetime(Vector3 from,Vector3 to)=>Vector3.Distance(from,to)/Speed+.025f;
        public static void Sample(Vector3 from,Vector3 to,float age,out Vector3 tail,out Vector3 head,out float width)
        {
            var delta=to-from;float distance=delta.magnitude;var direction=distance>.001f?delta/distance:Vector3.forward;
            float travel=Mathf.Max(0,age)*Speed;
            head=from+direction*Mathf.Min(distance,travel+1);
            tail=from+direction*Mathf.Min(distance,Mathf.Max(0,travel+1-Length));
            width=Mathf.Clamp01((Lifetime(from,to)-age)/.025f);
        }
    }
}
