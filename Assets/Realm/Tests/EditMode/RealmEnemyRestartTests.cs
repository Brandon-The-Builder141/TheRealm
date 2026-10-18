using NUnit.Framework;
using UnityEngine;

namespace TheRealm.Tests
{
    public sealed class RealmEnemyRestartTests
    {
        static RealmSimulation Simulation(WorldState world) => new RealmSimulation(world,RealmCampaign.CreateDefaultQuests());
        [Test] public void EmptyWorldIsNotAClearedEncounter()
        {
            Assert.IsFalse(Simulation(new WorldState()).RestoreClearedEnemies(100));
        }
        [Test] public void PartialClearPreservesDeadAndWoundedEnemies()
        {
            var w=new WorldState();
            w.enemies.Add(new EnemyState{defeated=true,health=0,maxHealth=100});
            w.enemies.Add(new EnemyState{health=17,maxHealth=100,position=new Vector3(3,0,50),nextAttack=123});
            Assert.IsFalse(Simulation(w).RestoreClearedEnemies(100));
            Assert.IsTrue(w.enemies[0].defeated);
            Assert.AreEqual(17,w.enemies[1].health);
            Assert.AreEqual(new Vector3(3,0,50),w.enemies[1].position);
            Assert.AreEqual(123,w.enemies[1].nextAttack);
        }
        [Test] public void FullClearRestoresRosterWithoutResettingProgressOrRevivingCharacters()
        {
            var w=new WorldState{chapter=2,questIndex=18,ending=true,expeditionNumber=9,rescueUsed=true};
            w.characters.Add(new CharacterState{life=LifeState.Dead,level=12,gold=57});
            w.enemies.Add(new EnemyState{id="boss",boss=true,defeated=true,health=0,maxHealth=430,home=new Vector3(0,0,119),telegraphUntil=99,vulnerableUntil=99});
            Assert.IsTrue(Simulation(w).RestoreClearedEnemies(100));
            var e=w.enemies[0];
            Assert.IsFalse(e.defeated);Assert.AreEqual(430,e.health);Assert.AreEqual(e.home,e.position);
            Assert.AreEqual(104,e.nextAttack);Assert.AreEqual(0,e.telegraphUntil);Assert.AreEqual(0,e.vulnerableUntil);
            Assert.AreEqual("boss",e.id);Assert.IsTrue(e.boss);
            Assert.AreEqual(2,w.chapter);Assert.AreEqual(18,w.questIndex);Assert.IsTrue(w.ending);
            Assert.AreEqual(9,w.expeditionNumber);Assert.IsTrue(w.rescueUsed);
            Assert.AreEqual(LifeState.Dead,w.characters[0].life);Assert.AreEqual(12,w.characters[0].level);Assert.AreEqual(57,w.characters[0].gold);
        }
        [Test] public void ReopeningRestoredRosterDoesNotHealItAgain()
        {
            var w=new WorldState();w.enemies.Add(new EnemyState{defeated=true,maxHealth=100});
            var sim=Simulation(w);Assert.IsTrue(sim.RestoreClearedEnemies(100));
            w.enemies[0].health=42;
            var reopened=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(w));
            Assert.IsFalse(Simulation(reopened).RestoreClearedEnemies(110));
            Assert.AreEqual(42,reopened.enemies[0].health);
        }
    }
}
