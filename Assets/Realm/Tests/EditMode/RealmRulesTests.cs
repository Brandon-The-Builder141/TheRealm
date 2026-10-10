using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TheRealm.Tests
{
    public sealed class RealmRulesTests
    {
        WorldState world;
        CharacterState healer, victim;
        [SetUp] public void Setup()
        {
            world=new WorldState {expeditionActive=true};
            healer=new CharacterState {id="healer",owner="healer-owner",name="Healer",primary=Discipline.Spirit,secondary=Discipline.Steel,connected=true,position=new Vector3(0,0,40)};
            victim=new CharacterState {id="victim",owner="victim-owner",name="Victim",life=LifeState.Intervention,rescueDeadline=103,position=new Vector3(1,0,40)};
            world.characters.Add(healer);world.characters.Add(victim);
        }
        [Test] public void QualifiedAllyCanRescueOnce()
        {
            Assert.IsTrue(RealmRules.Rescue(world,healer,victim,101));
            Assert.AreEqual(LifeState.Alive,victim.life);Assert.Greater(victim.health,0);Assert.IsTrue(world.rescueUsed);
            victim.life=LifeState.Intervention;Assert.IsFalse(RealmRules.Rescue(world,healer,victim,102));
        }
        [Test] public void RescueAtDeadlineIsRejected()=>Assert.IsFalse(RealmRules.Rescue(world,healer,victim,103));
        [Test] public void RescueOutOfRangeIsRejected(){healer.position=new Vector3(30,0,40);Assert.IsFalse(RealmRules.Rescue(world,healer,victim,101));}
        [Test] public void RescueRequiresSpirit(){healer.primary=Discipline.Shadow;Assert.IsFalse(RealmRules.Rescue(world,healer,victim,101));}
        [Test] public void DeadHealerCannotRescue(){healer.life=LifeState.Dead;Assert.IsFalse(RealmRules.Rescue(world,healer,victim,101));}
        [Test] public void DeathClearsEveryPersonalReward()
        {
            victim.level=9;victim.gold=800;victim.experience=9000;victim.upgrades=4;victim.learned.Add(Discipline.Artifice);
            RealmRules.FinalizeDeath(world,victim);
            Assert.AreEqual(LifeState.Dead,victim.life);Assert.AreEqual(0,victim.level);Assert.AreEqual(0,victim.gold);Assert.AreEqual(0,victim.experience);
            Assert.IsEmpty(victim.inventory);Assert.IsEmpty(victim.learned);Assert.AreEqual(0,victim.potions);Assert.IsFalse(victim.Has(Discipline.Steel));
            Assert.AreEqual(1,world.memorials.Count);
            RealmRules.FinalizeDeath(world,victim);Assert.AreEqual(1,world.memorials.Count);
        }
        [Test] public void DeadCharactersReceiveNoRewards(){RealmRules.FinalizeDeath(world,victim);RealmRules.Award(victim,500,50);Assert.AreEqual(0,victim.gold);Assert.AreEqual(0,victim.experience);}
        [Test] public void ExpiredInterventionBecomesPermanent()
        {
            var sim=new RealmSimulation(world,RealmCampaign.CreateDefaultQuests());sim.Tick(.03f,104);Assert.AreEqual(LifeState.Dead,victim.life);
        }
        [Test] public void NoEligibleAllyMeansImmediateDeath()
        {
            var sim=new RealmSimulation(world,RealmCampaign.CreateDefaultQuests());victim.life=LifeState.Alive;healer.primary=Discipline.Shadow;
            sim.Damage(victim,999,100);Assert.AreEqual(LifeState.Dead,victim.life);
        }
        [Test] public void ReconnectCannotResetRescue()
        {
            world.rescueUsed=true;var sim=new RealmSimulation(world,RealmCampaign.CreateDefaultQuests());sim.Connect(healer.owner,100);Assert.IsTrue(world.rescueUsed);
        }
        [Test] public void CreateRejectsDuplicateDisciplines()
        {
            world.expeditionActive=false;var sim=new RealmSimulation(world,RealmCampaign.CreateDefaultQuests());int count=world.characters.Count;
            sim.Command("new-player",new PlayerCommand {kind="create",text="Rook",primary=0,secondary=0},100);Assert.AreEqual(count,world.characters.Count);
        }
        [Test] public void ClientCannotSupplyDamageOrResurrect()
        {
            var sim=new RealmSimulation(world,RealmCampaign.CreateDefaultQuests());sim.Connect(healer.owner,100);
            sim.Command(healer.owner,new PlayerCommand {kind="resurrect",target=victim.id,choice=999999},100);
            Assert.AreEqual(LifeState.Intervention,victim.life);
        }
        [Test] public void SaveReloadPreservesDeathAndExpeditionRescue()
        {
            string dir=Path.Combine(Path.GetTempPath(),"RealmTest-"+Guid.NewGuid().ToString("N"));
            try
            {
                var store=new RealmSaveStore(dir);RealmRules.FinalizeDeath(world,victim);world.rescueUsed=true;store.Commit(world);
                var loaded=store.Load();Assert.AreEqual(LifeState.Dead,loaded.Find(victim.id).life);Assert.IsTrue(loaded.rescueUsed);
            }
            finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [Test] public void JournalRecoversDeathWhenSnapshotIsTruncated()
        {
            string dir=Path.Combine(Path.GetTempPath(),"RealmTest-"+Guid.NewGuid().ToString("N"));
            try
            {
                var store=new RealmSaveStore(dir);store.Commit(world);RealmRules.FinalizeDeath(world,victim);store.Commit(world);
                File.WriteAllText(Path.Combine(dir,"world.json"),"{broken");File.AppendAllText(Path.Combine(dir,"world.journal"),"{partial");
                Assert.AreEqual(LifeState.Dead,store.Load().Find(victim.id).life);
            }
            finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [Test] public void CorruptSaveDoesNotSilentlyCreateWorld()
        {
            string dir=Path.Combine(Path.GetTempPath(),"RealmTest-"+Guid.NewGuid().ToString("N"));
            try {Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"world.json"),"broken");Assert.Throws<IOException>(()=>new RealmSaveStore(dir).Load());}
            finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [Test] public void EveryDisciplineSlotHasAUniqueAbility()
        {
            var names=new System.Collections.Generic.HashSet<string>();
            Assert.AreEqual(12,RealmAbilities.All.Length);
            for(int d=0;d<6;d++)
                for(int slot=0;slot<2;slot++)
                {
                    var a=RealmAbilities.Get((Discipline)d,slot);
                    Assert.IsTrue(names.Add(a.name),"Duplicate ability name: "+a.name);
                    Assert.Greater(a.energy,0);Assert.Greater(a.cooldown,0);
                }
        }
        [Test] public void AbilitiesCostEnergyAndRespectTheirOwnCooldown()
        {
            var sim=Field(out var hero);
            var cleave=RealmAbilities.Get(Discipline.Steel,0);
            hero.energy=cleave.energy;
            sim.Command(hero.owner,new PlayerCommand{kind="ability",choice=0},100);
            Assert.AreEqual(0,hero.energy,.01f);
            Assert.AreEqual(100+cleave.cooldown,hero.abilityReady,.01f);
            hero.energy=100;
            float health=world.enemies[0].health;
            sim.Command(hero.owner,new PlayerCommand{kind="ability",choice=0},100.1);
            Assert.AreEqual(health,world.enemies[0].health,"Cooldown must reject a second cast.");
            Assert.AreEqual(100,hero.energy,"A rejected cast must not charge energy.");
        }
        [Test] public void AbilitiesAreRejectedWithoutEnoughEnergy()
        {
            var sim=Field(out var hero);
            hero.energy=RealmAbilities.Get(Discipline.Steel,0).energy-1;
            float health=world.enemies[0].health;
            sim.Command(hero.owner,new PlayerCommand{kind="ability",choice=0},100);
            Assert.AreEqual(health,world.enemies[0].health);
            Assert.AreEqual(0,hero.abilityReady);
        }
        [Test] public void MultiTargetAbilitiesHitEveryEnemyInTheirCone()
        {
            var sim=Field(out var hero);
            world.enemies.Add(new EnemyState{id="b",name="Second",position=new Vector3(2,0,42),health=200,maxHealth=200});
            sim.Cast(hero,RealmAbilities.Get(Discipline.Elements,1),100);
            Assert.Less(world.enemies[0].health,200);
            Assert.Less(world.enemies[1].health,200);
        }
        [Test] public void SingleTargetAbilitiesStopAtOneEnemy()
        {
            var sim=Field(out var hero);
            world.enemies.Add(new EnemyState{id="b",name="Second",position=new Vector3(0,0,43),health=200,maxHealth=200});
            sim.Cast(hero,RealmAbilities.Get(Discipline.Wilderness,0),100);
            Assert.AreEqual(1,world.enemies.FindAll(e=>e.health<200).Count);
        }
        [Test] public void RivetShotLeavesTheTargetVulnerable()
        {
            var sim=Field(out var hero);
            sim.Cast(hero,RealmAbilities.Get(Discipline.Artifice,0),100);
            Assert.AreEqual(104,world.enemies[0].vulnerableUntil,.01f);
        }
        [Test] public void SecondWindRestoresWithoutATarget()
        {
            var sim=Field(out var hero);
            world.enemies.Clear();hero.health=10;hero.energy=0;
            sim.Cast(hero,RealmAbilities.Get(Discipline.Steel,1),100);
            Assert.Greater(hero.health,10);Assert.Greater(hero.energy,0);
        }
        [Test] public void RestoringLightCannotRaiseTheDead()
        {
            var sim=Field(out var hero);
            RealmRules.FinalizeDeath(world,victim);victim.position=hero.position;
            sim.Cast(hero,RealmAbilities.Get(Discipline.Spirit,1),100);
            Assert.AreEqual(LifeState.Dead,victim.life);Assert.AreEqual(0,victim.health);
        }
        [Test] public void FactionTrustCanDiverge()
        {
            var quests=RealmCampaign.CreateDefaultQuests();
            Assert.IsTrue(Array.Exists(quests,q=>q.settlementShift!=q.wildernessShift),
                "At least one choice must move the two factions differently, or trust can never diverge.");
        }
        RealmSimulation Field(out CharacterState hero)
        {
            world.enemies.Add(new EnemyState{id="a",name="Target",position=new Vector3(0,0,41),health=200,maxHealth=200});
            healer.primary=Discipline.Steel;healer.secondary=Discipline.Spirit;healer.energy=100;
            hero=healer;
            var sim=new RealmSimulation(world,RealmCampaign.CreateDefaultQuests());
            sim.Connect(healer.owner,100);
            return sim;
        }
        [Test] public void CampaignHasThreeOrderedChaptersAndUniqueIds()
        {
            var quests=RealmCampaign.CreateDefaultQuests();var ids=new System.Collections.Generic.HashSet<string>();
            Assert.AreEqual(18,quests.Length);for(int i=0;i<quests.Length;i++){Assert.IsTrue(ids.Add(quests[i].id));Assert.AreEqual(i/6,quests[i].region);}
        }
    }
}
