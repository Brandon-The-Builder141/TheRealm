using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace TheRealm.Editor
{
    // Imports the original Blender characters (ArtSource/Characters/RealmCharacters.blend).
    // Every character shares one 22-bone skeleton, so animation is imported as Generic and plays back exactly as authored
    // (Humanoid retargeting discards hand roll, which misaligns held weapons).
    // Baked texture sets (<Material>_BaseColor/_Normal/_Mask.png) become HDRP Lit materials remapped onto the FBX by name.
    public sealed class RealmCharacterImport : AssetPostprocessor
    {
        const string Folder = "Assets/Realm/Art/Characters/";
        const string TextureFolder = Folder + "Textures/";
        const string MaterialFolder = Folder + "Materials/";
        // Bump when import settings change so Unity reimports existing character assets.
        public override uint GetVersion() => 6;

        static bool IsCharacter(string path) => path.StartsWith(Folder) && !path.StartsWith(TextureFolder) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) && !IsAnimationSet(path);
        static bool IsAnimationSet(string path) => path.StartsWith(Folder) && path.EndsWith("_Animations.fbx", System.StringComparison.OrdinalIgnoreCase);
        static readonly HashSet<string> LoopingClips = new HashSet<string> { "Idle", "Walk", "Run", "Strafe", "StrafeRight", "Block", "Downed" };
        static bool IsCharacterTexture(string path) => path.StartsWith(TextureFolder) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (IsAnimationSet(assetPath))
            {
                var set = (ModelImporter)assetImporter;
                set.globalScale = 1; set.bakeAxisConversion = false;
                set.importCameras = set.importLights = false; set.importAnimation = true;
                set.materialImportMode = ModelImporterMaterialImportMode.None;
                set.preserveHierarchy = true;   // skeleton-only files would otherwise drop the Rig_Humanoid node, breaking clip binding paths
                set.animationType = ModelImporterAnimationType.Generic;
                set.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                set.resampleCurves = true;
                set.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
                return;
            }
            if (!IsCharacter(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1;
            importer.bakeAxisConversion = false;
            importer.importCameras = importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            foreach (var file in Directory.Exists(MaterialFolder) ? Directory.GetFiles(MaterialFolder, "*.mat") : new string[0])
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(file.Replace('\\', '/'));
                if (material != null) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), material);
            }
        }

        void OnPreprocessAnimation()
        {
            if (!IsAnimationSet(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                string take = clip.takeName;
                clip.name = take.Substring(take.LastIndexOf('|') + 1);
                bool loop = LoopingClips.Contains(clip.name);
                clip.loopTime = loop; clip.loopPose = loop;
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!IsCharacterTexture(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            if (assetPath.EndsWith("_Normal.png")) importer.textureType = TextureImporterType.NormalMap;
            else { importer.textureType = TextureImporterType.Default; importer.sRGBTexture = assetPath.EndsWith("_BaseColor.png"); }
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Any(IsCharacterTexture)) EditorApplication.delayCall += EnsureMaterials;
            var report = new StringBuilder();
            foreach (var path in imported)
            {
                if (IsAnimationSet(path))
                {
                    var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                    report.AppendLine(path + " clips=" + string.Join(",", clips.Select(c => c.name + "(" + c.length.ToString("0.00") + "s" + (c.isLooping ? ",loop" : "") + (c.isHumanMotion ? ",human" : ",generic") + (c.empty ? ",EMPTY" : "") + ")")));
                    continue;
                }
                if (!IsCharacter(path)) continue;
                Avatar avatar = null; int meshes = 0, triangles = 0;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Avatar a) avatar = a;
                    if (asset is Mesh m) { meshes++; triangles += m.triangles.Length / 3; }
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var bounds = new Bounds();
                var materials = new List<string>();
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    if (r is SkinnedMeshRenderer s && s.sharedMesh != null) bounds.Encapsulate(s.sharedMesh.bounds);
                    foreach (var m in r.sharedMaterials) if (m != null) materials.Add(m.name + (AssetDatabase.GetAssetPath(m) == path ? "(embedded)" : "(remapped)"));
                }
                var lodGroup = model.GetComponentInChildren<LODGroup>();
                string lods = lodGroup == null ? "none" : string.Join("/", lodGroup.GetLODs().Select(l => l.renderers.Sum(r => r is SkinnedMeshRenderer s && s.sharedMesh != null ? s.sharedMesh.triangles.Length / 3 : 0)));
                report.AppendLine(path + " avatarValid=" + (avatar != null && avatar.isValid)
                    + " meshes=" + meshes + " triangles=" + triangles + " lodTriangles=" + lods + " boundsSize=" + bounds.size + " materials=" + string.Join(",", materials.Distinct()));
            }
            if (report.Length == 0) return;
            Directory.CreateDirectory("Logs/Realm");
            File.WriteAllText("Logs/Realm/character-import.txt", report.ToString());
            Debug.Log("[Realm Characters] " + report);
        }

        // One HDRP Lit material per baked texture set. Cloth sets stay white so gameplay can tint _BaseColor per discipline.
        static void EnsureMaterials()
        {
            if (!Directory.Exists(TextureFolder)) return;
            Directory.CreateDirectory(MaterialFolder);
            bool created = false;
            var lines = new StringBuilder();
            foreach (var file in Directory.GetFiles(TextureFolder, "*_BaseColor.png"))
            {
                string set = Path.GetFileNameWithoutExtension(file).Replace("_BaseColor", "");
                string path = MaterialFolder + set + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("HDRP/Lit")) { name = set };
                    AssetDatabase.CreateAsset(material, path);
                    created = true;
                }
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + set + "_BaseColor.png"));
                material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + set + "_Normal.png"));
                material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + set + "_Mask.png"));
                material.SetFloat("_NormalScale", 1f);
                material.SetFloat("_MetallicRemapMin", 0f); material.SetFloat("_MetallicRemapMax", 1f);
                material.SetFloat("_SmoothnessRemapMin", 0f); material.SetFloat("_SmoothnessRemapMax", 1f);
                material.SetFloat("_AORemapMin", 0f); material.SetFloat("_AORemapMax", 1f);
                material.enableInstancing = true;
                HDMaterial.ValidateMaterial(material);
                EditorUtility.SetDirty(material);
                lines.AppendLine(path + " normal=" + material.IsKeywordEnabled("_NORMALMAP") + " mask=" + material.IsKeywordEnabled("_MASKMAP"));
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/Realm");
            File.WriteAllText("Logs/Realm/character-materials.txt", lines.ToString());
            if (!created) return;
            foreach (var fbx in Directory.GetFiles(Folder, "*.fbx")) AssetDatabase.ImportAsset(fbx.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
        }
    }
}
