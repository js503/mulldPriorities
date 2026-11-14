using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Mulld.Priorities
{
    // Loaded on demand. Preset names are XML values, never filesystem paths.
    public sealed class NamedPresetStore
    {
        private readonly string path;
        private readonly Func<XElement, string> validate;
        private Dictionary<string, (string Name, XElement Definition)> entries = new(StringComparer.OrdinalIgnoreCase);
        public string Error { get; private set; }
        public IEnumerable<string> Names => entries.Values.Select(e => e.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        public NamedPresetStore(string path, Func<XElement, string> validate) { this.path = path; this.validate = validate; Reload(); }
        public static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Enter a preset name.";
            if (name.Trim().Length > 40) return "Use 40 characters or fewer.";
            if (name.Any(char.IsControl)) return "Preset names cannot contain control characters.";
            return null;
        }
        public void Reload()
        {
            try
            {
                var next = new Dictionary<string, (string, XElement)>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(path))
                {
                    using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000 });
                    var root = XElement.Load(reader);
                    if (root.Name != "mulldPresetLibrary" || (int?)root.Attribute("version") != 1) throw new InvalidDataException("Unsupported preset library format.");
                    foreach (var preset in root.Elements("preset"))
                    {
                        string name = ((string)preset.Attribute("name"))?.Trim();
                        string error = ValidateName(name);
                        var definition = preset.Element("mulldPriorities");
                        error ??= definition == null ? "Preset is missing its rules." : validate(definition);
                        if (error != null || next.ContainsKey(name)) throw new InvalidDataException(error ?? "Duplicate preset names.");
                        next.Add(name, (name, new XElement(definition)));
                    }
                }
                entries = next; Error = null;
            }
            catch (Exception e) { Error = "Could not read preset library: " + e.Message; }
        }
        public bool Contains(string name) => name != null && entries.ContainsKey(name.Trim());
        public bool TryGet(string name, out XElement definition)
        {
            if (name != null && entries.TryGetValue(name.Trim(), out var entry)) { definition = new XElement(entry.Definition); return true; }
            definition = null; return false;
        }
        public string Save(string name, XElement definition, bool overwrite)
        {
            if (Error != null) return Error;
            string error = ValidateName(name);
            if (error != null) return error;
            name = name.Trim();
            error = definition == null ? "Preset is missing its rules." : validate(definition);
            if (error != null) return error;
            if (entries.ContainsKey(name) && !overwrite) return "That preset already exists. Confirm overwrite to replace it.";
            var next = new Dictionary<string, (string Name, XElement Definition)>(entries, StringComparer.OrdinalIgnoreCase);
            if (next.TryGetValue(name, out var previous)) name = previous.Name;
            next[name] = (name, new XElement(definition));
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                var root = new XElement("mulldPresetLibrary", new XAttribute("version", 1), next.Values.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Select(e => new XElement("preset", new XAttribute("name", e.Name), new XElement(e.Definition))));
                string serialized = root.ToString();
                if (serialized.Length > 1_900_000) return "Preset library is too large to save safely.";
                File.WriteAllText(temporary, serialized);
                File.Move(temporary, path, true);
                entries = next; return null;
            }
            catch (Exception e) { return "Could not save preset: " + e.Message; }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
