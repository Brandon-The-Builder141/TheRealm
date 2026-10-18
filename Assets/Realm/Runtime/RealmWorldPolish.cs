using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheRealm
{
    // Deterministic, non-solid dressing. Gameplay and quest coordinates retain their existing ground plane.
    public static class RealmWorldPolish
    {
        public static readonly string[] Assets={"GrassTuft_A","GrassTuft_B","FernPatch","DryScrub","FallenBranch","RootCluster","ForestFloorPatch","GravelPatch","MossPatch"};

        public const string RootName = "Pass five world polish";
        public static string GroundMaterialName(string model) => "P5_" + model.Replace("Patch", "");

        // Existing environment generation still enters here. The focused Editor action supplies
        // the saved campaign; default generation uses the same default quest definitions as the builder.
        public static GameObject Build(Transform parent, QuestDefinition[] quests = null)
        {
            if(parent == null) throw new ArgumentNullException(nameof(parent));
            var existing = parent.GetComponentsInChildren<Transform>(true).Where(t => t.name == RootName).ToArray();
            if(existing.Length > 1) throw new InvalidOperationException("Multiple polish roots exist. Resolve them manually before continuing.");
            if(existing.Length == 1) return existing[0].gameObject; // Preserve edits and identity on repeated execution.
            if(RealmArt.KitProvider == null) return null; // Preserve the primitive-only runtime fallback.
            var placement = new Placement(parent, quests ?? RealmCampaign.CreateDefaultQuests());
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            try { placement.Populate(root); return root; }
            catch { Discard(root); throw; }
        }

        static void Discard(GameObject obj)
        {
            if(Application.isPlaying) { obj.SetActive(false); UnityEngine.Object.Destroy(obj); }
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        public static Bounds BoundsOf(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length == 0) throw new InvalidOperationException("No renderers: " + obj.name);
            var bounds = renderers[0].bounds;
            foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        public static bool OverlapsXZ(Bounds a, Bounds b) =>
            a.min.x <= b.max.x && a.max.x >= b.min.x && a.min.z <= b.max.z && a.max.z >= b.min.z;

        // The authoring scene uses flat box surfaces. Fail on slopes instead of mistaking the
        // top of an arbitrary renderer AABB for a sampled terrain surface.
        public sealed class Placement
        {
            readonly Transform parent;
            readonly List<Bounds> surfaces = new List<Bounds>();
            readonly List<Bounds> exclusions = new List<Bounds>();
            readonly List<Bounds> roads = new List<Bounds>();
            readonly List<Bounds> treeTrunks = new List<Bounds>();
            readonly List<Bounds> patches = new List<Bounds>();

            public Placement(Transform parent, QuestDefinition[] quests)
            {
                this.parent = parent;
                if(quests == null) throw new ArgumentNullException(nameof(quests));
                foreach(var t in parent.GetComponentsInChildren<Transform>(true))
                {
                    if(t.name == RootName || t.GetComponentsInParent<Transform>(true).Any(p => p.name == RootName)) continue;
                    bool surface = t.name == "The frontier" || t.name == "The old road" || t.name == "Emberhearth street";
                    if(surface)
                    {
                        if(Vector3.Dot(t.up, Vector3.up) < .9999f || t.GetComponent<MeshFilter>() == null)
                            throw new InvalidOperationException("Unsupported ground surface: " + t.name);
                        surfaces.Add(BoundsOf(t.gameObject));
                    }
                    if(t.name == "The old road" || t.name == "Emberhearth street" || t.name == "StoneGate" || t.name == "RuinArch" || t.name == "SealDais")
                    {
                        var b = BoundsOf(t.gameObject);
                        b.Expand(new Vector3(1, 0, 2));
                        exclusions.Add(b);
                        if(t.name == "The old road" || t.name == "Emberhearth street") roads.Add(b);
                    }
                }
                if(!parent.GetComponentsInChildren<Transform>(true).Any(t => t.name == "The frontier"))
                    throw new InvalidOperationException("Missing named frontier surface; cannot ground dressing safely.");
                foreach(var q in quests)
                    exclusions.Add(new Bounds(q.location, new Vector3(10, 0, 10))); // Full five-metre interaction radius.
                for(int i = 0; i < 3; i++)
                    exclusions.Add(new Bounds(parent.TransformPoint(new Vector3(0,0,121+i*149)), new Vector3(26,0,26)));
                foreach(var c in parent.GetComponentsInChildren<Collider>(true))
                {
                    if(c.name == "The frontier" || c.isTrigger || !c.enabled) continue;
                    var b = c.bounds; b.Expand(new Vector3(.5f,0,.5f));
                    if(c.GetComponentsInParent<Transform>().Any(t => t.name == "Oak" || t.name == "Fir" || t.name == "Birch")) treeTrunks.Add(b);
                    else exclusions.Add(b);
                }
            }

            // Roots/moss may meet the existing trunk they dress; this never exempts roads or quest areas.
            public bool IsClear(Bounds bounds, bool treeBase = false) =>
                !exclusions.Any(b => OverlapsXZ(bounds,b)) && (treeBase || !treeTrunks.Any(b => OverlapsXZ(bounds,b)));

            public bool SampleSurface(Vector3 point, out float height)
            {
                height = float.NegativeInfinity;
                foreach(var b in surfaces)
                    if(point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z)
                        height = Mathf.Max(height,b.max.y);
                return !float.IsNegativeInfinity(height);
            }

            public bool FootprintHeight(Bounds bounds, out float height)
            {
                if(!SampleSurface(bounds.center,out height)) return false;
                foreach(float x in new[]{bounds.min.x,bounds.max.x})
                    foreach(float z in new[]{bounds.min.z,bounds.max.z})
                        if(!SampleSurface(new Vector3(x,0,z),out float corner) || Mathf.Abs(corner-height) > .005f) return false;
                return true;
            }

        public void Populate(GameObject root)
        {
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
                var p=parent.InverseTransformPoint(t.position);
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
        void Place(Transform group,string name,Vector3 position,System.Random rng,float scale,bool ground=false)
        {
            // Consume the original random draw even when a candidate is rejected, preserving the layout seed.
            if(ground) Range(rng,.008f,.020f);
            float yaw=Range(rng,0,360);
            var obj=RealmArt.KitProvider?.Invoke(name);
            if(obj==null) throw new InvalidOperationException("Missing Pass Five model: " + name);
            obj.name=name;
            obj.transform.SetParent(group,false);
            obj.transform.localPosition=position;
            obj.transform.localRotation=Quaternion.Euler(0,yaw,0);
            obj.transform.localScale=Vector3.one*scale;
            if(obj.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Pass Five assets must be non-colliding: " + name);
            var bounds=BoundsOf(obj);
            if(ground && group.name == "Road shoulders")
                foreach(var road in roads)
                    if(OverlapsXZ(bounds,road))
                    {
                        // Keep the seeded shoulder station, moving its full width beyond the travel surface.
                        float offset = bounds.center.x < road.center.x ? road.min.x-bounds.max.x-.05f : road.max.x-bounds.min.x+.05f;
                        obj.transform.position += Vector3.right*offset;
                        bounds=BoundsOf(obj);
                    }
            if(!IsClear(bounds,name == "RootCluster" || name == "MossPatch") || !FootprintHeight(bounds,out float height) || (ground && patches.Any(b => OverlapsXZ(bounds,b))))
            { Discard(obj); return; }
            // Sample the visible authoring surface, not RealmSession's logical y=0 movement plane.
            obj.transform.position += Vector3.up * (height - bounds.min.y + (ground ? .002f : 0));
            Material material=null;
            if(ground)
            {
                material=RealmArt.MaterialProvider?.Invoke(GroundMaterialName(name));
                if(material==null || material.shader==null || material.shader.name!="HDRP/Lit" || material.GetTexture("_BaseColorMap")==null || material.GetFloat("_AlphaCutoffEnable")!=1)
                    throw new InvalidOperationException("Configure ground material " + GroundMaterialName(name) + " before placement.");
                patches.Add(BoundsOf(obj));
            }
            foreach(var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                if(ground) renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
                if(renderer.sharedMaterials.Any(m=>m==null || m.shader==null || m.shader.name!="HDRP/Lit"))
                    throw new InvalidOperationException("Missing or non-HDRP material on " + name);
                renderer.shadowCastingMode=ground?ShadowCastingMode.Off:ShadowCastingMode.On;
                renderer.receiveShadows=true;
            }
        }
        }
    }
}
