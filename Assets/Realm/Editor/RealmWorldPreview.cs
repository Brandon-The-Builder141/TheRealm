using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheRealm.Editor
{
    // Renders the gameplay scene from fixed viewpoints through HDRP in edit mode, so world passes can be compared
    // before and after. Output: Logs/Realm/world-preview.png (4x2 grid, labelled in the order below).
    public static class RealmWorldPreview
    {
        const int Width = 640, Height = 360;
        static readonly (string name, Vector3 position, Vector3 target)[] Views =
        {
            ("Emberhearth overview", new Vector3(20, 13, -16), new Vector3(0, 1, 8)),
            ("Village street", new Vector3(3, 1.7f, -7), new Vector3(0, 1.6f, 14)),
            ("Hearth and warden", new Vector3(-3, 1.8f, 1), new Vector3(4, 1.2f, 8)),
            ("Gate onto the road", new Vector3(1, 1.8f, 14), new Vector3(0, 3, 34)),
            ("Forest road", new Vector3(2, 1.7f, 45), new Vector3(-4, 2, 80)),
            ("First seal", new Vector3(14, 7, 98), new Vector3(0, 1, 121)),
            ("Borderland escarpments", new Vector3(12, 4, 170), new Vector3(0, 3, 215)),
            ("Aerial", new Vector3(70, 95, 60), new Vector3(0, 0, 190)),
        };

        [MenuItem("The Realm/Render World Preview")]
        public static void Render()
        {
            RenderViews(Views,"Logs/Realm/world-preview.png");
        }
        [MenuItem("The Realm/Render Pass Four Preview")]
        public static void RenderPassFour()
        {
            RenderViews(new (string name, Vector3 position, Vector3 target)[]
            {
                ("Borderland approach",new Vector3(2,2,150),new Vector3(-10,3,175)),
                ("Ruined crossing",new Vector3(-10,3,154),new Vector3(-21,2,168)),
                ("Cliff face",new Vector3(8,4,185),new Vector3(27,5,202)),
                ("Road and ruins",new Vector3(0,2,190),new Vector3(17,3,211)),
                ("Broken walls",new Vector3(-12,3,222),new Vector3(-21,2,233)),
                ("Second seal",new Vector3(13,8,247),new Vector3(0,2,270)),
                ("Sunless ruins",new Vector3(3,3,337),new Vector3(21,3,353)),
                ("Borderland overview",new Vector3(52,40,154),new Vector3(0,0,218))
            },"Logs/Realm/pass4-preview.png");
        }
        internal static void RenderViews((string name, Vector3 position, Vector3 target)[] views,string output)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode before rendering the world preview.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != RealmBuilder.ScenePath) EditorSceneManager.OpenScene(RealmBuilder.ScenePath, OpenSceneMode.Single);
            var go = new GameObject("__RealmWorldPreview") { hideFlags = HideFlags.DontSave };
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var sheet = new Texture2D(Width * 4, Height * 2, TextureFormat.RGBA32, false);
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 55; cam.nearClipPlane = .1f; cam.farClipPlane = 600; cam.targetTexture = rt;
                var tile = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                for (int i = 0; i < views.Length; i++)
                {
                    cam.transform.position = views[i].position; cam.transform.LookAt(views[i].target);
                    // HDRP exposure and volumetrics converge over a few frames.
                    for (int f = 0; f < 6; f++) RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest { destination = rt });
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    tile.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); tile.Apply();
                    RenderTexture.active = prev;
                    sheet.SetPixels(i % 4 * Width, (1 - i / 4) * Height, Width, Height, tile.GetPixels());
                }
                Object.DestroyImmediate(tile);
                sheet.Apply();
                Directory.CreateDirectory("Logs/Realm");
                File.WriteAllBytes(output, sheet.EncodeToPNG());
                Debug.Log("[Realm World] Preview written to "+output);
            }
            finally
            {
                go.GetComponent<Camera>().targetTexture=null;
                Object.DestroyImmediate(sheet); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            }
        }
    }
}
