using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Frieren.Save.Storage
{
    /// <summary>
    /// Stores each slot as a JSON file under <c>Application.persistentDataPath/Saves</c>.
    /// </summary>
    /// <remarks>
    /// Writes go to a temporary file first and are then swapped in, so a crash or a pulled plug
    /// mid-write cannot leave a truncated save behind.
    /// </remarks>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private const string FileExtension = ".json";
        private const string TempExtension = ".tmp";
        private const string BackupExtension = ".bak";

        private readonly string rootDirectory;

        public FileSaveStorage(string rootDirectory = null)
        {
            this.rootDirectory = string.IsNullOrEmpty(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "Saves")
                : rootDirectory;
        }

        public string RootDirectory => rootDirectory;

        public bool Exists(string slotId) => File.Exists(PathForSlot(slotId));

        public void Write(string slotId, string payload)
        {
            string target = PathForSlot(slotId);
            string temp = target + TempExtension;

            Directory.CreateDirectory(rootDirectory);
            File.WriteAllText(temp, payload, Encoding.UTF8);

            if (File.Exists(target))
            {
                // Replace keeps a .bak so a corrupted swap is still recoverable by hand.
                File.Replace(temp, target, target + BackupExtension);
            }
            else
            {
                File.Move(temp, target);
            }
        }

        public bool TryRead(string slotId, out string payload)
        {
            payload = null;
            string target = PathForSlot(slotId);

            if (!File.Exists(target))
            {
                return false;
            }

            try
            {
                payload = File.ReadAllText(target, Encoding.UTF8);
                return true;
            }
            catch (IOException exception)
            {
                Debug.LogError($"Could not read save slot '{slotId}': {exception.Message}");
                return false;
            }
        }

        public bool Delete(string slotId)
        {
            string target = PathForSlot(slotId);

            if (!File.Exists(target))
            {
                return false;
            }

            File.Delete(target);
            return true;
        }

        public IReadOnlyList<string> ListSlots()
        {
            if (!Directory.Exists(rootDirectory))
            {
                return Array.Empty<string>();
            }

            string[] files = Directory.GetFiles(rootDirectory, "*" + FileExtension);
            var slots = new List<string>(files.Length);

            foreach (string file in files)
            {
                slots.Add(Path.GetFileNameWithoutExtension(file));
            }

            slots.Sort(StringComparer.Ordinal);
            return slots;
        }

        public string PathForSlot(string slotId) =>
            Path.Combine(rootDirectory, Sanitize(slotId) + FileExtension);

        /// <summary>Keeps slot ids to characters that are legal filenames on every target platform.</summary>
        private static string Sanitize(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                throw new ArgumentException("Save slot id must not be empty.", nameof(slotId));
            }

            var builder = new StringBuilder(slotId.Length);

            foreach (char character in slotId)
            {
                bool legal = char.IsLetterOrDigit(character) || character == '-' || character == '_';
                builder.Append(legal ? character : '_');
            }

            return builder.ToString();
        }
    }
}
