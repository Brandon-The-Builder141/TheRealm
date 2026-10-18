using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TheRealm
{
    // Visual observer only. No animation event deals damage or changes replicated state.
    public sealed class RealmEnemyActor : MonoBehaviour
    {
        public enum Pose { Idle, Run, Windup, Attack, Hit, Death }
        public struct Observed
        {
            public double nextAttack;
            public float health;
            public bool defeated;
            public static Observed From(EnemyState e) => new Observed {nextAttack=e.nextAttack,health=e.health,defeated=e.defeated};
        }
        public static Pose Reaction(Observed before, Observed after)
        {
            if(after.defeated && !before.defeated) return Pose.Death;
            if(after.nextAttack>before.nextAttack+.01) return Pose.Attack;
            if(after.health<before.health-.01f) return Pose.Hit;
            return Pose.Idle;
        }
        public static int Variant(int region,bool boss) => boss?2:Mathf.Clamp(region,0,2)==0?0:region==1?1:3;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] players;
        AnimationClip[] clips;
        readonly float[] weights=new float[6];
        Observed last;
        bool primed, hiddenOnArrival;
        Vector3 lastPosition;
        Pose pose;
        float age, deathAge;
        static bool missingReported;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDiagnostics() => missingReported=false;
        public Pose CurrentPose => pose;
        public bool Visible => !hiddenOnArrival && (pose!=Pose.Death || deathAge<2.5f);

        public static RealmEnemyActor Spawn(Transform parent,EnemyState enemy)
        {
            var set=Resources.Load<RealmEnemyVisualSet>("Characters/Skeletons");
            if(set==null)
            {
                if(!missingReported) {Debug.LogError("Skeleton enemy art is missing. Run The Realm > Imported Assets > Configure Animated Skeletons.");missingReported=true;}
                return null;
            }
            var root=new GameObject(enemy.name); root.transform.SetParent(parent,false);
            var actor=root.AddComponent<RealmEnemyActor>();
            actor.Initialize(set,Variant(enemy.region,enemy.boss));
            return actor;
        }

        public void Initialize(RealmEnemyVisualSet set,int variant)
        {
            var model=Instantiate(set.models[variant],transform,false);
            model.name="Skeleton visual";
            model.transform.localScale=Vector3.one*.84f; // Medium rig: 2.14m posed height -> 1.80m.
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials=r.sharedMaterials;
                for(int i=0;i<materials.Length;i++) if(materials[i]!=null && materials[i].name=="skeleton") materials[i]=set.boneMaterial;
                r.sharedMaterials=materials;
            }
            var weapon=variant==0?null:variant==3?set.staff:set.blade;
            if(weapon!=null)
            {
                Transform socket=null;
                foreach(var t in model.GetComponentsInChildren<Transform>()) if(t.name=="handslot.r") {socket=t;break;}
                if(socket!=null)
                {
                    var held=Instantiate(weapon,socket,false);
                    foreach(var r in held.GetComponentsInChildren<Renderer>()) r.sharedMaterial=set.boneMaterial;
                }
            }
            var animator=model.GetComponent<Animator>();
            if(animator==null) animator=model.AddComponent<Animator>();
            animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            clips=new[]{set.idle,set.run,variant==3?set.staffAttack:set.attack,variant==3?set.staffAttack:set.attack,set.hit,set.death};
            graph=PlayableGraph.Create("Realm skeleton presentation"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer=AnimationMixerPlayable.Create(graph,6);
            players=new AnimationClipPlayable[6];
            for(int i=0;i<6;i++) {players[i]=AnimationClipPlayable.Create(graph,clips[i]);players[i].SetSpeed(0);graph.Connect(players[i],0,mixer,i);}
            AnimationPlayableOutput.Create(graph,"Skeleton",animator).SetSourcePlayable(mixer);
            weights[0]=1; mixer.SetInputWeight(0,1); graph.Play(); graph.Evaluate(0);
        }
        void OnEnable() {primed=false;age=deathAge=0;hiddenOnArrival=false;lastPosition=transform.position;}
        void OnDestroy() {if(graph.IsValid()) graph.Destroy();}

        public void Sync(EnemyState enemy,float dt,double now)
        {
            if(!graph.IsValid()) return;
            dt=Mathf.Max(0,dt);
            var observed=Observed.From(enemy);
            if(primed && last.defeated && !observed.defeated)
            {
                primed=false;hiddenOnArrival=false;age=deathAge=0;
            }
            if(!primed)
            {
                primed=true;last=observed;lastPosition=transform.position;
                hiddenOnArrival=enemy.defeated;pose=Pose.Idle;
            }
            var reaction=Reaction(last,observed); last=observed;
            bool moving=dt>0 && (transform.position-lastPosition).sqrMagnitude>dt*dt*.04f;
            lastPosition=transform.position;
            age+=dt;
            if(enemy.defeated)
            {
                if(pose!=Pose.Death) {pose=Pose.Death;age=deathAge=0;}
                deathAge+=dt;
            }
            else
            {
                Pose next=pose;
                if(enemy.telegraphUntil>now) next=Pose.Windup;
                else if(reaction==Pose.Attack || reaction==Pose.Hit) next=reaction;
                else if(pose==Pose.Windup || (pose!=Pose.Attack && pose!=Pose.Hit) || age>=clips[(int)pose].length*(pose==Pose.Attack?.58f:1))
                    next=moving?Pose.Run:Pose.Idle;
                if(next!=pose || reaction==next && reaction!=Pose.Idle) {pose=next;age=0;}
            }
            for(int i=0;i<6;i++)
            {
                weights[i]=Mathf.Lerp(weights[i],i==(int)pose?1:0,1-Mathf.Exp(-dt*25));
                mixer.SetInputWeight(i,weights[i]);
            }
            float time=age;
            if(pose==Pose.Idle || pose==Pose.Run) time=Mathf.Repeat(age,clips[(int)pose].length);
            else if(pose==Pose.Windup) time=clips[(int)pose].length*Mathf.Lerp(.08f,.42f,1-Mathf.Clamp01((float)(enemy.telegraphUntil-now)/(enemy.boss?1.1f:.8f)));
            else if(pose==Pose.Attack) time+=clips[(int)pose].length*.42f;
            players[(int)pose].SetTime(Mathf.Min(time,clips[(int)pose].length-.001f));
            graph.Evaluate(0);
        }
    }
}
