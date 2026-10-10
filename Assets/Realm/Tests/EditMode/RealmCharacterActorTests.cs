using NUnit.Framework;

namespace TheRealm.Tests
{
    public sealed class RealmCharacterActorTests
    {
        static RealmCharacterActor.Observed Base(Discipline primary = Discipline.Steel, Discipline secondary = Discipline.Shadow) =>
            new RealmCharacterActor.Observed {attackReady = 10, abilityReady = 10, dodgeUntil = 10, potions = 2, bombs = 2, health = 100, energy = 80, primary = primary, secondary = secondary};

        [Test] public void NoChangeMeansNoCue() => Assert.AreEqual(RealmCharacterActor.Cue.None, RealmCharacterActor.Infer(Base(), Base()));
        [Test] public void AttackCooldownStartIsASwing() { var a = Base(); a.attackReady = 10.65; Assert.AreEqual(RealmCharacterActor.Cue.Attack, RealmCharacterActor.Infer(Base(), a)); }
        [Test] public void BombUsesAttackCooldownButThrows() { var a = Base(); a.attackReady = 11; a.bombs = 1; Assert.AreEqual(RealmCharacterActor.Cue.Throw, RealmCharacterActor.Infer(Base(), a)); }
        [Test] public void PotionIsADrink() { var a = Base(); a.potions = 1; a.health = 155; Assert.AreEqual(RealmCharacterActor.Cue.Drink, RealmCharacterActor.Infer(Base(), a)); }
        [Test] public void DamageIsAHit() { var a = Base(); a.health = 85; Assert.AreEqual(RealmCharacterActor.Cue.Hit, RealmCharacterActor.Infer(Base(), a)); }
        [Test] public void DodgeWindowIsADodge() { var a = Base(); a.dodgeUntil = 10.45; a.energy = 60; Assert.AreEqual(RealmCharacterActor.Cue.Dodge, RealmCharacterActor.Infer(Base(), a)); }

        [Test] public void MeleeAbilityIsACleave()
        {
            var a = Base(); a.abilityReady = 12.6; a.energy = 80 - 22 + 1.5f;   // Steel Cleave costs 22, with a little regeneration between snapshots
            Assert.AreEqual(RealmCharacterActor.Cue.Cleave, RealmCharacterActor.Infer(Base(), a));
        }

        [Test] public void EvadeAbilityIsADodgeNotACast()
        {
            var a = Base(); a.abilityReady = 17; a.dodgeUntil = 11.5; a.energy = 80 - 20;       // Veil step and Second wind both cost 20; the dodge window tells them apart
            Assert.AreEqual(RealmCharacterActor.Cue.Dodge, RealmCharacterActor.Infer(Base(), a));
        }

        [Test] public void RestoreAbilityIsACast()
        {
            var a = Base(); a.abilityReady = 19; a.energy = 80 - 20;
            Assert.AreEqual(RealmCharacterActor.Cue.Cast, RealmCharacterActor.Infer(Base(), a));
        }

        [Test] public void RangedAbilityIsACast()
        {
            var before = Base(Discipline.Elements, Discipline.Spirit); var a = before; a.abilityReady = 17; a.energy = 80 - 32;   // Storm pulse
            Assert.AreEqual(RealmCharacterActor.Cue.Cast, RealmCharacterActor.Infer(before, a));
        }
    }
}
