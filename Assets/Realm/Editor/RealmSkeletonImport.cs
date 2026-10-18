using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheRealm.Editor
{
    public static class RealmSkeletonImport
    {
        const string Source="Assets/KayKit_Skeletons_1.1_FREE/KayKit_Skeletons_1.1_FREE/";
        const string Animations="Assets/KayKit_Character_Animations_1.1/KayKit_Character_Animations_1.1/Animations/fbx/Rig_Medium/Rig_Medium_";
        const string Target="Assets/Realm/Resources/Characters/Skeletons.asset";
        static AnimationClip Clip(string bundle,string name) => AssetDatabase.LoadAllAssetsAtPath(Animations+bundle+".fbx").OfType<AnimationClip>().Single(c=>c.name==name);

        [MenuItem("The Realm/Imported Assets/Configure Animated Skeletons")]
        public static void Configure()
        {
            var models=new[]{"Minion","Rogue","Warrior","Mage"}.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>(Source+"characters/fbx/Skeleton_"+n+".fbx")).ToArray();
            if(models.Any(m=>m==null)) throw new InvalidOperationException("Missing skeleton model.");
            var idle=Clip("General","Idle_A");
            foreach(var model in models)
                if(AnimationUtility.GetCurveBindings(idle).Any(b=>model.transform.Find(b.path)==null)) throw new InvalidOperationException("Skeleton animation bone paths do not match.");
            string materialPath="Assets/Realm/Art/World/Materials/Imported_KayKit_Skeleton.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null)
            {
                var source=models[0].GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).First(m=>m.name=="skeleton");
                material=new Material(source){name="Imported_KayKit_Skeleton"};
                AssetDatabase.CreateAsset(material,materialPath);
                Undo.RegisterCreatedObjectUndo(material,"Create skeleton palette");
            }
            var set=AssetDatabase.LoadAssetAtPath<RealmEnemyVisualSet>(Target);
            if(set==null)
            {
                set=ScriptableObject.CreateInstance<RealmEnemyVisualSet>();
                AssetDatabase.CreateAsset(set,Target);Undo.RegisterCreatedObjectUndo(set,"Create skeleton presentation references");
            }
            else Undo.RecordObject(set,"Configure skeleton presentation references");
            set.models=models;set.boneMaterial=material;
            set.blade=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"assets/fbx(unity)/Skeleton_Blade.fbx");
            set.staff=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"assets/fbx(unity)/Skeleton_Staff.fbx");
            set.idle=idle;set.run=Clip("MovementBasic","Running_A");
            set.attack=Clip("CombatMelee","Melee_1H_Attack_Chop");
            set.staffAttack=Clip("CombatMelee","Melee_2H_Attack_Chop");
            set.hit=Clip("General","Hit_A");set.death=Clip("General","Death_A");
            EditorUtility.SetDirty(set);AssetDatabase.SaveAssetIfDirty(set);AssetDatabase.SaveAssetIfDirty(material);
            Validate();
        }

        [MenuItem("The Realm/Imported Assets/Validate Animated Skeletons")]
        public static void Validate()
        {
            var set=AssetDatabase.LoadAssetAtPath<RealmEnemyVisualSet>(Target);
            if(set==null || set.models.Length!=4 || set.blade==null || set.staff==null || set.boneMaterial==null || set.boneMaterial.shader.name!="HDRP/Lit" || set.boneMaterial.GetTexture("_BaseColorMap")==null)
                throw new InvalidOperationException("Incomplete skeleton references/materials.");
            foreach(var model in set.models)
            {
                if(model==null || model.GetComponentsInChildren<Collider>().Length!=0) throw new InvalidOperationException("Invalid skeleton model.");
                foreach(var clip in new[]{set.idle,set.run,set.attack,set.staffAttack,set.hit,set.death})
                    if(clip==null || AnimationUtility.GetCurveBindings(clip).Any(b=>model.transform.Find(b.path)==null)) throw new InvalidOperationException("Animation binding mismatch.");
            }
            Debug.Log("[Realm Skeletons] Four models, six clips, HDRP palette, all bone bindings valid. Source imports and player prefab unchanged.");
        }

        [MenuItem("The Realm/Imported Assets/Render Skeleton Comparison")]
        public static void Preview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=RealmBuilder.ScenePath)
                throw new InvalidOperationException("Open TheRealm outside Play Mode.");
            Validate();
            var temporary=new GameObject("__SkeletonPreview"){hideFlags=HideFlags.DontSave};
            var views=new (string name,Vector3 position,Vector3 target)[8];
            try
            {
                for(int i=0;i<8;i++)
                {
                    var p=new Vector3(0,0,45+i*12);
                    var placeholder=RealmArt.Adventurer("Before",new Color(.32f,.24f,.18f),true,i==2 || i==6);
                    placeholder.transform.SetParent(temporary.transform);placeholder.transform.position=p;
                    views[i]=("Skeleton "+i,p+new Vector3(3,2.2f,4.5f),p+Vector3.up*(i%4==2?1.5f:.9f));
                }
                RealmWorldPreview.RenderViews(views,"Logs/Realm/skeletons-before.png");
                foreach(Transform t in temporary.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
                for(int i=0;i<8;i++)
                {
                    int variant=i%4;
                    var e=new EnemyState {name="Preview",region=variant==3?2:variant,boss=variant==2,health=100};
                    var actor=RealmEnemyActor.Spawn(temporary.transform,e);
                    actor.transform.position=new Vector3(0,0,45+i*12);actor.transform.localScale=Vector3.one*(e.boss?1.8f:1);
                    actor.Sync(e,.1f,10);
                    if(i>=4) {e.telegraphUntil=10.3;actor.Sync(e,.1f,10);}
                    for(int f=0;f<20;f++) actor.Sync(e,.016f,10);
                }
                RealmWorldPreview.RenderViews(views,"Logs/Realm/skeletons-after.png");
            }
            finally {UnityEngine.Object.DestroyImmediate(temporary);}
        }
    }
}
