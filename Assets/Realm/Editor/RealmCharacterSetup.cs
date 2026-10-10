using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheRealm.Editor
{
    // Builds the Adventurer animator controller and the runtime prefab loaded by RealmCharacterActor.
    public static class RealmCharacterSetup
    {
        const string ModelPath = "Assets/Realm/Art/Characters/Adventurer.fbx";
        const string ClipsPath = "Assets/Realm/Art/Characters/Adventurer_Animations.fbx";
        const string AnimationFolder = "Assets/Realm/Art/Characters/Animation/";
        const string ControllerPath = AnimationFolder + "AdventurerAnimator.controller";
        const string MaskPath = AnimationFolder + "UpperBody.mask";
        const string PrefabPath = "Assets/Realm/Resources/" + RealmCharacterActor.PrefabPath + ".prefab";

        [MenuItem("The Realm/Build Character Prefabs")]
        public static void Build()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(ClipsPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            string[] required = { "Idle", "Walk", "Run", "Strafe", "StrafeRight", "Attack1", "Attack2", "Cleave", "Block", "Dodge", "Hit", "Cast", "Drink", "Throw", "Downed" };
            var missing = required.Where(n => !clips.ContainsKey(n)).ToArray();
            if (missing.Length > 0) throw new System.InvalidOperationException("Adventurer animation clips missing: " + string.Join(", ", missing));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new System.InvalidOperationException("Adventurer model not imported: " + ModelPath);

            Directory.CreateDirectory(AnimationFolder); Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var mask = BuildMask(model);
            var controller = BuildController(clips, mask);
            BuildPrefab(model, controller);

            var report = new StringBuilder();
            report.AppendLine("controller=" + ControllerPath + " layers=" + string.Join(",", controller.layers.Select(l => l.name + "[" + l.stateMachine.states.Length + "]")));
            report.AppendLine("parameters=" + string.Join(",", controller.parameters.Select(p => p.name)));
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var savedAnimator = saved != null ? saved.GetComponent<Animator>() : null;
            report.AppendLine("prefab=" + PrefabPath + " loadable=" + (saved != null) + " controller=" + (savedAnimator != null && savedAnimator.runtimeAnimatorController != null)
                + " avatar=" + (savedAnimator != null && savedAnimator.avatar != null && savedAnimator.avatar.isValid)
                + " maskActive=" + Enumerable.Range(0, mask.transformCount).Count(mask.GetTransformActive) + "/" + mask.transformCount);
            Directory.CreateDirectory("Logs/Realm");
            File.WriteAllText("Logs/Realm/character-setup.txt", report.ToString());
            Debug.Log("[Realm Characters] " + report);
        }

        // Generic rig: mask by transform path. The action layer drives the spine, arms and head; hips and legs keep walking.
        static AvatarMask BuildMask(GameObject model)
        {
            // Assets are rebuilt in place, never deleted: a deleted-and-recreated asset keeps its GUID but existing references
            // resolve to the destroyed object for the rest of the editor session.
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            bool created = mask == null;
            if (created) mask = new AvatarMask();
            mask.transformCount = 0;
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.AddTransformPath(model.transform, true);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                string bone = path.Substring(path.LastIndexOf('/') + 1);
                bool lower = bone == "Hips" || bone.StartsWith("UpperLeg") || bone.StartsWith("LowerLeg") || bone.StartsWith("Foot") || bone.StartsWith("Toes")
                             || !path.Contains("Hips");
                mask.SetTransformActive(i, !lower);
            }
            if (created) AssetDatabase.CreateAsset(mask, MaskPath); else EditorUtility.SetDirty(mask);
            return mask;
        }

        static AnimatorController BuildController(System.Collections.Generic.Dictionary<string, AnimationClip> clips, AvatarMask mask)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            else
            {
                controller.layers = new AnimatorControllerLayer[0];
                controller.parameters = new AnimatorControllerParameter[0];
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                    if (sub != controller && sub != null) Object.DestroyImmediate(sub, true);
                controller.AddLayer("Base Layer");
            }
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Strafe", AnimatorControllerParameterType.Float);
            controller.AddParameter("Block", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Downed", AnimatorControllerParameterType.Bool);
            controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            foreach (var trigger in new[] { "Attack", "Cleave", "Cast", "Dodge", "Drink", "Throw", "Hit" })
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            // Base layer: direction-aware locomotion, full-body dodge and the downed state.
            var root = controller.layers[0].stateMachine;
            var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.FreeformDirectional2D, blendParameter = "Strafe", blendParameterY = "Speed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.children = new[]
            {
                Child(clips["Idle"], 0, 0),
                Child(clips["Walk"], 0, 2.5f), Child(clips["Run"], 0, 6f),
                Child(clips["Walk"], 0, -2.5f, -1), Child(clips["Run"], 0, -6f, -1),
                Child(clips["Strafe"], -2.5f, 0, .6f), Child(clips["Strafe"], -6f, 0, 1.2f),
                Child(clips["StrafeRight"], 2.5f, 0, .6f), Child(clips["StrafeRight"], 6f, 0, 1.2f),
            };
            var locomotion = root.AddState("Locomotion"); locomotion.motion = tree; root.defaultState = locomotion;
            var dodge = root.AddState("Dodge"); dodge.motion = clips["Dodge"];
            var downed = root.AddState("Downed"); downed.motion = clips["Downed"];
            var toDodge = root.AddAnyStateTransition(dodge); toDodge.AddCondition(AnimatorConditionMode.If, 0, "Dodge"); Quick(toDodge, .05f); toDodge.canTransitionToSelf = false;
            ExitTo(dodge, locomotion, .8f);
            var toDowned = root.AddAnyStateTransition(downed); toDowned.AddCondition(AnimatorConditionMode.If, 0, "Downed"); Quick(toDowned, .2f); toDowned.canTransitionToSelf = false;
            var up = downed.AddTransition(locomotion); up.AddCondition(AnimatorConditionMode.IfNot, 0, "Downed"); Quick(up, .35f);

            // Upper-body layer: sword work and item use while the legs keep moving.
            controller.AddLayer("Actions");
            var layers = controller.layers;
            layers[1].avatarMask = mask; layers[1].defaultWeight = 1; layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;
            var actions = controller.layers[1].stateMachine;
            var empty = actions.AddState("Empty"); actions.defaultState = empty;
            var block = actions.AddState("Block"); block.motion = clips["Block"];
            var raise = empty.AddTransition(block); raise.AddCondition(AnimatorConditionMode.If, 0, "Block"); Quick(raise, .12f);
            var lower = block.AddTransition(empty); lower.AddCondition(AnimatorConditionMode.IfNot, 0, "Block"); Quick(lower, .15f);
            OneShot(actions, empty, clips["Attack1"], "Attack", .04f, a => a.AddCondition(AnimatorConditionMode.Equals, 0, "AttackIndex"));
            OneShot(actions, empty, clips["Attack2"], "Attack", .04f, a => a.AddCondition(AnimatorConditionMode.Equals, 1, "AttackIndex"));
            foreach (var name in new[] { "Cleave", "Cast", "Drink", "Throw", "Hit" }) OneShot(actions, empty, clips[name], name, name == "Hit" ? .03f : .06f, null);

            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            return controller;
        }

        static ChildMotion Child(Motion motion, float x, float y, float timeScale = 1) =>
            new ChildMotion { motion = motion, position = new Vector2(x, y), timeScale = timeScale };

        static void Quick(AnimatorStateTransition t, float duration) { t.hasExitTime = false; t.duration = duration; t.hasFixedDuration = true; }

        static void ExitTo(AnimatorState from, AnimatorState to, float exitTime)
        {
            var t = from.AddTransition(to); t.hasExitTime = true; t.exitTime = exitTime; t.duration = .12f; t.hasFixedDuration = true;
        }

        static void OneShot(AnimatorStateMachine machine, AnimatorState empty, AnimationClip clip, string trigger, float blendIn, System.Action<AnimatorStateTransition> extra)
        {
            var state = machine.AddState(clip.name); state.motion = clip;
            var enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0, trigger); extra?.Invoke(enter);
            Quick(enter, blendIn); enter.canTransitionToSelf = true;
            ExitTo(state, empty, .85f);
        }

        static void BuildPrefab(GameObject model, AnimatorController controller)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                // A plain prefab (not a model variant) so FBX re-exports can never silently drop the controller override.
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "Adventurer";
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                if (instance.GetComponent<RealmCharacterActor>() == null) instance.AddComponent<RealmCharacterActor>();
                foreach (var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = false;
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
