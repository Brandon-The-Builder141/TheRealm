using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheRealm.Editor
{
    // Play-mode check of the animated player actor without networking: spawns the real prefab in the running scene and
    // feeds it CharacterState the way RealmPresentation does (moving, swinging, casting, blocking), then captures the Game view.
    public static class RealmCharacterSmoke
    {
        static RealmCharacterActor[] actors;
        static CharacterState[] states;
        static double started, lastCapture;
        static int captures;

        public static void Start()
        {
            if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode before running the character smoke test.");
            Stop();
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            var cam = Camera.main;
            var parent = new GameObject("__RealmCharacterSmoke").transform;
            Vector3 origin = new Vector3(4.5f, 0, 11);   // on the road beside Warden Elowen, in view of the title-screen camera
            Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            float faceCamera = Quaternion.LookRotation(-Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up)).eulerAngles.y;
            actors = new RealmCharacterActor[4]; states = new CharacterState[4];
            for (int i = 0; i < 4; i++)
            {
                states[i] = new CharacterState { id = "smoke" + i, name = "Smoke " + i, primary = (Discipline)(i + 1), secondary = Discipline.Steel, connected = true, yaw = faceCamera };
                actors[i] = RealmCharacterActor.Spawn(parent, states[i].name, RealmArt.DisciplineColors[i + 1]);
                if (actors[i] == null) throw new System.InvalidOperationException("Character prefab could not be loaded from Resources.");
                actors[i].transform.SetPositionAndRotation(origin + right * ((i - 1.5f) * 1.6f), Quaternion.Euler(0, faceCamera, 0));
            }
            started = EditorApplication.timeSinceStartup; lastCapture = 0; captures = 0;
            EditorApplication.update += Tick;
        }

        static void Stop()
        {
            EditorApplication.update -= Tick;
            var old = GameObject.Find("__RealmCharacterSmoke");
            if (old != null) Object.Destroy(old);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || actors == null || actors[0] == null) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - started;
            float dt = Time.deltaTime;
            // 0: runs back and forth across its lane.  1: swings every 0.8s.  2: casts (energy cost + ability cooldown).  3: blocks.
            var runner = actors[0].transform;
            runner.position += runner.forward * (Mathf.Sin((float)t * 1.2f) > 0 ? 5.5f : -5.5f) * dt * .35f;
            if (t % .8 < dt) states[1].attackReady = t + .65;
            if (t % 1.6 < dt) { states[2].abilityReady = t + 2; states[2].energy -= 22; }
            states[2].energy = Mathf.Min(100, states[2].energy + dt * 12);
            states[3].blocking = true;
            for (int i = 0; i < actors.Length; i++) actors[i].Sync(states[i], dt);
            if (t - lastCapture > .7 && captures < 3 && t > 1.2)
            {
                lastCapture = t; Directory.CreateDirectory("Logs/Realm");
                ScreenCapture.CaptureScreenshot("Logs/Realm/actor-smoke-" + captures + ".png"); captures++;
            }
            if (t > 4.5) { File.WriteAllText("Logs/Realm/actor-smoke.txt", "captures=" + captures); Stop(); }
        }
    }
}
