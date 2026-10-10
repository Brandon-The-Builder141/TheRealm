using System;

namespace TheRealm
{
    public enum AbilityShape { Strike, SelfRestore, AllyRestore, Evade }

    [Serializable]
    public sealed class AbilityDefinition
    {
        public string name, note;
        public Discipline discipline;
        public int slot;
        public AbilityShape shape;
        public float energy, cooldown, range, minDot, damage, amount, vulnerability;
        public bool multi, backstab;
    }

    public static class RealmAbilities
    {
        // minDot is the forward cone: 1 is a needle, 0 is a half circle, -1 reaches all around.
        public static readonly AbilityDefinition[] All =
        {
            new AbilityDefinition{discipline=Discipline.Steel,slot=0,name="Cleave",note="Wide arc. Strikes every enemy in front of you.",shape=AbilityShape.Strike,energy=22,cooldown=2.6f,range=4.2f,minDot=0f,damage=30,multi=true},
            new AbilityDefinition{discipline=Discipline.Steel,slot=1,name="Second wind",note="Recover health and stamina. No target required.",shape=AbilityShape.SelfRestore,energy=20,cooldown=9,amount=26},
            new AbilityDefinition{discipline=Discipline.Shadow,slot=0,name="Ambush",note="Single target. Devastating from behind.",shape=AbilityShape.Strike,energy=24,cooldown=3.2f,range=4,minDot=.25f,damage=34,backstab=true},
            new AbilityDefinition{discipline=Discipline.Shadow,slot=1,name="Veil step",note="Slip out of reach. Attacks pass through you.",shape=AbilityShape.Evade,energy=20,cooldown=7,amount=1.5f},
            new AbilityDefinition{discipline=Discipline.Wilderness,slot=0,name="Piercing shot",note="Long range. Rewards a steady aim.",shape=AbilityShape.Strike,energy=20,cooldown=2.4f,range=24,minDot=.5f,damage=30},
            new AbilityDefinition{discipline=Discipline.Wilderness,slot=1,name="Volley",note="Arrows fall across a distant spread.",shape=AbilityShape.Strike,energy=30,cooldown=6,range=22,minDot=.25f,damage=24,multi=true},
            new AbilityDefinition{discipline=Discipline.Elements,slot=0,name="Arc bolt",note="A focused bolt at range.",shape=AbilityShape.Strike,energy=22,cooldown=2.2f,range=20,minDot=.4f,damage=33},
            new AbilityDefinition{discipline=Discipline.Elements,slot=1,name="Storm pulse",note="Bursts outward in every direction.",shape=AbilityShape.Strike,energy=32,cooldown=7,range=9,minDot=-1,damage=30,multi=true},
            new AbilityDefinition{discipline=Discipline.Spirit,slot=0,name="Radiant strike",note="A close blow that returns some of its light to you.",shape=AbilityShape.Strike,energy=22,cooldown=3,range=4,minDot=.25f,damage=26,amount=10},
            new AbilityDefinition{discipline=Discipline.Spirit,slot=1,name="Restoring light",note="Heals nearby allies. It cannot reverse death.",shape=AbilityShape.AllyRestore,energy=34,cooldown=9,range=10,amount=32},
            new AbilityDefinition{discipline=Discipline.Artifice,slot=0,name="Rivet shot",note="Breaks armour. The target takes heavier hits for a while.",shape=AbilityShape.Strike,energy=20,cooldown=2.8f,range=18,minDot=.4f,damage=26,vulnerability=4},
            new AbilityDefinition{discipline=Discipline.Artifice,slot=1,name="Blast charge",note="A thrown charge that catches a broad group.",shape=AbilityShape.Strike,energy=34,cooldown=8,range=16,minDot=.1f,damage=44,multi=true}
        };
        public const float VulnerableMultiplier = 1.35f;
        public static AbilityDefinition Get(Discipline discipline, int slot)
        {
            foreach (var a in All) if (a.discipline == discipline && a.slot == slot) return a;
            throw new ArgumentOutOfRangeException(nameof(slot), "No ability is defined for " + discipline + " slot " + slot + ".");
        }
        public static string Name(int discipline, int slot) => Get((Discipline)discipline, slot).name;
    }
}
