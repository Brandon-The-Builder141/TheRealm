using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheRealm
{
    public enum LifeState { Alive, Intervention, Dead }
    public enum Discipline { Steel, Shadow, Wilderness, Elements, Spirit, Artifice }

    [Serializable]
    public sealed class CharacterState
    {
        public string id, owner, name;
        public Discipline primary, secondary;
        public LifeState life;
        public int level = 1, gold, upgrades, offense, defense, focus;
        public long experience;
        public int activePrimary = -1, activeSecondary = -1;
        public float health = 100, energy = 100;
        public Vector3 position = new Vector3(0, 0, 3);
        public float yaw;
        public double rescueDeadline, disconnectedUntil, attackReady, abilityReady, dodgeUntil;
        public bool connected, blocking;
        public int potions = 2, bombs = 2;
        public List<Discipline> learned = new List<Discipline>();
        public List<string> inventory = new List<string> { "Wayfarer's blade", "Travel leathers" };
        public bool Has(Discipline discipline) => life != LifeState.Dead && (primary == discipline || secondary == discipline || learned.Contains(discipline));
        public Discipline ActivePrimary => activePrimary < 0 ? primary : (Discipline)activePrimary;
        public Discipline ActiveSecondary => activeSecondary < 0 ? secondary : (Discipline)activeSecondary;
        public float MaxHealth => 100 + defense * 8 + 12 * Mathf.Log(Mathf.Max(1, level), 2) + (Has(Discipline.Steel) ? 25 : 0);
    }

    [Serializable]
    public sealed class EnemyState
    {
        public string id, name;
        public Vector3 position, home;
        public int region;
        public float health, maxHealth, damage, yaw;
        public double nextAttack, telegraphUntil, vulnerableUntil;
        public bool boss, defeated;
    }

    [Serializable]
    public sealed class VoteState
    {
        public string questId, speaker;
        public int turn;
        public double deadline;
        public List<string> participants = new List<string>();
        public List<string> checkedPlayers = new List<string>();
        public List<string> voters = new List<string>();
        public List<int> choices = new List<int>();
        public int successes;
    }

    [Serializable]
    public sealed class WorldState
    {
        public int version = 1;
        public string worldId = Guid.NewGuid().ToString("N");
        public int seed = 731029, revision, chapter, questIndex, expeditionNumber;
        public bool expeditionActive, rescueUsed, ending;
        public float tension;
        public int settlementTrust, wildernessTrust;
        public double nextDirectorEvent;
        public List<CharacterState> characters = new List<CharacterState>();
        public List<EnemyState> enemies = new List<EnemyState>();
        public List<string> completed = new List<string>();
        public List<string> history = new List<string>();
        public List<string> memorials = new List<string>();
        public VoteState dialogue;
        public string lastRoll = "Your fate is unwritten.", directorMood = "Watchful";
        public int lastDie;
        public string fatalError;
        public CharacterState Find(string id) => characters.Find(c => c.id == id);
    }

    [Serializable]
    public sealed class PlayerCommand
    {
        public string kind, text, target;
        public int choice, primary, secondary;
        public float x, z, yaw;
        public bool block;
    }

    public static class RealmRules
    {
        public static long ExperienceForLevel(int level) => (long)Math.Min(long.MaxValue, 100d * level * level);
        public static void Award(CharacterState character, int experience, int gold)
        {
            if (character.life != LifeState.Alive) return;
            character.experience = character.experience > long.MaxValue - experience ? long.MaxValue : character.experience + experience;
            character.gold = (int)Math.Min(int.MaxValue, (long)character.gold + gold);
            while (character.level < int.MaxValue && character.experience >= ExperienceForLevel(character.level)) character.level++;
        }
        public static bool InHaven(Vector3 position) => position.z < 19 && Mathf.Abs(position.x) < 19;
        public static bool CanRescue(WorldState world, CharacterState rescuer, CharacterState target, double now)
        {
            return world.expeditionActive && !world.rescueUsed && rescuer != target && rescuer.connected &&
                rescuer.life == LifeState.Alive && rescuer.Has(Discipline.Spirit) && target.life == LifeState.Intervention &&
                now < target.rescueDeadline && Vector3.Distance(rescuer.position, target.position) <= 12;
        }
        public static bool Rescue(WorldState world, CharacterState rescuer, CharacterState target, double now)
        {
            if (!CanRescue(world, rescuer, target, now)) return false;
            world.rescueUsed = true;
            target.life = LifeState.Alive;
            target.health = target.MaxHealth * .3f;
            target.rescueDeadline = 0;
            return true;
        }
        public static void FinalizeDeath(WorldState world, CharacterState character)
        {
            if (character.life == LifeState.Dead) return;
            world.memorials.Add(character.name + " • level " + character.level + " • expedition " + world.expeditionNumber);
            character.life = LifeState.Dead;
            character.health = character.energy = 0;
            character.level = character.gold = character.upgrades = character.potions = character.bombs = 0;
            character.experience = 0; character.offense = character.defense = character.focus = 0;
            character.activePrimary = character.activeSecondary = -1;
            character.primary = character.secondary = default;
            character.inventory.Clear(); character.learned.Clear();
        }
        public static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);
    }
}
