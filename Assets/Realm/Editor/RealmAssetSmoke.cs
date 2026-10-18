using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheRealm.Editor
{
    // Runs the real presentation in an isolated save; never loads or changes the player's campaign.
    public static class RealmAssetSmoke
    {
        static RealmSession session;
        static EnemyState target;
        static RealmEnemyActor actor;
        static double began,phaseAt;
        static int phase;
        static readonly HashSet<RealmEnemyActor.Pose> poses=new HashSet<RealmEnemyActor.Pose>();
        static readonly List<string> report=new List<string>();
        public static void Start()
        {
            if(!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            session=UnityEngine.Object.FindFirstObjectByType<RealmSession>();
            if(session==null || session.Active) throw new InvalidOperationException("Start from the game title screen; no live campaign may be open.");
            session.UseDevelopmentSaveDirectory("Temp/RealmAssetSmoke/"+Guid.NewGuid().ToString("N"));
            session.Host();phase=0;began=EditorApplication.timeSinceStartup;phaseAt=began;
            report.Clear();poses.Clear();EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying || session==null) {EditorApplication.update-=Tick;return;}
                double now=EditorApplication.timeSinceStartup;
                if(now-began>40) throw new InvalidOperationException("Runtime asset smoke timed out.");
                if(phase==0 && session.Active)
                {
                    session.Send(new PlayerCommand{kind="create",text="Art verification",primary=0,secondary=4});
                    session.Simulation.PopulateRegion(1);session.Simulation.PopulateRegion(2);
                    phase=1;phaseAt=now;
                }
                else if(phase==1 && now-phaseAt>1 && session.LocalCharacter!=null)
                {
                    var actors=UnityEngine.Object.FindObjectsByType<RealmEnemyActor>(FindObjectsSortMode.None);
                    var environment=GameObject.Find("The Frontier — original modular environment").transform;
                    var root=environment.Find("Imported world props — camps and graves");
                    if(root==null || root.childCount!=12 || actors.Length!=48) throw new InvalidOperationException("Missing live dressing or skeletons: sites="+(root==null?0:root.childCount)+" actors="+actors.Length);
                    report.Add("Playing=True; RealSession=True; IsolatedSave=True; Sites="+root.childCount+"; Skeletons="+actors.Length);
                    var views=new List<(string name,Vector3 position,Vector3 target)>();
                    foreach(Transform cluster in root)
                    {
                        report.Add(cluster.name+" at "+cluster.position+" active="+cluster.gameObject.activeInHierarchy);
                        views.Add((cluster.name,cluster.position+new Vector3(7,3,-9),cluster.position+Vector3.up));
                    }
                    foreach(var e in new[]{session.World.enemies.First(e=>e.region==0&&!e.boss),session.World.enemies.First(e=>e.region==1&&!e.boss),session.World.enemies.First(e=>e.boss),session.World.enemies.First(e=>e.region==2&&!e.boss)})
                        views.Add((e.name,e.position+new Vector3(3,2.2f,4.5f),e.position+Vector3.up*(e.boss?1.5f:.9f)));
                    Capture(views,"Logs/Realm/runtime-assets.png");
                    target=session.World.enemies.First(e=>!e.boss);
                    actor=actors.OrderBy(a=>Vector3.Distance(a.transform.position,target.position)).First();
                    session.LocalCharacter.health=100000;
                    session.LocalCharacter.position=target.position+Vector3.forward*6;
                    UnityEngine.Object.FindFirstObjectByType<RealmPresentation>().MenuOpen=true;
                    phase=2;phaseAt=now;
                }
                else if(phase==2)
                {
                    poses.Add(actor.CurrentPose);
                    if(now-phaseAt>6)
                    {
                        if(!poses.Contains(RealmEnemyActor.Pose.Run) || !poses.Contains(RealmEnemyActor.Pose.Windup) || !poses.Contains(RealmEnemyActor.Pose.Attack))
                            throw new InvalidOperationException("Missing live combat animation: "+string.Join(",",poses));
                        report.Add("Real combat poses="+string.Join(",",poses));
                        // Controlled replicated-state probes are confined to the isolated test world.
                        session.LocalCharacter.position=Vector3.zero;target.telegraphUntil=0;target.health-=1;
                        phase=3;phaseAt=now;
                    }
                }
                else if(phase==3)
                {
                    poses.Add(actor.CurrentPose);
                    if(now-phaseAt>.5)
                    {
                        if(!poses.Contains(RealmEnemyActor.Pose.Hit)) throw new InvalidOperationException("Hit reaction did not display.");
                        target.defeated=true;target.health=0;phase=4;phaseAt=now;
                    }
                }
                else if(phase==4 && now-phaseAt>.3)
                {
                    if(actor.CurrentPose!=RealmEnemyActor.Pose.Death || !actor.gameObject.activeInHierarchy) throw new InvalidOperationException("Death animation did not display.");
                    report.Add("Hit=True; Death=True");phase=5;phaseAt=now;
                }
                else if(phase==5 && now-phaseAt>3)
                {
                    if(actor.gameObject.activeInHierarchy) throw new InvalidOperationException("Defeated enemy not hidden after death.");
                    report.Add("DeathHidden=True; Passed=True");Finish();
                }
            }
            catch(Exception e) {report.Add("FAILED: "+e);Finish();Debug.LogException(e);}
        }
        static void Finish()
        {
            Directory.CreateDirectory("Logs/Realm");File.WriteAllLines("Logs/Realm/runtime-assets.txt",report);
            EditorApplication.update-=Tick;
            if(session!=null && session.Active) session.Leave();
        }
        static void Capture(List<(string name,Vector3 position,Vector3 target)> views,string path)
        {
            const int w=640,h=360;
            var go=new GameObject("__RuntimeArtCamera");
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=55;
            var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);
            var sheet=new Texture2D(w*4,h*4,TextureFormat.RGBA32,false);
            var tile=new Texture2D(w,h,TextureFormat.RGBA32,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;
                for(int i=0;i<views.Count;i++)
                {
                    camera.transform.position=views[i].position;camera.transform.LookAt(views[i].target);
                    for(int f=0;f<6;f++) RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
                    RenderTexture.active=rt;tile.ReadPixels(new Rect(0,0,w,h),0,0);tile.Apply();
                    sheet.SetPixels(i%4*w,(3-i/4)*h,w,h,tile.GetPixels());
                }
                sheet.Apply();Directory.CreateDirectory("Logs/Realm");File.WriteAllBytes(path,sheet.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;rt.Release();
                UnityEngine.Object.DestroyImmediate(tile);UnityEngine.Object.DestroyImmediate(sheet);
                UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
