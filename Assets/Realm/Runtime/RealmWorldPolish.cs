using UnityEngine;
using UnityEngine.Rendering;

namespace TheRealm
{
    // Deterministic, non-solid dressing. Gameplay and quest coordinates retain their existing ground plane.
    public static class RealmWorldPolish
    {
        public static readonly string[] Assets={"GrassTuft_A","GrassTuft_B","FernPatch","DryScrub","FallenBranch","RootCluster","ForestFloorPatch","GravelPatch","MossPatch"};

        public static void Build(Transform parent)
        {
            if(RealmArt.KitProvider==null)return;
            var root=new GameObject("Pass five world polish"); root.transform.SetParent(parent,false);
            var rng=new System.Random(51027);
            var shoulders=Group(root.transform,"Road shoulders");
            for(float z=22;z<445;z+=9)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    float pz=z+Range(rng,-2,2);
                    // Raised seal platforms remain readable and free of foliage.
                    if(NearSeal(pz,13))continue;
                    bool forest=pz<150;
                    var p=new Vector3(side*Range(rng,4.2f,5.3f),0,pz);
                    Place(shoulders,forest?"ForestFloorPatch":"GravelPatch",p,rng,Range(rng,.65f,1.1f),true);
                    Place(shoulders,forest?"GrassTuft_A":"GrassTuft_B",new Vector3(side*Range(rng,5.1f,6.5f),0,pz+Range(rng,-1.5f,1.5f)),rng,Range(rng,.8f,1.35f));
                    if(!forest && (int)z%3==1)Place(shoulders,"DryScrub",new Vector3(side*Range(rng,8,10),0,pz+2),rng,.75f);
                }
            }

            var woodland=Group(root.transform,"Woodland floor clusters");
            for(float z=34;z<146;z+=12)
                for(int side=-1;side<=1;side+=2)
                {
                    if(NearSeal(z,14))continue;
                    var p=new Vector3(side*Range(rng,8,13),0,z+Range(rng,-3,3));
                    Place(woodland,"ForestFloorPatch",p,rng,1.1f,true);
                    Place(woodland,"FernPatch",p+new Vector3(side*1.5f,0,1.1f),rng,.9f);
                    Place(woodland,"GrassTuft_A",p+new Vector3(-1.2f,0,-1),rng,1.2f);
                    Place(woodland,"GrassTuft_B",p+new Vector3(.8f,0,-1.6f),rng,1);
                    Place(woodland,"FallenBranch",p+new Vector3(side*2,0,-2.2f),rng,.9f);
                }
            // Anchor existing trees with roots and ground cover instead of a uniform random scatter.
            int tree=0;
            foreach(var t in parent.GetComponentsInChildren<Transform>())
            {
                if(t.name!="Oak" && t.name!="Fir" && t.name!="Birch")continue;
                if(tree++%2!=0 || t.localPosition.z<22)continue;
                var p=t.localPosition;
                Place(woodland,"MossPatch",p,rng,.8f,true);
                Place(woodland,"RootCluster",p+new Vector3(.4f,0,.2f),rng,1);
                Place(woodland,"FernPatch",p+new Vector3(1.6f,0,-1.2f),rng,.75f);
            }

            var ruins=Group(root.transform,"Ruins and cliff footings");
            var sites=new[]{164f,201f,230f,290f,320f,350f,380f,437f};
            for(int i=0;i<sites.Length;i++)
            {
                float side=i%2==0?-1:1,z=sites[i];
                Place(ruins,"GravelPatch",new Vector3(side*20,0,z),rng,1.2f,true);
                Place(ruins,"GravelPatch",new Vector3(side*24,0,z+5),rng,1.3f,true);
                Place(ruins,"MossPatch",new Vector3(side*18,0,z-3),rng,.65f,true);
                Place(ruins,"DryScrub",new Vector3(side*23,0,z-2),rng,1.1f);
                Place(ruins,"GrassTuft_B",new Vector3(side*17,0,z-2.2f),rng,1.3f);
                Place(ruins,"FallenBranch",new Vector3(side*25,0,z+2),rng,1.2f);
            }
            for(float z=155;z<445;z+=18)
                for(int side=-1;side<=1;side+=2)
                {
                    Place(ruins,"GravelPatch",new Vector3(side*27,0,z+Range(rng,-3,3)),rng,1.3f,true);
                    Place(ruins,"DryScrub",new Vector3(side*25,0,z+5),rng,Range(rng,.7f,1.2f));
                }

            var village=Group(root.transform,"Village verge");
            for(float z=-10;z<=16;z+=5.5f)
                for(int side=-1;side<=1;side+=2)
                {
                    Place(village,"MossPatch",new Vector3(side*10,0,z),rng,.65f,true);
                    Place(village,"GrassTuft_A",new Vector3(side*10.5f,0,z-1),rng,.8f);
                    Place(village,"GrassTuft_B",new Vector3(side*17.8f,0,z+1),rng,.85f);
                }
        }
        static bool NearSeal(float z,float margin)
        {
            for(int i=0;i<3;i++)if(Mathf.Abs(z-(121+i*149))<margin)return true;
            return false;
        }
        static Transform Group(Transform parent,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;
        }
        static float Range(System.Random rng,float min,float max)=>min+(float)rng.NextDouble()*(max-min);
        static void Place(Transform parent,string name,Vector3 position,System.Random rng,float scale,bool ground=false)
        {
            var obj=RealmArt.KitProvider?.Invoke(name);if(obj==null)return;
            obj.transform.SetParent(parent,false);
            // Slightly separate overlapping ground patches to prevent coincident depth surfaces.
            position.y=ground?Range(rng,.008f,.020f):0;
            obj.transform.localPosition=position;
            obj.transform.localRotation=Quaternion.Euler(0,Range(rng,0,360),0);
            obj.transform.localScale=Vector3.one*scale;
            foreach(var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode=ground?ShadowCastingMode.Off:ShadowCastingMode.On;
                renderer.receiveShadows=true;
            }
        }
    }
}
