using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace TheRealm.Editor
{
    // Renders the animated character prefab through HDRP in edit mode, stepping the Animator through each gameplay state.
    // Output: Logs/Realm/character-preview.png. Nothing is saved into the open scene.
    public static class RealmCharacterPreview
    {
        const int Tile = 420, TileHeight = 560;

        [MenuItem("The Realm/Render Character Preview")]
        public static void Render()
        {
            var prefab = Resources.Load<GameObject>(RealmCharacterActor.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Character prefab missing; run The Realm/Build Character Prefabs first.");
            var root = new GameObject("__RealmCharacterPreview") { hideFlags = HideFlags.DontSave };
            root.transform.position = new Vector3(0, 2000, 0);
            try
            {
                var sun = new GameObject("Preview sun").AddComponent<Light>(); sun.transform.SetParent(root.transform, false);
                sun.type = LightType.Directional; sun.lightUnit = LightUnit.Lux; sun.intensity = 60000; sun.color = new Color(1, .9f, .78f);
                sun.transform.rotation = Quaternion.Euler(35, -40, 0);
                var volume = new GameObject("Preview exposure").AddComponent<Volume>(); volume.transform.SetParent(root.transform, false);
                volume.isGlobal = true; volume.priority = 1000;
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var exposure = volume.profile.Add<Exposure>(); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(13.2f);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ground.transform.SetParent(root.transform, false);
                ground.transform.localScale = new Vector3(6, .02f, 6);
                var groundMaterial = new Material(Shader.Find("HDRP/Lit")) { hideFlags = HideFlags.DontSave };
                groundMaterial.SetColor("_BaseColor", new Color(.12f, .19f, .115f));
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

                var cam = new GameObject("Preview camera").AddComponent<Camera>(); cam.transform.SetParent(root.transform, false);
                cam.fieldOfView = 32; cam.nearClipPlane = .05f; cam.farClipPlane = 50;
                cam.transform.localPosition = new Vector3(2.2f, 1.55f, 4.0f); cam.transform.LookAt(root.transform.position + new Vector3(0, .95f, 0));
                var rt = new RenderTexture(Tile, TileHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                cam.targetTexture = rt;

                var shots = new (string name, Color tint, Action<Animator> setup, float seconds)[]
                {
                    ("Idle", RealmArt.DisciplineColors[0], a => { }, 1.1f),
                    ("Run", RealmArt.DisciplineColors[1], a => a.SetFloat("Speed", 6), .35f),
                    ("Strafe right", RealmArt.DisciplineColors[2], a => a.SetFloat("Strafe", 6), .3f),
                    ("Attack", RealmArt.DisciplineColors[3], a => a.SetTrigger("Attack"), .32f),
                    ("Block", RealmArt.DisciplineColors[4], a => a.SetBool("Block", true), .5f),
                    ("Cast", RealmArt.DisciplineColors[5], a => a.SetTrigger("Cast"), .42f),
                    ("Throw", RealmArt.DisciplineColors[0], a => a.SetTrigger("Throw"), .3f),
                    ("Downed", RealmArt.DisciplineColors[4], a => a.SetBool("Downed", true), 1.2f),
                };
                var sheet = new Texture2D(Tile * 4, TileHeight * 2, TextureFormat.RGBA32, false);
                var diagnostics = new System.Text.StringBuilder();
                for (int i = 0; i < shots.Length; i++)
                {
                    var actor = UnityEngine.Object.Instantiate(prefab, root.transform, false);
                    actor.transform.localRotation = Quaternion.Euler(0, 20, 0);    // model faces +Z, toward the camera
                    var animator = actor.GetComponent<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;     // nothing is "visible" to cull against in edit mode
                    actor.GetComponent<RealmCharacterActor>().Tint(shots[i].tint);
                    animator.Rebind(); animator.Update(0);
                    shots[i].setup(animator);
                    for (float t = 0; t < shots[i].seconds; t += 1 / 30f) animator.Update(1 / 30f);
                    if (i == 0)
                    {
                        var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
                        diagnostics.AppendLine("avatar=" + (animator.avatar != null ? animator.avatar.name + " valid=" + animator.avatar.isValid : "none")
                            + " controller=" + (animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "none")
                            + " clips=" + (animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips.Length : 0)
                            + " playing=" + string.Join(",", System.Array.ConvertAll(clipInfo, c => c.clip != null ? c.clip.name + ":" + c.weight.ToString("0.00") : "null"))
                            );
                    }
                    for (int f = 0; f < 3; f++) RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest { destination = rt });
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var tile = new Texture2D(Tile, TileHeight, TextureFormat.RGBA32, false); tile.ReadPixels(new Rect(0, 0, Tile, TileHeight), 0, 0); tile.Apply();
                    RenderTexture.active = prev;
                    sheet.SetPixels(i % 4 * Tile, (1 - i / 4) * TileHeight, Tile, TileHeight, tile.GetPixels());
                    UnityEngine.Object.DestroyImmediate(tile); UnityEngine.Object.DestroyImmediate(actor);
                }
                sheet.Apply();
                Directory.CreateDirectory("Logs/Realm");
                File.WriteAllBytes("Logs/Realm/character-preview.png", sheet.EncodeToPNG());
                File.WriteAllText("Logs/Realm/character-preview.txt", diagnostics.ToString());
                UnityEngine.Object.DestroyImmediate(sheet); cam.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(volume.profile); UnityEngine.Object.DestroyImmediate(groundMaterial);
                Debug.Log("[Realm Characters] Preview written to Logs/Realm/character-preview.png");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
