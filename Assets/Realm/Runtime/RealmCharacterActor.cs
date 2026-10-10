using System.Collections.Generic;
using UnityEngine;

namespace TheRealm
{
    // Animated presentation of a player character. Reads replicated CharacterState and infers one-shot actions
    // (attacks, casts, dodges, potions, bombs, hits) from how the state changed, so gameplay and networking stay untouched.
    public sealed class RealmCharacterActor : MonoBehaviour
    {
        public const string PrefabPath = "Characters/Adventurer";
        static GameObject prefab;
        static bool prefabLoaded;

        public enum Cue { None, Attack, Cleave, Cast, Dodge, Drink, Throw, Hit }

        // Snapshot of the replicated fields that drive animation.
        public struct Observed
        {
            public double attackReady, abilityReady, dodgeUntil;
            public int potions, bombs;
            public float health, energy;
            public Discipline primary, secondary;
            public static Observed From(CharacterState c) => new Observed
            {
                attackReady = c.attackReady, abilityReady = c.abilityReady, dodgeUntil = c.dodgeUntil, potions = c.potions, bombs = c.bombs,
                health = c.health, energy = c.energy, primary = c.ActivePrimary, secondary = c.ActiveSecondary
            };
        }

        static readonly int SpeedId = Animator.StringToHash("Speed"), StrafeId = Animator.StringToHash("Strafe"), BlockId = Animator.StringToHash("Block"),
            DownedId = Animator.StringToHash("Downed"), AttackIndexId = Animator.StringToHash("AttackIndex"), BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Dictionary<Cue, int> CueIds = new Dictionary<Cue, int>
        {
            {Cue.Attack, Animator.StringToHash("Attack")}, {Cue.Cleave, Animator.StringToHash("Cleave")}, {Cue.Cast, Animator.StringToHash("Cast")},
            {Cue.Dodge, Animator.StringToHash("Dodge")}, {Cue.Drink, Animator.StringToHash("Drink")}, {Cue.Throw, Animator.StringToHash("Throw")},
            {Cue.Hit, Animator.StringToHash("Hit")}
        };

        Animator animator;
        Observed last;
        bool primed;
        int attackIndex;
        Vector3 lastPosition, velocity;

        public static RealmCharacterActor Spawn(Transform parent, string name, Color tint)
        {
            if (!prefabLoaded) { prefab = Resources.Load<GameObject>(PrefabPath); prefabLoaded = true; }
            if (prefab == null) return null;
            var obj = Instantiate(prefab, parent, false); obj.name = name;
            var actor = obj.GetComponent<RealmCharacterActor>();
            if (actor == null) actor = obj.AddComponent<RealmCharacterActor>();
            actor.Tint(tint);
            return actor;
        }

        void Awake() => animator = GetComponentInChildren<Animator>();

        // Reappearing (reconnect, respawn) must not read as a sprint or replay stale actions.
        void OnEnable() { lastPosition = transform.position; velocity = Vector3.zero; primed = false; }

        // Cloth is baked neutral; the discipline colour is applied per instance.
        public void Tint(Color color)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].name.StartsWith("Adventurer_Cloth")) continue;
                    r.GetPropertyBlock(block, i); block.SetColor(BaseColorId, color); r.SetPropertyBlock(block, i);
                }
            }
        }

        public void Sync(CharacterState c, float dt)
        {
            if (animator == null) return;
            if (!primed) { last = Observed.From(c); primed = true; lastPosition = transform.position; velocity = Vector3.zero; }
            // Locomotion from the displayed motion, in the character's local frame.
            Vector3 frameVelocity = dt > 0 ? (transform.position - lastPosition) / dt : Vector3.zero;
            lastPosition = transform.position;
            velocity = Vector3.Lerp(velocity, frameVelocity, 1 - Mathf.Exp(-dt * 10));
            Vector3 local = transform.InverseTransformDirection(velocity);
            animator.SetFloat(SpeedId, local.z, .08f, dt);
            animator.SetFloat(StrafeId, local.x, .08f, dt);
            bool alive = c.life == LifeState.Alive;
            animator.SetBool(BlockId, alive && c.blocking);
            animator.SetBool(DownedId, c.life == LifeState.Intervention);

            var now = Observed.From(c);
            var cue = alive ? Infer(last, now) : Cue.None;
            last = now;
            if (cue == Cue.None) return;
            if (cue == Cue.Attack) { attackIndex = 1 - attackIndex; animator.SetInteger(AttackIndexId, attackIndex); }
            animator.SetTrigger(CueIds[cue]);
        }

        // Pure: which one-shot animation does this change in replicated state represent?
        public static Cue Infer(Observed before, Observed after)
        {
            if (after.bombs < before.bombs) return Cue.Throw;
            if (after.attackReady > before.attackReady + .01) return Cue.Attack;
            if (after.abilityReady > before.abilityReady + .01)
            {
                if (after.dodgeUntil > before.dodgeUntil + .01) return Cue.Dodge;   // only Evade abilities open a dodge window
                var ability = IdentifyAbility(before, after);
                if (ability == null) return Cue.Cast;
                if (ability.shape == AbilityShape.Evade) return Cue.Dodge;
                if (ability.shape == AbilityShape.Strike && ability.range <= 4.5f) return Cue.Cleave;
                return Cue.Cast;
            }
            if (after.dodgeUntil > before.dodgeUntil + .01) return Cue.Dodge;
            if (after.potions < before.potions) return Cue.Drink;
            if (after.health < before.health - .5f) return Cue.Hit;
            return Cue.None;
        }

        // The ability slot is not replicated; its energy cost is, so pick the equipped ability whose cost best explains the drop.
        // Regeneration between snapshots only ever hides part of the cost, so a candidate may not cost less than what was observed.
        public static AbilityDefinition IdentifyAbility(Observed before, Observed after)
        {
            float spent = before.energy - after.energy;
            AbilityDefinition best = null; float bestExcess = float.MaxValue;
            foreach (var discipline in new[] { before.primary, before.secondary })
                for (int slot = 0; slot < 2; slot++)
                {
                    var a = RealmAbilities.Get(discipline, slot);
                    float excess = a.energy - spent;
                    if (excess < -.25f || excess >= bestExcess) continue;
                    bestExcess = excess; best = a;
                }
            return best;
        }
    }
}
