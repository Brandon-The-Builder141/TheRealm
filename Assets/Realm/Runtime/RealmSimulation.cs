using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheRealm
{
    public sealed class RealmSimulation
    {
        public WorldState World { get; }
        public QuestDefinition[] Quests { get; }
        public Action Changed;
        public Func<Vector3, Vector3, Vector3> ResolveMovement;
        readonly System.Random random;
        readonly Dictionary<string, PlayerCommand> movement = new Dictionary<string, PlayerCommand>();
        public RealmSimulation(WorldState world, QuestDefinition[] quests)
        {
            World = world; Quests = quests;
            random = new System.Random(world.seed ^ world.revision);
            foreach (var c in world.characters) { c.connected = false; c.blocking = false; }
            world.dialogue = null;
        }
        public QuestDefinition CurrentQuest => Quests[Mathf.Clamp(World.questIndex, 0, Quests.Length - 1)];
        // Called only when opening a hosted world. Partial clears survive reconnects;
        // a completely cleared roster returns without rewinding quests or characters.
        public bool RestoreClearedEnemies(double now)
        {
            if(World.enemies.Count==0 || World.enemies.Exists(e=>!e.defeated)) return false;
            foreach(var enemy in World.enemies)
            {
                enemy.defeated=false;enemy.health=enemy.maxHealth;
                enemy.position=enemy.home;enemy.yaw=0;
                enemy.telegraphUntil=0;enemy.vulnerableUntil=0;enemy.nextAttack=now+4;
            }
            Note("The cleared frontier stirs again. Your deeds and character progress endure.");
            return true;
        }
        public void Note(string text)
        {
            World.history.Add(text);
            if (World.history.Count > 45) World.history.RemoveAt(0);
        }
        void Commit() => Changed?.Invoke();
        public CharacterState Connect(string owner, double now)
        {
            var c = World.characters.FindLast(p => p.owner == owner && p.life != LifeState.Dead);
            if (c != null) { c.connected = true; c.disconnectedUntil = 0; }
            Commit(); return c;
        }
        public void Disconnect(string owner, double now)
        {
            var c = World.characters.FindLast(p => p.owner == owner && p.life != LifeState.Dead);
            if (c == null) return;
            c.connected = false; c.blocking = false; c.disconnectedUntil = now + 30;
            movement.Remove(c.id); Commit();
        }
        public void Command(string owner, PlayerCommand command, double now)
        {
            if (!string.IsNullOrEmpty(World.fatalError) || command == null) return;
            var c = World.characters.FindLast(p => p.owner == owner && p.life != LifeState.Dead);
            if (command.kind == "create")
            {
                if (c != null || World.expeditionActive || command.primary < 0 || command.primary > 5 || command.secondary < 0 || command.secondary > 5 || command.primary == command.secondary) return;
                string name = (command.text ?? "Wayfarer").Trim();
                if (name.Length < 1 || name.Length > 24) return;
                c = new CharacterState { id = Guid.NewGuid().ToString("N"), owner = owner, name = name, primary = (Discipline)command.primary, secondary = (Discipline)command.secondary, connected = true };
                c.health = c.MaxHealth; World.characters.Add(c); Note(name + " arrives at Emberhearth."); Commit(); return;
            }
            if (c == null || !c.connected || c.life != LifeState.Alive) return;
            if (command.kind == "move")
            {
                if (RealmRules.Finite(command.x) && RealmRules.Finite(command.z) && RealmRules.Finite(command.yaw)) movement[c.id] = command;
                return;
            }
            if (command.kind == "rescue")
            {
                var target = World.Find(command.target);
                if (target != null && RealmRules.Rescue(World, c, target, now)) { Note(c.name + " pulls " + target.name + " back from the edge. The expedition's rescue is spent."); Commit(); }
                return;
            }
            if (World.dialogue != null)
            {
                if (command.kind == "check") Check(c);
                if (command.kind == "vote") Vote(c, command.choice);
                return;
            }
            if (command.kind == "attack" && now >= c.attackReady)
            {
                c.attackReady = now + .65;
                Strike(c, 3.5f, .25f, 20 + c.offense * 3 + 5 * Mathf.Log(c.level + 1, 2), false, false, 0, now);
            }
            else if (command.kind == "dodge" && c.energy >= 20 && now >= c.dodgeUntil + .6)
            { c.energy -= 20; c.dodgeUntil = now + .45; }
            else if (command.kind == "ability")
            {
                if (command.choice < 0 || command.choice > 3) return;
                Discipline d = command.choice < 2 ? c.ActivePrimary : c.ActiveSecondary;
                var ability = RealmAbilities.Get(d, command.choice % 2);
                if (now < c.abilityReady || c.energy < ability.energy) return;
                c.energy -= ability.energy; c.abilityReady = now + ability.cooldown;
                Cast(c, ability, now);
            }
            else if (command.kind == "potion" && c.potions > 0 && c.health < c.MaxHealth)
            { c.potions--; c.health = Mathf.Min(c.MaxHealth, c.health + 55); Commit(); }
            else if (command.kind == "bomb" && c.bombs > 0 && now >= c.attackReady)
            { c.bombs--; c.attackReady = now + 1; Strike(c, 14, .1f, 50, true, false, 0, now); Commit(); }
            else if (command.kind == "interact") Interact(c, now);
            else if (RealmRules.InHaven(c.position))
            {
                if (command.kind == "rest")
                {
                    c.health = c.MaxHealth; c.energy = 100;
                    if (c.gold >= 15) { c.gold -= 15; c.potions = 2; c.bombs = 2; }
                    Note(c.name + " rests by the hearth."); Commit();
                }
                if (command.kind == "upgrade" && command.choice >= 0 && command.choice < 3 && c.upgrades < c.level && c.gold >= 20 * (c.upgrades + 1))
                { c.gold -= 20 * (c.upgrades + 1); c.upgrades++; if(command.choice==0)c.offense++;else if(command.choice==1)c.defense++;else c.focus++; Commit(); }
                if (command.kind == "retrain" && c.gold >= 25 && c.upgrades > 0)
                { c.gold -= 25; c.upgrades=c.offense=c.defense=c.focus=0; c.health=Mathf.Min(c.health,c.MaxHealth); Note("Upgrades released. You may invest again at the trainer."); Commit(); }
                if (command.kind == "learn" && command.choice >= 0 && command.choice < 6 && c.level >= 4 + c.learned.Count * 3 && c.gold >= 100 && !c.Has((Discipline)command.choice))
                { c.gold -= 100; c.learned.Add((Discipline)command.choice); Note(c.name + " learns " + (Discipline)command.choice + "."); Commit(); }
                if (command.kind == "equip" && command.choice >= 0 && command.choice < 6 && c.Has((Discipline)command.choice))
                {
                    if(command.primary==0 && (Discipline)command.choice!=c.ActiveSecondary)c.activePrimary=command.choice;
                    else if(command.primary==1 && (Discipline)command.choice!=c.ActivePrimary)c.activeSecondary=command.choice;
                    Commit();
                }
            }
        }
        public void Cast(CharacterState c, AbilityDefinition ability, double now)
        {
            switch (ability.shape)
            {
                case AbilityShape.SelfRestore:
                    c.health = Mathf.Min(c.MaxHealth, c.health + ability.amount);
                    c.energy = Mathf.Min(100, c.energy + 15);
                    Note(c.name + " catches a second wind.");
                    break;
                case AbilityShape.Evade:
                    c.dodgeUntil = now + ability.amount;
                    break;
                case AbilityShape.AllyRestore:
                    foreach (var ally in World.characters)
                        if (ally.life == LifeState.Alive && Vector3.Distance(c.position, ally.position) <= ability.range)
                            ally.health = Mathf.Min(ally.MaxHealth, ally.health + ability.amount);
                    Note(c.name + " invokes a restoring light. It cannot reverse death.");
                    break;
                default:
                    float damage = ability.damage + c.offense * 3 + 4 * Mathf.Log(c.level + 1, 2);
                    Strike(c, ability.range, ability.minDot, damage, ability.multi, ability.backstab, ability.vulnerability, now);
                    if (ability.amount > 0) c.health = Mathf.Min(c.MaxHealth, c.health + ability.amount);
                    break;
            }
            Commit();
        }
        void Strike(CharacterState c, float range, float minDot, float damage, bool area, bool backstab, float vulnerability, double now)
        {
            Vector3 forward = Quaternion.Euler(0, c.yaw, 0) * Vector3.forward;
            bool hit = false;
            foreach (var enemy in World.enemies)
            {
                Vector3 delta = enemy.position - c.position;
                if (enemy.defeated || delta.magnitude > range || Vector3.Dot(forward, delta.normalized) < minDot) continue;
                int roll = random.Next(1, 21);
                float impact = damage * (roll == 20 ? 1.7f : .85f + roll * .015f);
                // Striking a target that is facing away from you is the payoff for flanking.
                if (backstab && Vector3.Dot(Quaternion.Euler(0, enemy.yaw, 0) * Vector3.forward, forward) > .35f) impact *= 1.9f;
                if (enemy.vulnerableUntil > now) impact *= RealmAbilities.VulnerableMultiplier;
                if (vulnerability > 0) enemy.vulnerableUntil = now + vulnerability;
                enemy.health -= impact; hit = true;
                World.lastRoll = c.name + " → " + enemy.name + "  •  impact " + Mathf.CeilToInt(impact) + (roll == 20 ? "  CRITICAL" : "");
                World.lastDie = roll;
                if (enemy.health <= 0)
                {
                    enemy.defeated = true;
                    foreach (var ally in World.characters) if (ally.connected && Vector3.Distance(ally.position, enemy.position) < 35) RealmRules.Award(ally, enemy.boss ? 140 : 35, enemy.boss ? 80 : 12);
                    Note(enemy.name + " falls."); World.tension = Mathf.Max(0, World.tension - 5);
                }
                if (!area) break;
            }
            if (hit) Commit();
        }
        public void Damage(CharacterState c, float amount, double now)
        {
            if (c.life != LifeState.Alive || now < c.dodgeUntil) return;
            if (c.blocking && c.energy >= 10) { amount *= .3f; c.energy -= 10; }
            c.health = Mathf.Max(0, c.health - amount);
            World.tension = Mathf.Min(100, World.tension + amount * .1f);
            if (c.health <= 0)
            {
                bool possible = !World.rescueUsed && World.characters.Exists(a => a != c && a.connected && a.life == LifeState.Alive && a.Has(Discipline.Spirit) && Vector3.Distance(a.position, c.position) <= 12);
                if (possible) { c.life = LifeState.Intervention; c.rescueDeadline = now + 3; Note(c.name + " is at death's edge. An ally must intervene NOW."); }
                else { RealmRules.FinalizeDeath(World, c); Note(c.name + " has died. Everything is lost."); }
                Commit();
            }
        }
        public void Tick(float dt, double now)
        {
            if (!string.IsNullOrEmpty(World.fatalError)) return;
            foreach (var c in World.characters)
            {
                if (c.life == LifeState.Intervention && now >= c.rescueDeadline) { RealmRules.FinalizeDeath(World, c); Note(c.name + " has died. Everything is lost."); Commit(); }
            }
            if (World.dialogue != null)
            {
                if (now >= World.dialogue.deadline) ResolveVote();
                return;
            }
            foreach (var c in World.characters)
            {
                if (c.life != LifeState.Alive || !c.connected) continue;
                c.energy = Mathf.Min(100, c.energy + dt * (12+c.focus));
                if (!movement.TryGetValue(c.id, out var input)) continue;
                c.yaw = input.yaw; c.blocking = input.block;
                Vector3 velocity = Vector3.ClampMagnitude(new Vector3(input.x, 0, input.z), 1) * (now < c.dodgeUntil ? 13 : c.blocking ? 2.5f : 6);
                Vector3 next = c.position + velocity * dt;
                next.x = Mathf.Clamp(next.x, -28, 28);
                next.z = Mathf.Clamp(next.z, -13, 145 + World.chapter * 149);
                c.position = ResolveMovement != null ? ResolveMovement(c.position, next) : next;
                if (!World.expeditionActive && !RealmRules.InHaven(c.position)) StartExpedition(now);
            }
            TickEnemies(dt, now);
            if (World.expeditionActive)
            {
                bool alive = World.characters.Exists(c => c.life != LifeState.Dead);
                bool allHome = World.characters.TrueForAll(c => c.life == LifeState.Dead || RealmRules.InHaven(c.position));
                if (!alive || allHome) { World.expeditionActive = false; Note(alive ? "The expedition ends at the hearth." : "The expedition is lost. The world endures."); Commit(); }
                else if (now >= World.nextDirectorEvent) Director(now);
            }
        }
        void StartExpedition(double now)
        {
            World.expeditionActive = true; World.rescueUsed = false; World.expeditionNumber++;
            World.nextDirectorEvent = now + 75; Note("Expedition " + World.expeditionNumber + " begins. One rescue. No second lives."); Commit();
        }
        void TickEnemies(float dt, double now)
        {
            foreach (var e in World.enemies)
            {
                if (e.defeated) continue;
                CharacterState target = null; float nearest = 18;
                foreach (var c in World.characters)
                {
                    if (c.life != LifeState.Alive || RealmRules.InHaven(c.position) || (!c.connected && now > c.disconnectedUntil)) continue;
                    float distance = Vector3.Distance(c.position, e.position);
                    if (distance < nearest) { nearest = distance; target = c; }
                }
                if (target == null) { e.position = Vector3.MoveTowards(e.position, e.home, dt * 2); e.telegraphUntil = 0; continue; }
                Vector3 delta = target.position - e.position;
                e.yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                if (e.telegraphUntil > 0)
                {
                    if (now >= e.telegraphUntil)
                    {
                        if (nearest < (e.boss ? 4.5f : 3)) Damage(target, e.damage, now);
                        e.telegraphUntil = 0; e.nextAttack = now + (e.boss ? 1.8 : 1.5);
                    }
                }
                else if (nearest > 2.3f) e.position = Vector3.MoveTowards(e.position, target.position, dt * (e.boss ? 2.4f : 3.3f));
                else if (now >= e.nextAttack) e.telegraphUntil = now + (e.boss ? 1.1 : .8);
            }
        }
        public void PopulateRegion(int region)
        {
            int party = Mathf.Max(1, World.characters.FindAll(c => c.life == LifeState.Alive).Count);
            for (int i = 0; i < 16; i++)
            {
                string id = "r" + region + "-" + i;
                if (World.enemies.Exists(e => e.id == id)) continue;
                bool boss = i == 15;
                Vector3 p = new Vector3(boss ? 0 : (i % 2 == 0 ? -1 : 1) * (7 + i % 4 * 3), 0, boss ? 119 + region * 149 : 37 + region * 149 + i * 5);
                float hp = (boss ? 430 : 65) * (1 + (party - 1) * .55f) * (1 + region * .4f);
                World.enemies.Add(new EnemyState { id=id,name=boss ? new[] {"Rootbound Warden","Ash Marshal","The Sleeper"}[region] : new[] {"Thornbound","Ashen sentinel","Hollow keeper"}[region],position=p,home=p,region=region,health=hp,maxHealth=hp,damage=(boss ? 32 : 15) + region * 5,boss=boss });
            }
        }
        void Interact(CharacterState c, double now)
        {
            if (World.ending) return;
            var q = CurrentQuest;
            if (Vector3.Distance(c.position, q.location) > 5) return;
            if ((q.id=="warden" || q.id=="marshal" || q.id=="sleeper") && World.enemies.Exists(e=>e.boss&&e.region==q.region&&!e.defeated))
            {Note("The guardian must fall before you can decide the seal's fate.");return;}
            if (World.enemies.Exists(e => !e.defeated && Vector3.Distance(e.position, c.position) < 13)) { Note("Deal with the nearby danger before speaking."); return; }
            var vote = new VoteState { questId=q.id,speaker=c.id,deadline=now + 90 };
            foreach (var p in World.characters) if (p.connected && p.life == LifeState.Alive && Vector3.Distance(p.position, c.position) < 25) vote.participants.Add(p.id);
            if (!vote.participants.Contains(c.id)) vote.participants.Insert(0,c.id);
            World.dialogue = vote; Note(q.narrator + ": " + q.prompt);
        }
        void Check(CharacterState c)
        {
            var vote = World.dialogue;
            if (vote.turn >= vote.participants.Count || vote.participants[vote.turn] != c.id || vote.checkedPlayers.Contains(c.id)) return;
            var q = CurrentQuest; int die = random.Next(1,21); int modifier = (c.Has(q.skill) ? 4 : 0) + c.focus/2 + Mathf.FloorToInt(Mathf.Log(c.level + 1,2));
            bool success = die + modifier >= q.difficulty;
            vote.checkedPlayers.Add(c.id); vote.turn++;
            if (success) vote.successes++;
            World.lastDie = die; World.lastRoll = c.name + " • " + q.skill + " • " + die + " + " + modifier + " vs " + q.difficulty + " • " + (success ? "SUCCESS" : "FAILURE");
            Note(World.lastRoll); Commit();
        }
        void Vote(CharacterState c,int choice)
        {
            var vote = World.dialogue;
            if (choice < 0 || choice > 1 || !vote.participants.Contains(c.id) || vote.voters.Contains(c.id)) return;
            vote.voters.Add(c.id); vote.choices.Add(choice);
            if (vote.voters.Count == vote.participants.Count) ResolveVote();
        }
        void ResolveVote()
        {
            var vote = World.dialogue; if (vote == null) return;
            int a = vote.choices.FindAll(x => x == 0).Count, b = vote.choices.Count - a;
            int chosen = a > b ? 0 : 1;
            if (a == b) { int speaker = vote.voters.IndexOf(vote.speaker); chosen = speaker >= 0 ? vote.choices[speaker] : 0; }
            var q = CurrentQuest;
            Note(q.title + " — " + (chosen == 0 ? q.optionA : q.optionB));
            Note(vote.successes > 0 ? q.success : q.failure);
            World.completed.Add(q.id + ":" + chosen);
            int direction = chosen == 0 ? 1 : -1;
            World.settlementTrust += q.settlementShift * direction;
            World.wildernessTrust += q.wildernessShift * direction;
            foreach (var c in World.characters) if (vote.participants.Contains(c.id)) RealmRules.Award(c, 100 + vote.successes * 25, 40 + vote.successes * 10);
            World.dialogue = null; World.questIndex++;
            if (World.questIndex >= Quests.Length) { World.ending = true; Note(Ending()); }
            else { World.chapter = Mathf.Min(2, World.questIndex / 6); PopulateRegion(World.chapter); }
            Commit();
        }
        public string Ending()
        {
            int settlement = World.settlementTrust, wilderness = World.wildernessTrust;
            if (settlement >= 0 && wilderness >= 0) return "A refuge rises from the ruins and the grove keeps its voice. The names of the fallen endure in both.";
            if (settlement > wilderness) return "A guarded frontier survives. Its walls hold, and beyond them the wild remembers every bargain you struck.";
            if (wilderness > settlement) return "The realm reclaims the road. What is left of Emberhearth lives at the forest's sufferance, and it does not forget.";
            return "Neither hearth nor grove will speak your names kindly. The frontier endures without you.";
        }
        void Director(double now)
        {
            World.nextDirectorEvent = now + random.Next(70,111);
            var party = World.characters.FindAll(c => c.life == LifeState.Alive && c.connected && !RealmRules.InHaven(c.position));
            if (party.Count == 0) return;
            float condition = 0; foreach (var p in party) condition += p.health / p.MaxHealth;
            condition /= party.Count;
            if (condition < .4f || World.tension > 65)
            {
                World.directorMood = "Merciful";
                foreach (var p in party) p.energy = 100;
                foreach (var e in World.enemies) if (!e.defeated) e.nextAttack = Math.Max(e.nextAttack, now + 4);
                Note("A pale wind stills the hunters for a moment. Your strength returns. Use this opening.");
                World.tension = Mathf.Max(0,World.tension - 20);
            }
            else if (condition > .75f && World.enemies.FindAll(e => !e.defeated).Count < 35)
            {
                World.directorMood = "Ruthless";
                var p = party[random.Next(party.Count)];
                Vector3 position = p.position + new Vector3(15,0,12); position.x = Mathf.Clamp(position.x,-26,26);
                World.enemies.Add(new EnemyState {id=Guid.NewGuid().ToString("N"),name="Oath hunter",position=position,home=position,health=90,maxHealth=90,damage=19,region=World.chapter,nextAttack=now+8});
                Note("A horn sounds beyond the trail. An oath hunter approaches. You have time to withdraw.");
            }
            else
            {
                World.directorMood = "Watchful";
                Note(World.settlementTrust > 0 ? "A villager's trail sign points toward shelter. Your kindness has not been forgotten." : "Old tracks cross your path. Someone is watching the road behind you.");
            }
            Debug.Log("[Realm Director] " + World.directorMood + " health=" + condition.ToString("F2") + " tension=" + World.tension);
            Commit();
        }
    }
}
