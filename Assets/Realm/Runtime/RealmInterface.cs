using UnityEngine;

namespace TheRealm
{
    public sealed class RealmInterface : MonoBehaviour
    {
        RealmSession session;
        RealmPresentation presentation;
        GUIStyle title,heading,body,small,button,field;
        readonly Color ink=new Color(.035f,.055f,.055f,.96f), parchment=new Color(.90f,.87f,.77f), muted=new Color(.58f,.66f,.62f);
        string address="127.0.0.1",characterName="Wayfarer";
        int primary,secondary=4;
        bool selectingSecondary;
        Vector2 scroll;
        static readonly string[] Descriptions={"Hold the line.\nHeavy strikes · endurance","Find the opening.\nPrecision · evasion","Read the wild.\nRanged attacks · survival","Shape the unseen.\nElemental reach · area damage","Keep hope alive.\nRestoration · one fatal rescue","Make your own advantage.\nDevices · explosive force"};
        static string Ability(int discipline,int slot)=>RealmAbilities.Name(discipline,slot);
        public void Initialize(RealmSession s,RealmPresentation p) {session=s;presentation=p;}
        void Styles()
        {
            if(title!=null)return;
            var serif=Font.CreateDynamicFontFromOSFont("Georgia",48);
            var sans=Font.CreateDynamicFontFromOSFont("Segoe UI",18);
            title=new GUIStyle {font=serif,fontSize=58,normal={textColor=parchment}};
            heading=new GUIStyle {font=serif,fontSize=26,normal={textColor=parchment},wordWrap=true};
            body=new GUIStyle {font=sans,fontSize=18,normal={textColor=parchment},wordWrap=true};
            small=new GUIStyle(body){fontSize=14,normal={textColor=muted}};
            button=new GUIStyle(GUI.skin.button){font=sans,fontSize=17,alignment=TextAnchor.MiddleCenter,wordWrap=true,normal={textColor=parchment},padding=new RectOffset(12,12,8,8)};
            field=new GUIStyle(GUI.skin.textField){font=sans,fontSize=20,padding=new RectOffset(12,12,10,10)};
        }
        void OnGUI()
        {
            if(session==null)return;
            Styles(); float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1600*scale)*.5f,(Screen.height-900*scale)*.5f,0),Quaternion.identity,new Vector3(scale,scale,1));
            if(!session.Active) {MainMenu();return;}
            if(session.World==null) {Panel(new Rect(480,330,640,180));Text(520,380,560,80,"Entering the frontier…",heading);return;}
            var c=session.LocalCharacter;
            if(!string.IsNullOrEmpty(session.World.fatalError)) {Panel(new Rect(300,260,1000,340));Text(340,300,920,240,session.World.fatalError,heading);return;}
            if(c==null && !session.World.expeditionActive) {Creation();return;}
            Hud(c);
            if(session.World.dialogue!=null) Dialogue(c);
            else if(presentation.MenuOpen) Journal(c);
            else if(c==null) {Panel(new Rect(480,320,640,180));Text(520,350,560,45,"Your story has ended.",heading);Text(520,410,560,65,"Everything is lost. Watch your companions finish the expedition, then begin a new life.",body);}
        }
        void MainMenu()
        {
            Fill(new Rect(0,0,700,900),new Color(.02f,.045f,.043f,.91f));
            Text(74,85,550,25,"A WORLD THAT REMEMBERS",small);
            Text(70,140,600,85,"THE REALM",title);
            Line(74,248,480);
            Text(74,285,500,105,"Every choice leaves a mark.\nEvery life is your only one.",heading);
            Text(74,413,480,65,"A hardcore cooperative fantasy adventure\n1–4 players  /  persistent world  /  permanent death",body);
            if(Button(new Rect(74,530,475,55),"ENTER YOUR WORLD",true)) session.Host();
            address=GUI.TextField(new Rect(74,612,285,48),address,field);
            if(Button(new Rect(373,612,176,48),"JOIN FRIEND"))session.Join(address);
            Text(74,686,490,80,session.Status,small);
            Text(74,812,520,45,"Direct connection · UDP 7777\nFriends need the host online to enter this world.",small);
            Text(1170,820,360,35,"EMBERHEARTH  /  THE FRONTIER",small);
        }
        void Creation()
        {
            Fill(new Rect(0,0,1600,900),new Color(.025f,.044f,.045f,.95f));
            Text(64,36,750,45,"A new name. An unwritten fate.",heading);
            Text(64,89,1100,30,"Choose two disciplines. Your primary defines your approach; your secondary opens another path.",body);
            characterName=GUI.TextField(new Rect(64,142,400,48),characterName,24,field);
            if(Button(new Rect(490,142,250,48),"1  PRIMARY: "+(Discipline)primary,!selectingSecondary))selectingSecondary=false;
            if(Button(new Rect(758,142,280,48),"2  SECONDARY: "+(Discipline)secondary,selectingSecondary))selectingSecondary=true;
            for(int i=0;i<6;i++)
            {
                int col=i%3,row=i/3; Rect r=new Rect(64+col*327,222+row*226,305,205);
                Panel(r); Fill(new Rect(r.x,r.y,5,r.height),RealmArt.DisciplineColors[i]);
                Text(r.x+22,r.y+20,260,40,((Discipline)i).ToString(),heading);
                Text(r.x+22,r.y+75,255,75,Descriptions[i],body);
                if(Button(new Rect(r.x+20,r.y+153,265,35),i==primary?"PRIMARY":i==secondary?"SECONDARY":"CHOOSE",i==primary||i==secondary))
                {
                    if(selectingSecondary) {if(i!=primary)secondary=i;}
                    else if(i!=secondary) {primary=i;selectingSecondary=true;}
                }
            }
            Panel(new Rect(1080,142,456,590));
            Text(1110,170,390,35,"YOUR STARTING PATH",small);
            Text(1110,213,390,70,(Discipline)primary+" + "+(Discipline)secondary,heading);
            for(int i=0;i<4;i++)
            {
                var a=RealmAbilities.Get((Discipline)(i<2?primary:secondary),i%2);
                Text(1110,299+i*62,390,24,(i+1)+"  "+a.name+"   ·   "+Mathf.RoundToInt(a.energy)+" energy",body);
                Text(1110,323+i*62,385,36,a.note,small);
            }
            Text(1110,556,390,80,primary==4||secondary==4?"LAST LIGHT\nYou can rescue one ally at zero health within three seconds. Shared limit: once per expedition.":"No fatal rescue skill in this build. A Spirit companion can protect the party's one chance.",body);
            Text(1110,650,390,80,"Death erases this character and everything they own. Choose with intention.",small);
            if(Button(new Rect(1080,754,456,60),"BEGIN THIS LIFE",true))session.Send(new PlayerCommand {kind="create",text=characterName,primary=primary,secondary=secondary});
            if(Button(new Rect(64,754,270,45),"PRESET: OATHKEEPER")){primary=0;secondary=4;}
            if(Button(new Rect(349,754,270,45),"PRESET: SPELLSTALKER")){primary=1;secondary=3;}
            if(Button(new Rect(634,754,270,45),"PRESET: PATHMAKER")){primary=2;secondary=5;}
            if(Button(new Rect(64,824,200,35),"LEAVE WORLD"))session.Leave();
        }
        void Hud(CharacterState c)
        {
            var world=session.World;
            int row=0;
            foreach(var ally in world.characters)
            {
                if(ally.life==LifeState.Dead)continue;
                float y=30+row++*72;Panel(new Rect(30,y,270,62));
                Text(44,y+8,240,24,ally.name+"  ·  "+ally.level,body);
                Bar(new Rect(44,y+40,240,5),ally.health/ally.MaxHealth,new Color(.63f,.24f,.18f));
            }
            var q=session.Quests[Mathf.Min(world.questIndex,session.Quests.Length-1)];
            Panel(new Rect(1170,30,400,134));
            Text(1190,44,360,22,world.ending?"THE WORLD ENDURES":"CHAPTER "+(world.chapter+1)+"  /  "+new[]{"THE LISTENING WOOD","THE ASHEN MARCH","THE SUNLESS KINGDOM"}[world.chapter],small);
            Text(1190,77,355,55,world.ending?"A dawn you have earned":q.title,heading);
            if(c!=null&&!world.ending)Text(1190,136,350,25,Mathf.RoundToInt(Vector3.Distance(c.position,q.location))+" m  ·  Follow the gold beacon",small);
            Text(620,30,480,30,world.expeditionActive?"EXPEDITION "+world.expeditionNumber+"   ·   RESCUE "+(world.rescueUsed?"SPENT":"AVAILABLE"):"EMBERHEARTH   ·   SAFE HAVEN",small);
            if(c!=null)
            {
                if(c.life==LifeState.Intervention)
                {
                    Fill(new Rect(0,0,1600,900),new Color(.35f,0,0,.2f));Panel(new Rect(490,320,620,160));
                    Text(530,350,540,45,"AT DEATH'S EDGE",heading);
                    Text(530,415,540,40,"An ally must intervene: "+Mathf.Max(0,(float)(c.rescueDeadline-RealmSession.Now)).ToString("F1")+"s",body);
                }
                foreach(var ally in world.characters)
                    if(RealmRules.CanRescue(world,c,ally,RealmSession.Now)) {Panel(new Rect(510,580,580,80));Text(535,595,540,60,"R  ·  SAVE "+ally.name.ToUpperInvariant()+" NOW\nUses the expedition's only rescue",body);}
                Text(38,745,340,30,c.name+"  ·  LEVEL "+c.level,heading);
                Bar(new Rect(38,792,300,10),c.health/c.MaxHealth,new Color(.65f,.25f,.19f));
                Bar(new Rect(38,812,300,5),c.energy/100,new Color(.26f,.60f,.59f));
                Text(38,832,340,28,Mathf.CeilToInt(c.health)+" / "+Mathf.CeilToInt(c.MaxHealth)+" HP     "+c.gold+" gold",small);
                for(int i=0;i<4;i++)
                {
                    int d=i<2?(int)c.ActivePrimary:(int)c.ActiveSecondary;
                    var a=RealmAbilities.Get((Discipline)d,i%2);
                    bool ready=RealmSession.Now>=c.abilityReady && c.energy>=a.energy;
                    Panel(new Rect(438+i*153,790,143,70));
                    Text(451+i*153,798,125,20,(i+1)+"   "+((Discipline)d).ToString().ToUpperInvariant(),small);
                    Text(451+i*153,819,125,22,a.name,small);
                    Text(451+i*153,838,125,18,ready?Mathf.RoundToInt(a.energy)+" energy":"…",small);
                }
                Text(1090,792,470,55,"Q  Potion ×"+c.potions+"     G  Bomb ×"+c.bombs+"\nTAB  Character & journal     ESC  Menu",small);
                if(Vector3.Distance(c.position,q.location)<5&&!world.ending&&world.dialogue==null) {Panel(new Rect(565,667,470,50));Text(590,680,420,30,"E  ·  "+q.narrator,body);}
            }
            Text(430,742,730,38,world.lastRoll,small);
            if(world.lastDie>0) {Panel(new Rect(365,730,48,48));Text(374,738,45,35,world.lastDie.ToString(),heading);}
            Text(430,870,850,25,"WASD  Move    MOUSE  Look    LMB  Strike    RMB  Block    SPACE  Dodge    E  Speak",small);
            // Enemy tells are visible before damage resolves.
            foreach(var enemy in world.enemies)
            {
                if(enemy.defeated || c==null || Vector3.Distance(c.position,enemy.position)>22)continue;
                Vector3 p=presentation.ViewCamera.WorldToViewportPoint(enemy.position+Vector3.up*(enemy.boss?4:2.4f));
                if(p.z<0 || p.x<0 || p.x>1 || p.y<0 || p.y>1)continue;
                float x=p.x*1600,y=(1-p.y)*900;
                Bar(new Rect(x-45,y,90,4),enemy.health/enemy.maxHealth,new Color(.73f,.28f,.16f));
                if(enemy.telegraphUntil>RealmSession.Now)Text(x-65,y-32,160,28,"!  INCOMING",body);
            }
        }
        void Dialogue(CharacterState c)
        {
            var vote=session.World.dialogue;var q=session.Quests[Mathf.Min(session.World.questIndex,session.Quests.Length-1)];
            Panel(new Rect(290,215,1020,490));
            Text(325,240,930,30,q.narrator.ToUpperInvariant(),small);
            Text(325,286,930,125,q.prompt,heading);
            string speaker=vote.turn<vote.participants.Count?session.World.Find(vote.participants[vote.turn])?.name:"All checks complete";
            Text(325,428,930,40,"Speaking turn: "+speaker+"   ·   "+q.skill+" check / difficulty "+q.difficulty,body);
            if(c!=null && vote.turn<vote.participants.Count && vote.participants[vote.turn]==c.id)
                if(Button(new Rect(325,476,930,43),"ROLL YOUR CHECK",true))session.Send(new PlayerCommand {kind="check"});
            if(c!=null && vote.participants.Contains(c.id) && !vote.voters.Contains(c.id))
            {
                if(Button(new Rect(325,545,452,65),q.optionA))session.Send(new PlayerCommand {kind="vote",choice=0});
                if(Button(new Rect(795,545,460,65),q.optionB))session.Send(new PlayerCommand {kind="vote",choice=1});
            }
            Text(325,639,930,35,"Party vote  ·  "+vote.voters.Count+" / "+vote.participants.Count+"  ·  Speaker breaks ties  ·  "+Mathf.Max(0,(float)(vote.deadline-RealmSession.Now)).ToString("F0")+"s remaining",small);
        }
        void Journal(CharacterState c)
        {
            Panel(new Rect(280,115,1040,620));
            Text(315,145,920,50,"The story you carry",heading);
            Text(315,200,920,40,"Settlement trust: "+session.World.settlementTrust+"     Wilderness trust: "+session.World.wildernessTrust+"     Game Master: "+session.World.directorMood,small);
            scroll=GUI.BeginScrollView(new Rect(315,250,950,230),scroll,new Rect(0,0,900,session.World.history.Count*43+20));
            for(int i=0;i<session.World.history.Count;i++)Text(0,i*43,885,42,session.World.history[session.World.history.Count-1-i],small);
            GUI.EndScrollView();
            if(c!=null && RealmRules.InHaven(c.position))
            {
                if(Button(new Rect(315,504,295,45),"REST / RESUPPLY · 15 gold"))session.Send(new PlayerCommand {kind="rest"});
                if(Button(new Rect(630,504,295,45),"RETRAIN ALL · 25 gold"))session.Send(new PlayerCommand {kind="retrain"});
                Text(945,510,295,45,"Training: "+20*(c.upgrades+1)+" gold / rank",small);
                if(Button(new Rect(315,560,295,38),"POWER +3 damage  ["+c.offense+"]"))session.Send(new PlayerCommand {kind="upgrade",choice=0});
                if(Button(new Rect(630,560,295,38),"VIGOR +8 health  ["+c.defense+"]"))session.Send(new PlayerCommand {kind="upgrade",choice=1});
                if(Button(new Rect(945,560,295,38),"FOCUS +regen/checks  ["+c.focus+"]"))session.Send(new PlayerCommand {kind="upgrade",choice=2});
                for(int i=0;i<6;i++)
                {
                    if(!c.Has((Discipline)i)) {if(Button(new Rect(315+i*155,611,147,35),"Learn "+(Discipline)i))session.Send(new PlayerCommand {kind="learn",choice=i});}
                    else if(Button(new Rect(315+i*155,611,147,35),"Equip "+(Discipline)i)) session.Send(new PlayerCommand {kind="equip",choice=i,primary=0});
                }
                Text(315,650,930,20,"Learning: level "+(4+c.learned.Count*3)+" + 100 gold. Equip changes slots 1–2. Starting disciplines remain yours.",small);
            }
            else Text(315,520,930,55,"Return to Emberhearth to rest, resupply, or train. Opening this journal does not pause danger.",body);
            if(Button(new Rect(315,669,450,42),"RETURN TO THE ROAD",true))presentation.MenuOpen=false;
            if(Button(new Rect(785,669,450,42),"SAVE & LEAVE WORLD"))session.Leave();
        }
        void Text(float x,float y,float w,float h,string text,GUIStyle style)=>GUI.Label(new Rect(x,y,w,h),text,style);
        void Fill(Rect r,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Panel(Rect r){Fill(r,ink);Fill(new Rect(r.x,r.y,r.width,1),new Color(.56f,.49f,.31f,.65f));}
        void Line(float x,float y,float w)=>Fill(new Rect(x,y,w,1),RealmArt.Gold);
        void Bar(Rect r,float value,Color color){Fill(r,new Color(.13f,.14f,.13f));Fill(new Rect(r.x,r.y,r.width*Mathf.Clamp01(value),r.height),color);}
        bool Button(Rect r,string label,bool selected=false)
        {
            Color old=GUI.backgroundColor;GUI.backgroundColor=selected?new Color(.36f,.49f,.37f):new Color(.17f,.23f,.22f);
            bool result=GUI.Button(r,label,button);GUI.backgroundColor=old;return result;
        }
    }
}
