using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SimpleHackintosh.App;

internal sealed record PlistEditableEntry(string Path, string Type, string Value, XElement Element)
{
    public override string ToString() => $"{Path}  [{Type}]";
}

internal sealed class ConfigPlistEditorSession
{
    private const int MaxEditableEntries = 10000;
    private readonly XDocument _document;
    private readonly List<PlistEditableEntry> _entries;

    private ConfigPlistEditorSession(XDocument document)
    {
        _document = document;
        var root = document.Root;
        if (root?.Name.LocalName != "plist")
            throw new InvalidDataException("The XML root element must be <plist>.");
        var value = root.Elements().FirstOrDefault();
        if (value?.Name.LocalName != "dict")
            throw new InvalidDataException("The top-level plist value must be a dictionary (<dict>).");

        _entries = new List<PlistEditableEntry>();
        Walk(value, "", _entries, 0);
    }

    public IReadOnlyList<PlistEditableEntry> Entries => _entries;

    public static ConfigPlistEditorSession Load(string xmlText)
    {
        var document = SafePlistXml.Load(xmlText);
        return new ConfigPlistEditorSession(document);
    }

    public void SetValue(PlistEditableEntry entry, string value)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        if (entry.Element.Document != _document)
            throw new InvalidOperationException("The selected property does not belong to the active config.plist document.");

        switch (entry.Type)
        {
            case "string":
                XmlConvert.VerifyXmlChars(value);
                entry.Element.Value = value;
                break;
            case "integer":
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
                    throw new InvalidDataException("Enter a valid signed 64-bit integer.");
                entry.Element.Value = integer.ToString(CultureInfo.InvariantCulture);
                break;
            case "real":
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double real) ||
                    !double.IsFinite(real))
                    throw new InvalidDataException("Enter a finite real number using invariant decimal notation.");
                entry.Element.Value = real.ToString("R", CultureInfo.InvariantCulture);
                break;
            case "boolean":
                if (!bool.TryParse(value, out bool boolean))
                    throw new InvalidDataException("A Boolean value must be true or false.");
                entry.Element.ReplaceWith(new XElement(entry.Element.Name.Namespace + (boolean ? "true" : "false")));
                break;
            default:
                throw new InvalidOperationException("This plist value type is read-only in the scalar editor.");
        }

        _entries.Clear();
        var root = _document.Root ?? throw new InvalidDataException("The plist root is missing.");
        var dictionary = root.Elements().FirstOrDefault() ?? throw new InvalidDataException("The top-level plist value is missing.");
        Walk(dictionary, "", _entries, 0);
    }

    public void SaveAsNewFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Choose an output file.", nameof(path));
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidDataException("The output directory is invalid.");
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The output directory does not exist.");
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
            throw new IOException("The output file already exists. Choose a new name; the editor never overwrites an existing file.");

        var outputDocument = new XDocument(_document);
        outputDocument.Declaration ??= new XDeclaration("1.0", "UTF-8", null);
        if (outputDocument.DocumentType is null)
            outputDocument.AddFirst(new XDocumentType(
                "plist",
                "-//Apple//DTD PLIST 1.0//EN",
                "http://www.apple.com/DTDs/PropertyList-1.0.dtd",
                null));

        string temporaryPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".partial");
        try
        {
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Entitize,
                OmitXmlDeclaration = false,
                CheckCharacters = true
            };
            using (var writer = XmlWriter.Create(temporaryPath, settings))
                outputDocument.Save(writer);
            File.Move(temporaryPath, fullPath);
        }
        catch
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
            throw;
        }
    }

    private static void Walk(XElement node, string path, List<PlistEditableEntry> entries, int depth)
    {
        if (depth > 32) throw new InvalidDataException("The plist nesting depth exceeds the 32-level safety limit.");
        switch (node.Name.LocalName)
        {
            case "dict":
            {
                var children = node.Elements().ToArray();
                if (children.Length % 2 != 0)
                    throw new InvalidDataException("A plist dictionary contains a key without a matching value.");
                var seenKeys = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < children.Length; index += 2)
                {
                    if (children[index].Name.LocalName != "key")
                        throw new InvalidDataException("A plist dictionary contains a value where a key was expected.");
                    string key = children[index].Value;
                    if (!seenKeys.Add(key)) throw new InvalidDataException($"Duplicate plist key: {key}");
                    Walk(children[index + 1], AppendPath(path, key), entries, depth + 1);
                }
                break;
            }
            case "array":
            {
                int index = 0;
                foreach (var item in node.Elements())
                {
                    string itemPath = path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                    index++;
                    Walk(item, itemPath, entries, depth + 1);
                }
                break;
            }
            case "string":
            case "integer":
            case "real":
                AddEntry(node, path, node.Name.LocalName, node.Value, entries);
                break;
            case "true":
                AddEntry(node, path, "boolean", "true", entries);
                break;
            case "false":
                AddEntry(node, path, "boolean", "false", entries);
                break;
            case "data":
            case "date":
                // Binary payloads and timestamps are intentionally read-only in this editor.
                break;
            default:
                throw new InvalidDataException($"Unsupported plist value type: {node.Name.LocalName}");
        }
    }

    private static void AddEntry(XElement node, string path, string type, string value, List<PlistEditableEntry> entries)
    {
        if (entries.Count >= MaxEditableEntries)
            throw new InvalidDataException("The plist contains more than 10,000 editable scalar values.");
        entries.Add(new PlistEditableEntry(path, type, value, node));
    }

    private static string AppendPath(string parent, string key)
    {
        string safeKey = key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        return parent.Length == 0 ? safeKey : parent + "/" + safeKey;
    }
}
