using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheRealm
{
    public sealed class RealmPresentation : MonoBehaviour
    {
        public RealmSession Session { get; private set; }
        public bool MenuOpen { get; set; }
        public Camera ViewCamera { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 22;
        readonly Dictionary<string,GameObject> actors=new Dictionary<string,GameObject>();
        readonly Dictionary<string,RealmCharacterActor> animated=new Dictionary<string,RealmCharacterActor>();
        readonly Dictionary<string,RealmEnemyActor> skeletons=new Dictionary<string,RealmEnemyActor>();
        InputActionAsset input;
        InputAction move,look,attack;
        float nextInput;
        GameObject marker;
        public void Initialize(RealmSession session,InputActionAsset actions)
        {
            Session=session; ViewCamera=Camera.main;
            if(ViewCamera==null) { var c=new GameObject("Realm Camera"); c.tag="MainCamera"; ViewCamera=c.AddComponent<Camera>(); c.AddComponent<AudioListener>(); }
            ViewCamera.nearClipPlane=.1f; ViewCamera.farClipPlane=500;
            if(actions!=null)
            {
                input=Instantiate(actions); move=input.FindAction("Player/Move"); look=input.FindAction("Player/Look"); attack=input.FindAction("Player/Attack"); input.Enable();
            }
            marker=RealmArt.Part(null,"Current objective",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.13f,2,.13f),RealmArt.Material("Objective gold",RealmArt.Gold,0,true));
        }
        void Update()
        {
            if(Session==null) return;
            var k=Keyboard.current;
            if(k!=null && k.escapeKey.wasPressedThisFrame) MenuOpen=!MenuOpen;
            var c=Session.LocalCharacter;
            bool control=Session.Active && c!=null && c.life==LifeState.Alive && Session.World.dialogue==null && !MenuOpen;
            Cursor.lockState=control?CursorLockMode.Locked:CursorLockMode.None; Cursor.visible=!control;
            if(!control)
            {
                if(Session.Active && Time.unscaledTime>nextInput) { nextInput=Time.unscaledTime+.05f; Session.Send(new PlayerCommand {kind="move",yaw=Yaw}); }
                return;
            }
            Vector2 delta=look?.ReadValue<Vector2>()??Vector2.zero;
            Yaw+=delta.x*.12f; Pitch=Mathf.Clamp(Pitch-delta.y*.1f,-10,65);
            Vector2 direction=move?.ReadValue<Vector2>()??Vector2.zero;
            Vector3 v=Quaternion.Euler(0,Yaw,0)*new Vector3(direction.x,0,direction.y);
            if(Time.unscaledTime>nextInput)
            {
                nextInput=Time.unscaledTime+.05f;
                Session.Send(new PlayerCommand {kind="move",x=v.x,z=v.z,yaw=Yaw,block=Mouse.current?.rightButton.isPressed??false});
            }
            if(attack!=null && attack.WasPressedThisFrame()) Action("attack");
            if(k==null) return;
            if(k.spaceKey.wasPressedThisFrame) Action("dodge");
            if(k.eKey.wasPressedThisFrame) Action("interact");
            if(k.digit1Key.wasPressedThisFrame) Action("ability",0);
            if(k.digit2Key.wasPressedThisFrame) Action("ability",1);
            if(k.digit3Key.wasPressedThisFrame) Action("ability",2);
            if(k.digit4Key.wasPressedThisFrame) Action("ability",3);
            if(k.qKey.wasPressedThisFrame) Action("potion");
            if(k.gKey.wasPressedThisFrame) Action("bomb");
            if(k.rKey.wasPressedThisFrame)
                foreach(var target in Session.World.characters) if(RealmRules.CanRescue(Session.World,c,target,RealmSession.Now)) { Session.Send(new PlayerCommand {kind="rescue",target=target.id}); break; }
            if(k.tabKey.wasPressedThisFrame) MenuOpen=true;
        }
        void Action(string kind,int choice=0) => Session.Send(new PlayerCommand {kind=kind,choice=choice});
        void LateUpdate()
        {
            if(Session==null) return;
            var world=Session.World;
            if(world==null)
            {
                foreach(var existing in actors.Values) if(existing!=null) Destroy(existing);
                actors.Clear();animated.Clear();skeletons.Clear();
                ViewCamera.transform.position=new Vector3(15,10,-9);
                ViewCamera.transform.LookAt(new Vector3(0,2,18)); marker.SetActive(false); return;
            }
            foreach(var c in world.characters)
            {
                var obj=CharacterActor(c);
                bool visible=c.life!=LifeState.Dead && (c.connected || c.disconnectedUntil>RealmSession.Now);
                obj.SetActive(visible);
                if(animated.TryGetValue(c.id,out var actor)) { Place(obj,c.position,c.yaw,false,false); if(visible) actor.Sync(c,Time.deltaTime); }
                else Place(obj,c.position,c.yaw,c.life==LifeState.Intervention);
            }
            foreach(var e in world.enemies)
            {
                if(!actors.ContainsKey(e.id))
                {
                    var skeleton=RealmEnemyActor.Spawn(transform,e);
                    if(skeleton!=null) {skeletons[e.id]=skeleton;actors[e.id]=skeleton.gameObject;skeleton.transform.SetPositionAndRotation(e.position,Quaternion.Euler(0,e.yaw,0));}
                }
                if(skeletons.TryGetValue(e.id,out var enemyActor))
                {
                    var visual=enemyActor.gameObject;
                    Place(visual,e.position,e.yaw,false,false);
                    visual.transform.localScale=Vector3.one*(e.boss?1.8f:1);
                    enemyActor.Sync(e,Time.deltaTime,RealmSession.Now);
                    visual.SetActive(enemyActor.Visible);
                    continue;
                }
                // Missing art is reported by Spawn; never substitute the retired block enemies.
            }
            var local=Session.LocalCharacter;
            Vector3 focus=local!=null?local.position:Vector3.zero;
            if(local==null) { var spectator=world.characters.Find(c=>c.life==LifeState.Alive && c.connected); if(spectator!=null) focus=spectator.position; }
            Vector3 pivot=focus+Vector3.up*1.6f;
            Vector3 offset=Quaternion.Euler(Pitch,Yaw,0)*new Vector3(.65f,0,-5.5f);
            if(Physics.SphereCast(pivot,.2f,offset.normalized,out var hit,offset.magnitude,1<<8)) offset=offset.normalized*Mathf.Max(.5f,hit.distance-.2f);
            ViewCamera.transform.position=Vector3.Lerp(ViewCamera.transform.position,pivot+offset,1-Mathf.Exp(-Time.deltaTime*12));
            ViewCamera.transform.LookAt(pivot+Quaternion.Euler(0,Yaw,0)*Vector3.forward*2);
            var q=Session.Quests[Mathf.Min(world.questIndex,Session.Quests.Length-1)];
            marker.SetActive(!world.ending); marker.transform.position=q.location+Vector3.up*(3+Mathf.Sin(Time.time)*.2f);
        }
        // Players use the animated Adventurer; the procedural figure remains as a fallback if the prefab is missing.
        GameObject CharacterActor(CharacterState c)
        {
            if(actors.TryGetValue(c.id,out var obj)) return obj;
            var actor=RealmCharacterActor.Spawn(transform,c.name,RealmArt.DisciplineColors[(int)c.primary]);
            if(actor==null) return Actor(c.id,c.name,RealmArt.DisciplineColors[(int)c.primary],false,false);
            actor.transform.SetPositionAndRotation(c.position,Quaternion.Euler(0,c.yaw,0));
            animated[c.id]=actor; actors[c.id]=actor.gameObject; return actor.gameObject;
        }
        GameObject Actor(string id,string name,Color color,bool enemy,bool boss)
        {
            if(actors.TryGetValue(id,out var obj)) return obj;
            obj=RealmArt.Adventurer(name,color,enemy,boss); obj.transform.SetParent(transform,false); actors[id]=obj; return obj;
        }
        void Place(GameObject obj,Vector3 position,float yaw,bool dying,bool gait=true)
        {
            bool moving=(obj.transform.position-position).sqrMagnitude>.01f;
            obj.transform.position=Vector3.Lerp(obj.transform.position,position,1-Mathf.Exp(-Time.deltaTime*18));
            obj.transform.rotation=Quaternion.Euler(dying?70:0,yaw,0);
            if(!gait) return;
            // Procedural gait for the placeholder figures that have no animation set yet.
            for(int i=0;i<obj.transform.childCount;i++)
            {
                var limb=obj.transform.GetChild(i);
                if(limb.name.EndsWith("Leg") || limb.name.EndsWith("Arm"))
                    limb.localRotation=Quaternion.Euler(moving?Mathf.Sin(Time.time*10)*(limb.name.StartsWith("Left")?25:-25):0,0,0);
            }
        }
        void OnDestroy()
        {
            if(input!=null) {input.Disable(); Destroy(input);}
            if(marker!=null) Destroy(marker);
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }
    }
}
