using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheRealm.Editor
{
    // Local development bridge. Only acts on an explicit, single-use command in the project's Temp folder.
    [InitializeOnLoad]
    public static class RealmAutomation
    {
        static double next;
        static RealmAutomation(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            next=EditorApplication.timeSinceStartup+1;
            string path="Temp/realm-command.json";
            if(!File.Exists(path))return;
            string json=File.ReadAllText(path);File.Delete(path);
            try
            {
                var command=JsonUtility.FromJson<Command>(json);
                switch(command.action)
                {
                    case "refresh": AssetDatabase.Refresh();break;
                    case "build-scene": RealmBuilder.BuildAdventure();break;
                    case "test": RealmValidation.RunTests();break;
                    case "play": EditorApplication.isPlaying=true;break;
                    case "stop": EditorApplication.isPlaying=false;break;
                    case "build-player": RealmBuilder.BuildWindows();break;
                    case "character-prefabs": RealmCharacterSetup.Build();break;
                    case "character-preview": RealmCharacterPreview.Render();break;
                    case "character-smoke": RealmCharacterSmoke.Start();break;
                    case "world-preview": RealmWorldPreview.Render();break;
                    case "world-materials": RealmWorldImport.EnsureMaterials();break;
                    case "capture":
                        var gameView=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));gameView.Focus();
                        Directory.CreateDirectory("Logs/Realm");ScreenCapture.CaptureScreenshot("Logs/Realm/"+command.value+".png");EditorApplication.QueuePlayerLoopUpdate();break;
                    case "host":
                        var host=UnityEngine.Object.FindFirstObjectByType<RealmSession>();host.UseDevelopmentSaveDirectory("Temp/RealmEditorSmoke");host.Host();break;
                    case "create": UnityEngine.Object.FindFirstObjectByType<RealmSession>().Send(new PlayerCommand {kind="create",text="Alden",primary=0,secondary=4});break;
                    case "send": UnityEngine.Object.FindFirstObjectByType<RealmSession>().Send(JsonUtility.FromJson<PlayerCommand>(command.value));break;
                    case "status":
                        var s=UnityEngine.Object.FindFirstObjectByType<RealmSession>();
                        Directory.CreateDirectory("Logs/Realm");File.WriteAllText("Logs/Realm/runtime-status.json",s!=null&&s.World!=null?JsonUtility.ToJson(s.World,true):"{\"status\":\"no world\"}");break;
                    case "inspect-art":
                        var scene=EditorSceneManager.GetActiveScene();
                        var session=UnityEngine.Object.FindFirstObjectByType<RealmSession>();
                        var lines=new System.Collections.Generic.List<string>{"Project="+Application.dataPath,"Scene="+scene.path,"Dirty="+scene.isDirty,"Playing="+EditorApplication.isPlaying,"Compiling="+EditorApplication.isCompiling,"SkeletonResource="+(Resources.Load<RealmEnemyVisualSet>("Characters/Skeletons")!=null),"Session="+(session!=null?session.Status:"none"),"Enemies="+(session?.World?.enemies.Count??0),"SkeletonActors="+UnityEngine.Object.FindObjectsByType<RealmEnemyActor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length};
                        foreach(var root in scene.GetRootGameObjects())
                            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                                if(t.name=="Imported world props — camps and graves" || t.name==RealmWorldPolish.RootName) lines.Add(t.name+" children="+t.childCount+" active="+t.gameObject.activeInHierarchy);
                        Directory.CreateDirectory("Logs/Realm");File.WriteAllLines("Logs/Realm/art-status.txt",lines);break;
                    case "imported-art":
                        RealmSkeletonImport.Configure();RealmImportedWorldProps.Redistribute();RealmWorldPass5.Validate();RealmWorldPass4.Validate();break;
                    case "asset-smoke": RealmAssetSmoke.Start();break;
                    case "enemy-restart-smoke": RealmEnemyRestartSmoke.Start();break;
                    case "save-verified-art":
                        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path!=RealmBuilder.ScenePath) throw new InvalidOperationException("Open TheRealm outside Play Mode.");
                        RealmSkeletonImport.Validate();RealmImportedWorldProps.Validate();RealmWorldPass5.Validate();RealmWorldPass4.Validate();
                        if(!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene())) throw new IOException("Scene save failed.");break;
                    default: throw new InvalidOperationException("Unknown Realm development command.");
                }
                File.WriteAllText("Temp/realm-command-result.txt",command.action+" dispatched at "+DateTime.UtcNow.ToString("O"));
            }
            catch(Exception e){File.WriteAllText("Temp/realm-command-result.txt",e.ToString());Debug.LogException(e);}
        }
        [Serializable] sealed class Command {public string action,value;}
    }
}
