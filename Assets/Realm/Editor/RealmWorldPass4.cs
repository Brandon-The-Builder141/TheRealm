using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheRealm.Editor
{
    public static class RealmWorldPass4
    {
        public static readonly string[] Assets={"Cliff_A","Cliff_B","Cliff_C","RockShelf","ScreeCluster","RuinWall_A","RuinWall_B","RuinArch","RuinPillar","RuinStairs","RuinRubble"};

        [MenuItem("The Realm/Apply World Pass Four")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before applying world assets.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=RealmBuilder.ScenePath || scene.isDirty)throw new InvalidOperationException("Open the saved TheRealm scene before applying this pass.");
            foreach(string name in Assets)if(RealmBuilder.LoadWorldKit(name)==null)throw new InvalidOperationException("Missing pass-four model: "+name);
            var old=scene.GetRootGameObjects().Single(o=>o.name=="The Frontier — original modular environment");
            var replacement=new GameObject(old.name);
            replacement.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);
            replacement.transform.localScale=old.transform.localScale;
            try
            {
                RealmArt.ResetCache();
                foreach(string file in Directory.GetFiles("Assets/Realm/Art/Materials","*.mat"))
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(file.Replace('\\','/'));
                    if(material!=null)RealmArt.RegisterMaterial(material.name,material);
                }
                RealmArt.KitProvider=name=>
                {
                    var model=RealmBuilder.LoadWorldKit(name);
                    return model!=null?(GameObject)PrefabUtility.InstantiatePrefab(model):null;
                };
                RealmArt.MaterialProvider=set=>AssetDatabase.LoadAssetAtPath<Material>(RealmWorldImport.MaterialFolder+set+".mat");
                RealmArt.BuildEnvironment(replacement.transform);
                foreach(var fire in replacement.GetComponentsInChildren<Light>())
                    if(fire.name=="Hearth fire light"){fire.lightUnit=UnityEngine.Rendering.LightUnit.Lumen;fire.intensity=9000;fire.shadows=LightShadows.Soft;}
                foreach(var item in RealmArt.Materials)
                    if(!AssetDatabase.Contains(item.Value))AssetDatabase.CreateAsset(item.Value,"Assets/Realm/Art/Materials/"+item.Key+".mat");
                PrefabUtility.SaveAsPrefabAsset(replacement,"Assets/Realm/Art/Prefabs/FrontierEnvironment.prefab",out bool success);
                if(!success)throw new IOException("Could not save environment prefab.");
                Undo.RegisterCreatedObjectUndo(replacement,"Apply world pass four");
                Undo.DestroyObjectImmediate(old);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[Realm Pass4] Environment replaced; gameplay, lighting and campaign objects preserved.");
            }
            catch
            {
                if(old!=null && replacement!=null)UnityEngine.Object.DestroyImmediate(replacement);
                throw;
            }
            finally {RealmArt.KitProvider=null;RealmArt.MaterialProvider=null;}
        }

        [MenuItem("The Realm/Validate World Pass Four")]
        public static void Validate()
        {
            var report=new StringBuilder(); int failures=0;
            foreach(string name in Assets)
            {
                var asset=RealmBuilder.LoadWorldKit(name);
                if(asset==null){report.AppendLine("FAIL missing "+name);failures++;continue;}
                int triangles=0;
                foreach(var filter in asset.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;
                    if(mesh==null){failures++;continue;}
                    for(int s=0;s<mesh.subMeshCount;s++)triangles+=(int)mesh.GetIndexCount(s)/3;
                }
                foreach(var renderer in asset.GetComponentsInChildren<Renderer>())
                    foreach(var material in renderer.sharedMaterials)
                        if(material==null || material.shader==null || material.shader.name!="HDRP/Lit" || !AssetDatabase.GetAssetPath(material).StartsWith(RealmWorldImport.MaterialFolder))
                        {report.AppendLine("FAIL material "+name);failures++;}
                report.AppendLine(name+" triangles="+triangles);
            }
            Physics.SyncTransforms();
            // Existing seal pillars occupy the exact road centre; verify a continuous lane two metres to its side.
            if(Physics.CheckCapsule(new Vector3(2,.85f,20),new Vector3(2,.85f,443),.3f,1<<8,QueryTriggerInteraction.Ignore))
            {report.AppendLine("FAIL continuous road lane blocked");failures++;}
            var campaign=AssetDatabase.LoadAssetAtPath<RealmCampaign>("Assets/Realm/Content/FrontierCampaign.asset");
            foreach(var quest in campaign.quests)
                if(Physics.CheckSphere(quest.location+Vector3.up*.85f,.3f,1<<8,QueryTriggerInteraction.Ignore))
                {report.AppendLine("FAIL quest blocked "+quest.id);failures++;}
            foreach(var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if(transform.name=="Broken escarpment"){report.AppendLine("FAIL primitive escarpment remains");failures++;}
                    if(transform.name=="RuinArch" && Physics.CheckSphere(transform.position+Vector3.up*.85f,.3f,1<<8,QueryTriggerInteraction.Ignore))
                    {report.AppendLine("FAIL ruin arch opening blocked at "+transform.position);failures++;}
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)>0){report.AppendLine("FAIL missing script "+transform.name);failures++;}
                }
            report.AppendLine("Failures="+failures);
            Directory.CreateDirectory("Logs/Realm");File.WriteAllText("Logs/Realm/pass4-validation.txt",report.ToString());
            if(failures>0)throw new InvalidOperationException(report.ToString());
            Debug.Log("[Realm Pass4] Model, material, road and quest-clearance checks passed.");
        }
    }
}
