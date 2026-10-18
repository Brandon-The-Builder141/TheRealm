using UnityEngine;

namespace TheRealm
{
    // Owned presentation references; imported models and their import settings remain untouched.
    public sealed class RealmEnemyVisualSet : ScriptableObject
    {
        public GameObject[] models;
        public GameObject blade, staff;
        public Material boneMaterial;
        public AnimationClip idle, run, attack, staffAttack, hit, death;
    }
}
