using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace TheRealm.Editor
{
    // First imported-art pass: bounded decorative clusters, using the existing world clearance sampler.
    public static class RealmImportedWorldProps
    {
        const string RootName = "Imported world props — camps and graves";
        const string Survival = "Assets/kenney_survival-kit/Models/FBX format/";
        const string Graveyard = "Assets/kenney_graveyard-kit_5.0/Models/FBX format/";
        const string Materials = "Assets/Realm/Art/World/Materials/";
        static readonly (string model, Vector3 offset, float width, float yaw)[] Camp = {
            ("tent-canvas",new Vector3(0,0,1.8f),2.8f,180),
            ("bedroll",new Vector3(-2.5f,0,0),.85f,15),
            ("bedroll",new Vector3(2.5f,0,0),.85f,-15),
            ("campfire-pit",new Vector3(0,0,-1.7f),1.1f,0),
            ("barrel",new Vector3(2.8f,0,2.2f),.7f,0),
            ("box",new Vector3(-2.8f,0,2.2f),.8f,10),
            ("resource-wood",new Vector3(-2.7f,0,-2.1f),1.2f,90)
        };
        static readonly (string model, Vector3 offset, float width, float yaw)[] Graves = {
            ("gravestone-round",new Vector3(-2,0,0),.7f,0),
            ("gravestone-bevel",new Vector3(0,0,0),.7f,-8),
            ("gravestone-broken",new Vector3(2,0,0),.7f,12),
            ("gravestone-wide",new Vector3(-2,0,3),.9f,0),
            ("gravestone-decorative",new Vector3(0,0,3),.8f,0),
            ("gravestone-round",new Vector3(2,0,3),.7f,5),
            ("bench-damaged",new Vector3(0,0,-3),1.7f,180)
        };
        static readonly (string model, Vector3 offset, float width, float yaw)[] Supplies = {
            ("barrel",new Vector3(0,0,0),.65f,0),
            ("box",new Vector3(.85f,0,.4f),.7f,12),
            ("resource-wood",new Vector3(-1,0,.4f),1.4f,80)
        };
        static readonly (string model, Vector3 offset, float width, float yaw)[] Memorial = {
            ("gravestone-broken",new Vector3(0,0,0),.65f,-12),
            ("gravestone-round",new Vector3(1.6f,0,.8f),.6f,8),
            ("bench-damaged",new Vector3(-1.5f,0,-1.8f),1.4f,65)
        };

        [MenuItem("The Realm/Imported Assets/Redistribute Dressing Across Map")]
        public static void Redistribute()
        {
            var parent=Environment();
            var root=parent.Find(RootName);
            if(root==null) { Apply(); root=parent.Find(RootName); }
            var sampler=new RealmWorldPolish.Placement(parent,Quests());
            var campMaterial=AssetDatabase.LoadAssetAtPath<Material>(Materials+"Imported_Kenney_Survival.mat");
            var graveMaterial=AssetDatabase.LoadAssetAtPath<Material>(Materials+"Imported_Kenney_Graveyard.mat");
            if(campMaterial==null || graveMaterial==null) throw new InvalidOperationException("Missing imported palette materials.");
            // Build the complete replacement before touching the existing imported dressing.
            var staging=new GameObject("__ImportedLayoutStaging"); staging.transform.SetParent(parent,false);
            var centres=new List<Vector3>();
            int undo=-1;
            try
            {
                var sites=new[]{
                    ("Village wood stores",-11f,6f,2),
                    ("Forest trail camp",16f,48f,0),
                    ("Old woodland graves",-18f,99f,1),
                    ("Woodcutters rest",-17f,61f,2),
                    ("Border road supplies",12f,151f,2),
                    ("Ruined parish graves",-16f,198f,1),
                    ("Caravan overnight camp",17f,223f,0),
                    ("Roadside remembrance",-12f,246f,3),
                    ("Abandoned supply stop",16f,294f,2),
                    ("Sunless burial ground",-17f,331f,1),
                    ("Last expedition camp",17f,367f,0),
                    ("Fallen travellers",-13f,391f,3)
                };
                foreach(var site in sites)
                {
                    var candidates=new List<Vector3>();
                    foreach(float dz in new[]{0f,-4f,4f,-8f,8f,-12f,12f})
                        foreach(float dx in new[]{0f,-2f,2f,-4f,4f})
                        {
                            var p=new Vector3(site.Item2+dx,0,site.Item3+dz);
                            if(p.z>=-11 && p.z<=440 && centres.All(c=>Vector3.Distance(c,p)>23)) candidates.Add(p);
                        }
                    var props=site.Item4==0?Camp:site.Item4==1?Graves:site.Item4==2?Supplies:Memorial;
                    bool burial=site.Item4==1 || site.Item4==3;
                    int before=staging.transform.childCount;
                    AddCluster(staging.transform,site.Item1,burial?Graveyard:Survival,props,
                        burial?graveMaterial:campMaterial,sampler,candidates.ToArray(),false,site.Item2<0?75:255);
                    if(staging.transform.childCount>before) centres.Add(staging.transform.GetChild(before).position);
                }
                if(centres.Count<9) throw new InvalidOperationException("Insufficient clear sites for a distributed map: "+centres.Count);
                Undo.IncrementCurrentGroup(); undo=Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Redistribute imported world dressing");
                foreach(Transform old in root.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(old.gameObject);
                foreach(Transform cluster in staging.transform.Cast<Transform>().ToArray())
                {
                    cluster.SetParent(root,true);
                    Undo.RegisterCreatedObjectUndo(cluster.gameObject,"Place distributed dressing");
                }
                Undo.RegisterCompleteObjectUndo(campMaterial,"Blend imported palette with world");
                Undo.RegisterCompleteObjectUndo(graveMaterial,"Blend imported palette with world");
                campMaterial.SetColor("_BaseColor",new Color(.72f,.77f,.70f));
                graveMaterial.SetColor("_BaseColor",new Color(.72f,.75f,.70f));
                EditorUtility.SetDirty(campMaterial); EditorUtility.SetDirty(graveMaterial);
                Undo.CollapseUndoOperations(undo);
                EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
                Validate();
                AssetDatabase.SaveAssetIfDirty(campMaterial);
                AssetDatabase.SaveAssetIfDirty(graveMaterial);
                Debug.Log("[Realm Imported] Distributed "+centres.Count+" distinct sites across the map; all at least 23m apart. Unsaved.");
            }
            catch {if(undo>=0) Undo.RevertAllDownToGroup(undo);throw;}
            finally {UnityEngine.Object.DestroyImmediate(staging);}
        }

        static Transform Environment()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path != RealmBuilder.ScenePath || PrefabStageUtility.GetCurrentPrefabStage()!=null)
                throw new InvalidOperationException("Open TheRealm scene outside Play Mode and Prefab Mode.");
            return scene.GetRootGameObjects().Single(o=>o.name=="The Frontier — original modular environment").transform;
        }

        static QuestDefinition[] Quests() => AssetDatabase.LoadAssetAtPath<RealmCampaign>("Assets/Realm/Content/FrontierCampaign.asset").quests;

        static Material Palette(string folder, string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Textures/colormap.png");
            if(texture==null) throw new InvalidOperationException("Missing palette: "+folder);
            string path=Materials+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null)
            {
                material=new Material(Shader.Find("HDRP/Lit")){name=name};
                AssetDatabase.CreateAsset(material,path);
                Undo.RegisterCreatedObjectUndo(material,"Create imported palette material");
            }
            else Undo.RegisterCompleteObjectUndo(material,"Configure imported palette material");
            material.SetTexture("_BaseColorMap",texture);
            material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Metallic",0);
            material.SetFloat("_Smoothness",.25f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        [MenuItem("The Realm/Imported Assets/Audit Camp and Graveyard Models")]
        public static void Audit()
        {
            var lines=new List<string>();
            foreach(var entry in Camp.Select(p=>(folder:Survival,name:p.model)).Concat(Graves.Select(p=>(folder:Graveyard,name:p.model))).Distinct())
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(entry.folder+entry.name+".fbx");
                if(model==null) throw new InvalidOperationException("Model not imported: "+entry.name);
                if(model.GetComponentsInChildren<Collider>(true).Length!=0 || model.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)
                    throw new InvalidOperationException("Unexpected behaviour/collider in decorative source: "+entry.name);
                var bounds=RealmWorldPolish.BoundsOf(model);
                if(bounds.size.x<=0) throw new InvalidOperationException("Invalid model bounds: "+entry.name);
                lines.Add(entry.name+" bounds="+bounds+" materials="+string.Join(",",model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Select(m=>m==null?"MISSING":m.name+"/"+m.shader.name)));
            }
            Directory.CreateDirectory("Logs/Realm");
            File.WriteAllLines("Logs/Realm/imported-world-audit.txt",lines);
            Debug.Log("[Realm Imported] "+string.Join("\n",lines));
        }

        [MenuItem("The Realm/Imported Assets/Apply Camp and Graveyard Dressing")]
        public static void Apply()
        {
            var parent=Environment();
            if(parent.Find(RootName)!=null) { Validate(); Debug.Log("[Realm Imported] Existing dressing retained."); return; }
            Audit();
            var sampler=new RealmWorldPolish.Placement(parent,Quests());
            Undo.IncrementCurrentGroup(); int undo=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add imported camp and graveyard dressing");
            GameObject root=null;
            try
            {
                var campMaterial=Palette(Survival,"Imported_Kenney_Survival");
                var graveMaterial=Palette(Graveyard,"Imported_Kenney_Graveyard");
                root=new GameObject(RootName);root.transform.SetParent(parent,false);
                // Try whole clusters, not disconnected individual props; reject occupied locations.
                AddCluster(root.transform,"Wayfarer camp",Survival,Camp,campMaterial,sampler,
                    new[]{new Vector3(17,0,48),new Vector3(-18,0,56),new Vector3(18,0,78),new Vector3(-18,0,78)});
                AddCluster(root.transform,"Forgotten graves",Graveyard,Graves,graveMaterial,sampler,
                    new[]{new Vector3(-18,0,95),new Vector3(19,0,100),new Vector3(-18,0,70),new Vector3(18,0,137)});
                Validate();
                Undo.RegisterCreatedObjectUndo(root,"Add imported world props");
                Undo.CollapseUndoOperations(undo);
                EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
                Debug.Log("[Realm Imported] Camp and graveyard added; scene left unsaved for review.");
            }
            catch
            {
                if(root!=null) UnityEngine.Object.DestroyImmediate(root);
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        static void AddCluster(Transform parent,string name,string folder,(string model,Vector3 offset,float width,float yaw)[] props,
            Material material,RealmWorldPolish.Placement sampler,Vector3[] candidates,bool required=true,float yaw=0)
        {
            foreach(var centre in candidates)
            {
                var cluster=new GameObject(name);cluster.transform.SetParent(parent,false);
                cluster.transform.localPosition=centre;
                cluster.transform.localRotation=Quaternion.Euler(0,yaw,0);
                int seed=17; foreach(char c in name) seed=unchecked(seed*31+c);
                var random=new System.Random(seed);
                bool valid=true;
                foreach(var prop in props)
                {
                    var model=AssetDatabase.LoadAssetAtPath<GameObject>(folder+prop.model+".fbx");
                    var obj=UnityEngine.Object.Instantiate(model,cluster.transform);obj.name=prop.model;
                    obj.transform.localPosition=prop.offset+new Vector3((float)(random.NextDouble()-.5)*.45f,0,(float)(random.NextDouble()-.5)*.45f);
                    obj.transform.localRotation=Quaternion.identity;
                    obj.transform.localScale=Vector3.one;
                    float width=RealmWorldPolish.BoundsOf(obj).size.x;
                    obj.transform.localScale=Vector3.one*(prop.width/width)*(float)(.93+random.NextDouble()*.14);
                    obj.transform.localRotation=Quaternion.Euler(0,prop.yaw+(float)(random.NextDouble()-.5)*16,0);
                    var bounds=RealmWorldPolish.BoundsOf(obj);
                    if(!sampler.IsClear(bounds) || !sampler.FootprintHeight(bounds,out float height)) { valid=false;break; }
                    obj.transform.position+=Vector3.up*(height-bounds.min.y);
                    foreach(var r in obj.GetComponentsInChildren<Renderer>(true))
                        r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();
                }
                if(valid)
                {
                    AddGroundCover(cluster.transform,sampler);
                    Debug.Log("[Realm Imported] "+name+" at "+centre);return;
                }
                UnityEngine.Object.DestroyImmediate(cluster);
            }
            if(required) throw new InvalidOperationException("No clear location for "+name+". No partial pass retained.");
        }

        static void AddGroundCover(Transform cluster,RealmWorldPolish.Placement sampler)
        {
            var environment=cluster.parent.parent;
            var existingPatches=environment.GetComponentsInChildren<Transform>(true)
                .Where(t=>new[]{"ForestFloorPatch","GravelPatch","MossPatch"}.Contains(t.name) &&
                    !t.GetComponentsInParent<Transform>(true).Any(p=>p.name==RootName))
                .Select(t=>RealmWorldPolish.BoundsOf(t.gameObject)).ToArray();
            var details=new[]{
                ("ForestFloorPatch",Vector3.zero,1.15f),
                ("GrassTuft_A",new Vector3(-3.6f,0,2.4f),1.1f),
                ("GrassTuft_B",new Vector3(3.7f,0,-2.7f),.95f),
                ("GrassTuft_A",new Vector3(-3.9f,0,-2.1f),.85f),
                ("FernPatch",new Vector3(3.8f,0,3.2f),.7f)
            };
            foreach(var detail in details)
            {
                bool ground=detail.Item1=="ForestFloorPatch";
                string name=ground && cluster.position.z>150?"GravelPatch":detail.Item1;
                var model=RealmBuilder.LoadWorldKit(name);
                if(model==null) throw new InvalidOperationException("Missing existing ground cover: "+name);
                var obj=UnityEngine.Object.Instantiate(model,cluster);obj.name=name;
                obj.transform.localPosition=detail.Item2;
                obj.transform.localRotation=Quaternion.Euler(0,37,0);
                obj.transform.localScale=Vector3.one*detail.Item3;
                var bounds=RealmWorldPolish.BoundsOf(obj);
                if(!sampler.IsClear(bounds) || !sampler.FootprintHeight(bounds,out float y) ||
                    (ground && existingPatches.Any(b=>RealmWorldPolish.OverlapsXZ(b,bounds))))
                {UnityEngine.Object.DestroyImmediate(obj);continue;}
                obj.transform.position+=Vector3.up*(y-bounds.min.y+(ground?.002f:0));
                if(ground)
                {
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(Materials+RealmWorldPolish.GroundMaterialName(name)+".mat");
                    foreach(var renderer in obj.GetComponentsInChildren<Renderer>())
                    {
                        renderer.sharedMaterial=mat;
                        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }
        }

        [MenuItem("The Realm/Imported Assets/Validate Camp and Graveyard Dressing")]
        public static void Validate()
        {
            var parent=Environment();
            var root=parent.Find(RootName);
            if(root==null || root.childCount<2) throw new InvalidOperationException("Expected populated dressing groups.");
            var sampler=new RealmWorldPolish.Placement(parent,Quests());
            if(root.GetComponentsInChildren<Collider>(true).Length!=0) throw new InvalidOperationException("Dressing must be non-colliding.");
            int count=0;
            foreach(Transform cluster in root)
                foreach(Transform item in cluster)
                {
                    var b=RealmWorldPolish.BoundsOf(item.gameObject);
                    if(!sampler.IsClear(b) || !sampler.FootprintHeight(b,out float height) || Mathf.Abs(height-b.min.y)>.003f)
                        throw new InvalidOperationException("Imported prop clearance/grounding failed: "+item.name);
                    foreach(var r in item.GetComponentsInChildren<Renderer>(true))
                        if(r.sharedMaterials.Any(m=>m==null || m.shader.name!="HDRP/Lit" || m.GetTexture("_BaseColorMap")==null))
                            throw new InvalidOperationException("Imported prop material failed: "+item.name);
                    count++;
                }
            if(count<14) throw new InvalidOperationException("Incomplete imported prop groups.");
            if(root.childCount>2)
            {
                var groups=root.Cast<Transform>().ToArray();
                for(int i=0;i<groups.Length;i++) for(int j=i+1;j<groups.Length;j++)
                    if(Vector3.Distance(groups[i].position,groups[j].position)<23)
                        throw new InvalidOperationException("Dressing sites crowded together.");
            }
            Debug.Log("[Realm Imported] Validated "+count+" props, zero colliders, HDRP palettes, clear roads/quests/gates/seals.");
        }

        [MenuItem("The Realm/Imported Assets/Render Camp and Graveyard Comparison")]
        public static void Preview()
        {
            Validate();
            var root=Environment().Find(RootName);
            var views=new List<(string name,Vector3 position,Vector3 target)>();
            foreach(Transform cluster in root.Cast<Transform>().Where((t,i)=>root.childCount<=2 || new[]{1,2,5,10}.Contains(i)))
            {
                var p=cluster.position;
                views.Add((cluster.name+" approach",p+new Vector3(7,3,-9),p+Vector3.up));
                views.Add((cluster.name+" front",p+new Vector3(0,1.7f,-7),p+Vector3.up*.7f));
                if(root.childCount<=2)
                {
                    views.Add((cluster.name+" detail",p+new Vector3(-5,2,-3),p+Vector3.up*.6f));
                    views.Add((cluster.name+" overview",p+new Vector3(5,10,5),p));
                }
            }
            bool active=root.gameObject.activeSelf;
            try
            {
                root.gameObject.SetActive(false);
                RealmWorldPreview.RenderViews(views.ToArray(),"Logs/Realm/imported-clusters-before.png");
                root.gameObject.SetActive(active);
                RealmWorldPreview.RenderViews(views.ToArray(),"Logs/Realm/imported-clusters-after.png");
            }
            finally {root.gameObject.SetActive(active);}
        }
    }
}
