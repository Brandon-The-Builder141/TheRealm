using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace TheRealm.Editor
{
    // Imports the original world kit (ArtSource/World/RealmWorld.blend): static meshes with world-scale UVs sharing the
    // tileable W_* material library. Each <Set>_BaseColor/_Normal/_Mask.png becomes an HDRP Lit material remapped by name.
    public sealed class RealmWorldImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Realm/Art/World/";
        const string TextureFolder = Folder + "Textures/";
        public const string MaterialFolder = Folder + "Materials/";
        public override uint GetVersion() => 2;

        static bool IsModel(string path) => path.StartsWith(Folder) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        static bool IsTexture(string path) => path.StartsWith(TextureFolder) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!IsModel(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1; importer.bakeAxisConversion = false;
            importer.importCameras = importer.importLights = false;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.isReadable = false;
            foreach (var file in Directory.Exists(MaterialFolder) ? Directory.GetFiles(MaterialFolder, "*.mat") : new string[0])
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(file.Replace('\\', '/'));
                if (material != null) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), material);
            }
        }

        void OnPreprocessTexture()
        {
            if (!IsTexture(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048; importer.mipmapEnabled = true; importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat;
            if (assetPath.EndsWith("_Normal.png")) importer.textureType = TextureImporterType.NormalMap;
            else { importer.textureType = TextureImporterType.Default; importer.sRGBTexture = assetPath.EndsWith("_BaseColor.png"); }
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Any(IsTexture)) EditorApplication.delayCall += EnsureMaterials;
        }

        [MenuItem("The Realm/Rebuild World Materials")]
        public static void EnsureMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            bool created = false; var report = new StringBuilder();
            foreach (var file in Directory.Exists(TextureFolder) ? Directory.GetFiles(TextureFolder, "*_BaseColor.png") : new string[0])
            {
                string set = Path.GetFileNameWithoutExtension(file).Replace("_BaseColor", "");
                var material = Load(set, ref created);
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + set + "_BaseColor.png"));
                material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + set + "_Normal.png"));
                material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + set + "_Mask.png"));
                material.SetFloat("_NormalScale", 1f);
                material.SetFloat("_MetallicRemapMin", 0f); material.SetFloat("_MetallicRemapMax", 1f);
                material.SetFloat("_SmoothnessRemapMin", 0f); material.SetFloat("_SmoothnessRemapMax", 1f);
                material.SetFloat("_AORemapMin", 0f); material.SetFloat("_AORemapMax", 1f);
                if (set == "T_Foliage")
                {
                    // Cards with a cutout leaf texture: clip the transparent pixels and light both faces.
                    material.SetFloat("_AlphaCutoffEnable", 1f); material.SetFloat("_AlphaCutoff", 0.4f);
                    material.SetFloat("_UseShadowThreshold", 1f); material.SetFloat("_AlphaCutoffShadow", 0.4f);
                    material.SetFloat("_DoubleSidedEnable", 1f); material.SetFloat("_DoubleSidedNormalMode", 1f);
                    material.SetFloat("_CullMode", (float)UnityEngine.Rendering.CullMode.Off);
                    material.SetFloat("_ZTestGBuffer", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                    material.EnableKeyword("_DOUBLESIDED_ON");
                }
                Finish(material); report.AppendLine(set + " normal=" + material.IsKeywordEnabled("_NORMALMAP") + " mask=" + material.IsKeywordEnabled("_MASKMAP")
                    + (set == "T_Foliage" ? " alphaClip=" + material.IsKeywordEnabled("_ALPHATEST_ON") + " doubleSided=" + material.IsKeywordEnabled("_DOUBLESIDED_ON") : ""));
            }
            // Warm light behind windows and inside lanterns.
            var glow = Load("W_Glow", ref created);
            glow.SetColor("_BaseColor", new Color(.35f, .16f, .05f));
            glow.SetColor("_EmissiveColor", new Color(1f, .45f, .14f) * 900f);   // nits: must read against daylight exposure (EV 12)
            glow.SetColor("_EmissiveColorLDR", new Color(1f, .45f, .14f));
            glow.SetFloat("_EmissiveIntensity", 900f);
            glow.SetFloat("_UseEmissiveIntensity", 1f);
            Finish(glow); report.AppendLine("W_Glow emissive=" + glow.IsKeywordEnabled("_EMISSIVE_COLOR_MAP") + "/" + glow.GetColor("_EmissiveColor"));
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/Realm"); File.WriteAllText("Logs/Realm/world-materials.txt", report.ToString());
            if (created) foreach (var fbx in Directory.GetFiles(Folder, "*.fbx", SearchOption.AllDirectories)) AssetDatabase.ImportAsset(fbx.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
        }

        static Material Load(string name, ref bool created)
        {
            string path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("HDRP/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path); created = true;
            return material;
        }

        static void Finish(Material material)
        {
            material.enableInstancing = true;
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
        }
    }
}
