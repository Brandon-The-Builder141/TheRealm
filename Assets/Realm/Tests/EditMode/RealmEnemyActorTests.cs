using NUnit.Framework;

namespace TheRealm.Tests
{
    public sealed class RealmEnemyActorTests
    {
        [Test] public void DeathOverridesHitAndAttack()
        {
            Assert.AreEqual(RealmEnemyActor.Pose.Death,RealmEnemyActor.Reaction(
                new RealmEnemyActor.Observed{health=20},new RealmEnemyActor.Observed{health=0,defeated=true,nextAttack=10}));
        }
        [Test] public void ConfirmedAttackOverridesSimultaneousHit()
        {
            Assert.AreEqual(RealmEnemyActor.Pose.Attack,RealmEnemyActor.Reaction(
                new RealmEnemyActor.Observed{health=20,nextAttack=5},new RealmEnemyActor.Observed{health=10,nextAttack=7}));
        }
        [Test] public void RepeatedSnapshotDoesNotReplayAttack()
        {
            var snapshot=new RealmEnemyActor.Observed{health=20,nextAttack=5};
            Assert.AreEqual(RealmEnemyActor.Pose.Idle,RealmEnemyActor.Reaction(snapshot,snapshot));
        }
        [Test] public void DamageWithoutAttackIsAHit()
        {
            Assert.AreEqual(RealmEnemyActor.Pose.Hit,RealmEnemyActor.Reaction(
                new RealmEnemyActor.Observed{health=20},new RealmEnemyActor.Observed{health=10}));
        }
    }
}
