using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheRealm.Editor
{
    // Hosts a COPY of the user's cleared world, then reopens a partial clear.
    public static class RealmEnemyRestartSmoke
    {
        static RealmSession session;
        static WorldState before;
        static string directory;
        static double started,phaseAt;
        static int phase;
        public static void Start()
        {
            if(!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            session=UnityEngine.Object.FindFirstObjectByType<RealmSession>();
            if(session==null || session.Active) throw new InvalidOperationException("Close the running session first.");
            string source=Path.Combine(Application.persistentDataPath,"TheRealm");
            directory="Temp/RealmRestartSmoke/"+Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(directory);
            foreach(string file in new[]{"world.json","world.journal"})
                if(File.Exists(Path.Combine(source,file))) File.Copy(Path.Combine(source,file),Path.Combine(directory,file));
            before=new RealmSaveStore(directory).Load();
            if(before.enemies.Count==0 || before.enemies.Any(e=>!e.defeated)) throw new InvalidOperationException("This diagnostic expects a fully cleared source world; source was not modified.");
            session.UseDevelopmentSaveDirectory(directory);session.Host();
            UnityEngine.Object.FindFirstObjectByType<RealmPresentation>().MenuOpen=true;
            started=phaseAt=EditorApplication.timeSinceStartup;phase=0;EditorApplication.update+=Tick;
        }
        static void Check(bool ok,string message) {if(!ok) throw new InvalidOperationException(message);}
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying) {EditorApplication.update-=Tick;return;}
                double now=EditorApplication.timeSinceStartup;
                if(now-started>20) throw new InvalidOperationException("Restart verification timed out.");
                if(phase==0 && now-phaseAt>.75 && session.Active)
                {
                    var world=session.World;
                    Check(world.enemies.Count==before.enemies.Count && world.enemies.All(e=>!e.defeated && e.health==e.maxHealth),"Full clear did not restore every enemy.");
                    Check(world.chapter==before.chapter && world.questIndex==before.questIndex && world.ending==before.ending,"Quest progress changed.");
                    foreach(var original in before.characters)
                    {
                        var c=world.characters.Single(c=>c.id==original.id);
                        Check(c.life==original.life && c.level==original.level && c.gold==original.gold && c.experience==original.experience && c.health==original.health,"Character progress changed.");
                    }
                    Check(UnityEngine.Object.FindObjectsByType<RealmEnemyActor>(FindObjectsSortMode.None).Length==before.enemies.Count,"Restored enemies missing skeleton visuals.");
                    world.enemies[0].health=17;world.enemies[1].health=0;world.enemies[1].defeated=true;
                    session.Persist();session.Leave();phase=1;phaseAt=now;
                }
                else if(phase==1 && now-phaseAt>1)
                {
                    Check(UnityEngine.Object.FindObjectsByType<RealmEnemyActor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0,"Old session actors were not cleaned up.");
                    session.Host();phase=2;phaseAt=now;
                }
                else if(phase==2 && now-phaseAt>.75 && session.Active)
                {
                    Check(session.World.enemies[0].health==17 && session.World.enemies[1].defeated,"Partial clear was reset.");
                    Check(UnityEngine.Object.FindObjectsByType<RealmEnemyActor>(FindObjectsSortMode.None).Length==before.enemies.Count-1,"Partial-clear visible skeleton count wrong.");
                    Directory.CreateDirectory("Logs/Realm");
                    File.WriteAllText("Logs/Realm/enemy-restart-smoke.txt","Passed=True; CopiedSave=True; Restored="+before.enemies.Count+"; CharacterAndQuestProgressPreserved=True; PartialClearPreserved=True; SessionActorCleanup=True; SkeletonVisuals=True");
                    Finish();
                }
            }
            catch(Exception e)
            {
                Directory.CreateDirectory("Logs/Realm");File.WriteAllText("Logs/Realm/enemy-restart-smoke.txt","FAILED: "+e);Finish();Debug.LogException(e);
            }
        }
        static void Finish() {EditorApplication.update-=Tick;if(session!=null && session.Active) session.Leave();}
    }
}
