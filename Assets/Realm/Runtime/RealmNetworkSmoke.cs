using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TheRealm
{
    // Opt-in development-player test. Never enabled in an ordinary game launch.
    public sealed class RealmNetworkSmoke : MonoBehaviour
    {
        RealmSession session;
        string role,output;
        public static void Attach(GameObject root,RealmSession session)
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++) if(args[i]=="--realm-smoke")
            {
                var smoke=root.AddComponent<RealmNetworkSmoke>();smoke.session=session;smoke.role=args[i+1];
                for(int j=0;j<args.Length-1;j++)if(args[j]=="--realm-report")smoke.output=args[j+1];
                smoke.StartCoroutine(smoke.Run());return;
            }
        }
        IEnumerator Run()
        {
            if(string.IsNullOrEmpty(output))yield break;
            yield return null;
            if(role=="host")session.Host();else session.Join("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+60;
            while(!session.Active && Time.realtimeSinceStartup<deadline)yield return null;
            if(!session.Active){Finish(false,"connection failed: "+session.Status);yield break;}
            session.Send(new PlayerCommand {kind="create",text=role=="host"?"SmokeHost":session.Owner,primary=4,secondary=0});
            while((session.World==null || session.World.characters.Count<4) && Time.realtimeSinceStartup<deadline)yield return null;
            if(session.World==null || session.World.characters.Count<4){Finish(false,"four characters did not replicate");yield break;}
            yield return new WaitForSecondsRealtime(2);
            if(role=="host")
            {
                foreach(var e in session.World.enemies)e.defeated=true;
                foreach(var c in session.World.characters)c.position=new Vector3(session.World.characters.IndexOf(c)*2,0,50);
                session.World.expeditionActive=true;session.World.expeditionNumber++;session.World.rescueUsed=false;
                var victim=session.World.characters[1];
                session.Simulation.Damage(victim,9999,RealmSession.Now);
                session.Send(new PlayerCommand {kind="rescue",target=victim.id});
                if(!session.World.rescueUsed || victim.life!=LifeState.Alive){Finish(false,"qualified rescue failed");yield break;}
                session.Simulation.Damage(session.World.characters[2],9999,RealmSession.Now);
                session.Persist();
            }
            yield return new WaitForSecondsRealtime(8);
            bool valid=session.World.rescueUsed && session.World.characters.Count==4 && session.World.characters[2].life==LifeState.Dead && session.World.characters[1].life==LifeState.Alive;
            if(role=="host")yield return new WaitForSecondsRealtime(4);
            Finish(valid,"four-peer state, shared rescue, permanent death replication");
        }
        void Finish(bool passed,string detail)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output,JsonUtility.ToJson(new Report {passed=passed,role=role,detail=detail,characters=session.World?.characters.Count??0},true));
            Debug.Log("[Realm Smoke] "+passed+" "+detail);session.Leave();Application.Quit(passed?0:1);
        }
        [Serializable] sealed class Report {public bool passed;public string role,detail;public int characters;}
    }
}
