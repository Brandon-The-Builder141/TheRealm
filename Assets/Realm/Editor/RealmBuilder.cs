using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace TheRealm.Editor
{
    public static class RealmBuilder
    {
        public const string ScenePath="Assets/Realm/Scenes/TheRealm.unity";
        [MenuItem("The Realm/Build Adventure Scene")]
        public static void BuildAdventure()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode before assembling assets.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory("Assets/Realm/Scenes");Directory.CreateDirectory("Assets/Realm/Art/Materials");Directory.CreateDirectory("Assets/Realm/Content");Directory.CreateDirectory("Assets/Realm/Art/Prefabs");
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene("Assets/OutdoorsScene.unity",OpenSceneMode.Single);
            var campaign=AssetDatabase.LoadAssetAtPath<RealmCampaign>("Assets/Realm/Content/FrontierCampaign.asset");
            if(campaign==null){campaign=ScriptableObject.CreateInstance<RealmCampaign>();campaign.quests=RealmCampaign.CreateDefaultQuests();AssetDatabase.CreateAsset(campaign,"Assets/Realm/Content/FrontierCampaign.asset");}
            var environment=new GameObject("The Frontier — original modular environment");
            RealmArt.ResetCache();
            foreach(string file in Directory.GetFiles("Assets/Realm/Art/Materials","*.mat"))
            {var saved=AssetDatabase.LoadAssetAtPath<Material>(file.Replace('\\','/'));if(saved!=null)RealmArt.RegisterMaterial(saved.name,saved);}
            // World kit: meshes and materials from ArtSource/World, placed as nested prefabs. Missing pieces fall back to primitives.
            RealmArt.KitProvider=name=>
            {
                var asset=LoadWorldKit(name);
                return asset!=null?(GameObject)PrefabUtility.InstantiatePrefab(asset):null;
            };
            RealmArt.MaterialProvider=set=>AssetDatabase.LoadAssetAtPath<Material>(RealmWorldImport.MaterialFolder+set+".mat");
            try { RealmArt.BuildEnvironment(environment.transform); }
            finally { RealmArt.KitProvider=null; RealmArt.MaterialProvider=null; }
            foreach(var fire in environment.GetComponentsInChildren<Light>())
                if(fire.name=="Hearth fire light"){fire.lightUnit=LightUnit.Lumen;fire.intensity=9000;fire.shadows=LightShadows.Soft;}
            foreach(var item in RealmArt.Materials)
            {
                string path="Assets/Realm/Art/Materials/"+item.Key+".mat";
                var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(existing==null)AssetDatabase.CreateAsset(item.Value,path);
            }
            PrefabUtility.SaveAsPrefabAsset(environment,"Assets/Realm/Art/Prefabs/FrontierEnvironment.prefab");
            var bootstrap=new GameObject("The Realm — game systems").AddComponent<RealmBootstrap>();
            bootstrap.Configure(campaign,AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional){light.transform.rotation=Quaternion.Euler(32,-35,0);light.color=new Color(1,.87f,.67f);light.lightUnit=LightUnit.Lux;light.intensity=65000;}
            var atmosphere=new GameObject("Realm atmosphere").AddComponent<Volume>();atmosphere.isGlobal=true;atmosphere.priority=10;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure=profile.Add<Exposure>();exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(12.2f);
            var fog=profile.Add<Fog>();fog.enabled.Override(true);fog.meanFreePath.Override(160);fog.baseHeight.Override(-1);fog.maximumHeight.Override(30);fog.albedo.Override(new Color(.53f,.62f,.58f));
            var bloom=profile.Add<Bloom>();bloom.intensity.Override(.2f);
            var tonemap=profile.Add<Tonemapping>();tonemap.mode.Override(TonemappingMode.ACES);
            string profilePath="Assets/Realm/Content/Atmosphere.asset";
            if(AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath)==null)AssetDatabase.CreateAsset(profile,profilePath);
            atmosphere.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            foreach(var component in profile.components) if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,atmosphere.sharedProfile);
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene("Assets/OutdoorsScene.unity",false)};
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("[Realm] Adventure scene assembled: "+ScenePath);
        }
        public static GameObject LoadWorldKit(string name)
        {
            foreach(string subfolder in new[]{"", "Pass4/Nature/", "Pass4/Ruins/", "Pass5/Vegetation/", "Pass5/Ground/"})
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(RealmWorldImport.Folder+subfolder+name+".fbx");
                if(asset!=null)return asset;
            }
            return null;
        }
        [MenuItem("The Realm/Build Windows Player")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},locationPathName="Builds/Windows/TheRealm.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            Debug.Log("[Realm] Windows build: "+report.summary.result+" / errors="+report.summary.totalErrors);
        }
    }
}
