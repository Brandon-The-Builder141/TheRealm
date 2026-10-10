using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace TheRealm
{
    // Complete journal records precede snapshot replacement. A crash cannot restore a finalized death.
    public sealed class RealmSaveStore
    {
        readonly string directory;
        public string DirectoryPath => directory;
        public RealmSaveStore(string directory) { this.directory = directory; }
        public WorldState Load()
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "world.json");
            string journal = Path.Combine(directory, "world.journal");
            WorldState latest = null;
            bool any = File.Exists(path) || File.Exists(journal);
            if (File.Exists(path)) latest = Parse(File.ReadAllText(path));
            if (File.Exists(journal))
                foreach (string line in File.ReadLines(journal))
                {
                    WorldState candidate = Parse(line);
                    if (candidate != null && (latest == null || candidate.revision > latest.revision)) latest = candidate;
                }
            if (latest == null && any) throw new IOException("The world save is unreadable. It has been preserved; a replacement world was not created.");
            if (latest != null && latest.version != 1) throw new IOException("Unsupported world save version: " + latest.version);
            return latest ?? new WorldState();
        }
        static WorldState Parse(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json) || !json.TrimEnd().EndsWith("}") || !json.Contains("\"worldId\"") || !json.Contains("\"version\"")) return null;
                var state = JsonUtility.FromJson<WorldState>(json);
                return state != null && !string.IsNullOrEmpty(state.worldId) && state.characters != null ? state : null;
            }
            catch (ArgumentException) { return null; }
        }
        public void Commit(WorldState world)
        {
            Directory.CreateDirectory(directory);
            world.revision++;
            string json = JsonUtility.ToJson(world);
            string path = Path.Combine(directory, "world.json");
            string temporary = path + ".tmp";
            string journal = Path.Combine(directory, "world.journal");
            using (var file = new FileStream(journal, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                byte[] bytes = Encoding.UTF8.GetBytes("\n" + json + "\n"); file.Write(bytes, 0, bytes.Length); file.Flush(true);
            }
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json); file.Write(bytes, 0, bytes.Length); file.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            // Snapshot is now durable. Compact rather than retaining an unbounded play history.
            if (new FileInfo(journal).Length > 4 * 1024 * 1024)
            {
                using (var file = new FileStream(journal, FileMode.Create, FileAccess.Write, FileShare.Read)) file.Flush(true);
            }
        }
    }
}
