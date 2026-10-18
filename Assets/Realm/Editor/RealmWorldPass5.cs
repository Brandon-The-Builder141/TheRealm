using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace TheRealm.Editor
{
    public static class RealmWorldPass5
    {
        const string EnvironmentName = "The Frontier — original modular environment";
        const string CampaignPath = "Assets/Realm/Content/FrontierCampaign.asset";
        const string TextureFolder = "Assets/Realm/Art/World/Pass5/Ground/Textures/";
        public static readonly string[] CutoutProperties = {
            "_AlphaCutoffEnable", "_AlphaCutoff", "_AlphaCutoffShadow", "_AlphaCutoffPrepass",
            "_AlphaCutoffPostpass", "_UseShadowThreshold", "_DoubleSidedEnable",
            "_DoubleSidedNormalMode", "_SurfaceType", "_CullMode", "_CullModeForward", "_ZWrite"
        };

        static Transform Environment()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play Mode and wait for compilation before using Pass Five tools.");
            var scene = EditorSceneManager.GetActiveScene();
            if(scene.path != RealmBuilder.ScenePath || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Open TheRealm.unity in the main stage. No scene is opened automatically.");
            return scene.GetRootGameObjects().Single(o => o.name == EnvironmentName).transform;
        }

        static QuestDefinition[] Quests()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<RealmCampaign>(CampaignPath);
            if(campaign == null || campaign.quests == null || campaign.quests.Length == 0)
                throw new InvalidOperationException("Missing saved campaign or quest definitions.");
            return campaign.quests;
        }

        [MenuItem("The Realm/Pass Five/Configure Ground Materials")]
        public static void ConfigureGroundMaterials()
        {
            Environment();
            string sourcePath = RealmWorldImport.MaterialFolder + "T_Foliage.mat";
            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if(source == null || source.shader == null || source.shader.name != "HDRP/Lit")
                throw new InvalidOperationException("Missing proven T_Foliage HDRP reference.");
            var names = RealmWorldPolish.Assets.Skip(6).Select(RealmWorldPolish.GroundMaterialName).ToArray();
            var textures = new Dictionary<string, Texture2D>();
            // Preflight the whole batch before modifying any asset.
            foreach(string name in names)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + name + "_BaseColor.png");
                if(texture == null) throw new InvalidOperationException("Missing ground texture: " + name);
                textures.Add(name, texture);
                string path = RealmWorldImport.MaterialFolder + name + ".mat";
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if(existing != null && !(existing is Material))
                    throw new InvalidOperationException("Destination is not a material: " + path);
                if(existing == source) throw new InvalidOperationException("Ground destination resolves to the source material.");
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure Pass Five ground materials");
            int created = 0, reused = 0;
            var changed = new List<Material>();
            try
            {
                foreach(string name in names)
                {
                    string path = RealmWorldImport.MaterialFolder + name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material == null)
                    {
                        if(!AssetDatabase.CopyAsset(sourcePath, path))
                            throw new IOException("Could not duplicate T_Foliage to " + path);
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(material == null) throw new IOException("Could not load copied material: " + path);
                        Undo.RegisterCreatedObjectUndo(material, "Create Pass Five ground material");
                        created++;
                    }
                    else
                    {
                        Undo.RegisterCompleteObjectUndo(material, "Configure Pass Five ground material");
                        reused++;
                    }
                    // Copy the full source state on reuse as well, without replacing the asset or GUID.
                    EditorUtility.CopySerialized(source, material);
                    material.name = name;
                    material.SetTexture("_BaseColorMap", textures[name]);
                    material.SetTextureScale("_BaseColorMap", Vector2.one);
                    material.SetTextureOffset("_BaseColorMap", Vector2.zero);
                    material.SetTexture("_NormalMap", null);
                    material.SetTexture("_MaskMap", null);
                    material.SetColor("_BaseColor", Color.white);
                    // Refresh derived HDRP map keywords after clearing the inherited foliage maps.
                    HDMaterial.ValidateMaterial(material);
                    EditorUtility.SetDirty(material);
                    changed.Add(material);
                }
                CheckMaterials();
                foreach(var material in changed) AssetDatabase.SaveAssetIfDirty(material);
                Undo.CollapseUndoOperations(group);
                Debug.Log("[Realm Pass5] Ground materials configured: Created=" + created + " Reused=" + reused + ". T_Foliage was read only. Scene not saved.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        public static void CheckMaterials()
        {
            var reference = AssetDatabase.LoadAssetAtPath<Material>(RealmWorldImport.MaterialFolder + "T_Foliage.mat");
            if(reference == null || reference.shader == null || reference.shader.name != "HDRP/Lit")
                throw new InvalidOperationException("Missing proven T_Foliage HDRP reference.");
            foreach(string name in RealmWorldPolish.Assets)
            {
                var model = RealmBuilder.LoadWorldKit(name);
                if(model == null) throw new InvalidOperationException("Missing model: " + name);
                if(model.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("Unexpected collider in " + name);
                if(model.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidOperationException("Model has no renderers: " + name);
                foreach(var r in model.GetComponentsInChildren<Renderer>(true))
                    foreach(var m in r.sharedMaterials)
                    {
                        if(m == null || m.shader == null || m.shader.name != "HDRP/Lit")
                            throw new InvalidOperationException("Missing HDRP material: " + name);
                        if(name == "GrassTuft_A" || name == "GrassTuft_B" || name == "FernPatch")
                            if(m != reference) throw new InvalidOperationException("Foliage reference changed: " + name);
                    }
            }
            foreach(string model in RealmWorldPolish.Assets.Skip(6))
            {
                string name = RealmWorldPolish.GroundMaterialName(model);
                var material = AssetDatabase.LoadAssetAtPath<Material>(RealmWorldImport.MaterialFolder + name + ".mat");
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + name + "_BaseColor.png");
                if(material == null || material.shader != reference.shader || texture == null || material.GetTexture("_BaseColorMap") != texture)
                    throw new InvalidOperationException("Create/configure " + name + " using Docs/AI/WorldPass5.md before applying.");
                foreach(string property in CutoutProperties)
                    if(!material.HasProperty(property) || !reference.HasProperty(property) || material.GetFloat(property) != reference.GetFloat(property))
                        throw new InvalidOperationException(name + " must copy T_Foliage property " + property);
                if(material.GetVector("_DoubleSidedConstants") != reference.GetVector("_DoubleSidedConstants"))
                    throw new InvalidOperationException(name + " double-sided constants differ from T_Foliage.");
                foreach(string keyword in new[]{"_ALPHATEST_ON", "_DOUBLESIDED_ON"})
                    if(material.IsKeywordEnabled(keyword) != reference.IsKeywordEnabled(keyword))
                        throw new InvalidOperationException(name + " cutout keyword mismatch: " + keyword);
                if(material.renderQueue != reference.renderQueue || material.GetFloat("_AlphaCutoffEnable") != 1)
                    throw new InvalidOperationException(name + " must use the foliage cutout render queue.");
                if(material.GetTexture("_NormalMap") != null || material.GetTexture("_MaskMap") != null)
                    throw new InvalidOperationException(name + " must not retain the foliage normal/mask maps.");
                if(material.GetTextureScale("_BaseColorMap") != Vector2.one || material.GetTextureOffset("_BaseColorMap") != Vector2.zero || material.GetColor("_BaseColor") != Color.white)
                    throw new InvalidOperationException(name + " requires unit tiling, zero offset and white tint.");
            }
        }

        [MenuItem("The Realm/Pass Five/Apply World Polish (Undoable)")]
        public static void Apply()
        {
            var parent = Environment();
            var existing = parent.GetComponentsInChildren<Transform>(true).Where(t => t.name == RealmWorldPolish.RootName).ToArray();
            if(existing.Length > 1) throw new InvalidOperationException("Multiple polish roots exist; no objects changed.");
            if(existing.Length == 1)
            {
                Validate();
                Debug.Log("[Realm Pass5] Existing polish preserved. No duplicate or replacement created.");
                return;
            }
            CheckMaterials();
            var quests = Quests();
            var kit = RealmArt.KitProvider;
            var materials = RealmArt.MaterialProvider;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply world pass five");
            GameObject root = null;
            bool registered = false;
            try
            {
                RealmArt.KitProvider = name => UnityEngine.Object.Instantiate(RealmBuilder.LoadWorldKit(name));
                RealmArt.MaterialProvider = name => AssetDatabase.LoadAssetAtPath<Material>(RealmWorldImport.MaterialFolder + name + ".mat");
                root = RealmWorldPolish.Build(parent, quests);
                ValidateRoot(parent, root, quests);
                // Register the finished hierarchy once so Undo/Redo includes every child and material binding.
                Undo.RegisterCreatedObjectUndo(root, "Apply world pass five");
                registered = true;
                EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = root;
                Debug.Log("[Realm Pass5] Added " + root.GetComponentsInChildren<Renderer>(true).Length + " renderers. Scene is unsaved; review and save manually. Ctrl+Z removes this pass.");
            }
            catch
            {
                if(registered) Undo.RevertAllDownToGroup(group);
                else if(root != null) UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
            finally { RealmArt.KitProvider = kit; RealmArt.MaterialProvider = materials; }
        }

        public static string ValidateRoot(Transform parent, GameObject root, QuestDefinition[] quests)
        {
            if(root == null || root.transform.parent != parent || root.transform.childCount != 4)
                throw new InvalidOperationException("Expected one polish root with four placement groups.");
            if(parent.GetComponentsInChildren<Transform>(true).Count(t => t.name == RealmWorldPolish.RootName) != 1)
                throw new InvalidOperationException("Expected exactly one polish root.");
            if(root.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Polish must not contain colliders.");
            var placement = new RealmWorldPolish.Placement(parent, quests);
            var patches = new List<Bounds>();
            int objects = 0;
            foreach(Transform group in root.transform)
                foreach(Transform item in group)
                {
                    var bounds = RealmWorldPolish.BoundsOf(item.gameObject);
                    if(RealmWorldPolish.Assets.Skip(6).Contains(item.name))
                    {
                        if(patches.Any(b => RealmWorldPolish.OverlapsXZ(bounds,b)))
                            throw new InvalidOperationException("Overlapping ground patches: " + item.name);
                        patches.Add(bounds);
                    }
                    if(!placement.IsClear(bounds,item.name == "RootCluster" || item.name == "MossPatch")) throw new InvalidOperationException("Clearance failed: " + item.name + " at " + item.position);
                    if(!placement.FootprintHeight(bounds, out float height) || Mathf.Abs(bounds.min.y - height) > .003f)
                        throw new InvalidOperationException("Grounding failed: " + item.name);
                    foreach(var r in item.GetComponentsInChildren<Renderer>(true))
                        foreach(var m in r.sharedMaterials)
                        {
                            if(m == null || m.shader == null || m.shader.name != "HDRP/Lit")
                                throw new InvalidOperationException("HDRP binding failed: " + item.name);
                            if(RealmWorldPolish.Assets.Skip(6).Contains(item.name) &&
                                m != AssetDatabase.LoadAssetAtPath<Material>(RealmWorldImport.MaterialFolder + RealmWorldPolish.GroundMaterialName(item.name) + ".mat"))
                                throw new InvalidOperationException("Incorrect ground material binding: " + item.name);
                        }
                    objects++;
                }
            if(objects == 0) throw new InvalidOperationException("Polish root is empty.");
            return "Objects=" + objects + " Renderers=" + root.GetComponentsInChildren<Renderer>(true).Length + " Colliders=0 Clearance=Passed Grounding=Passed";
        }

        [MenuItem("The Realm/Pass Five/Validate World Polish")]
        public static void Validate()
        {
            var parent = Environment();
            CheckMaterials();
            var root = parent.GetComponentsInChildren<Transform>(true).Single(t => t.name == RealmWorldPolish.RootName).gameObject;
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            report.AppendLine(ValidateRoot(parent, root, Quests()));
            report.AppendLine("Visual transitions, repetition and temporal shimmer require camera inspection.");
            Directory.CreateDirectory("Logs/Realm");
            File.WriteAllText("Logs/Realm/pass5-validation.txt", report.ToString());
            Debug.Log("[Realm Pass5] " + report);
        }

        [MenuItem("The Realm/Pass Five/Render Before Preview")]
        public static void Before()
        {
            var parent = Environment();
            if(parent.GetComponentsInChildren<Transform>(true).Any(t => t.name == RealmWorldPolish.RootName))
                throw new InvalidOperationException("Before preview requires no polish root.");
            Preview("before");
        }

        [MenuItem("The Realm/Pass Five/Render After Preview")]
        public static void After()
        {
            Validate();
            Preview("after");
        }

        static void Preview(string phase)
        {
            RealmWorldPreview.Render();
            File.Copy("Logs/Realm/world-preview.png", "Logs/Realm/pass5-" + phase + ".png", true);
        }
    }
}
