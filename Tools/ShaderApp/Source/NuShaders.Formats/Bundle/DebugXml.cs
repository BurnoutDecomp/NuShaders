using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NuShaders.Formats.Model;

namespace NuShaders.Formats.Bundle;

/// <summary>One <c>&lt;Resource id type name/&gt;</c> entry from a bundle's <c>.debug.xml</c> ResourceStringTable.</summary>
public sealed record DebugResource(uint Id, string Type, string Name);

/// <summary>
/// Reads a bundle's <c>.debug.xml</c> (id → human gamedb name + type string). Optional — bundles extracted without
/// debug info (e.g. the working TRK bundle) have none, in which case names degrade to the hex id.
/// </summary>
public sealed class DebugXML
{
    private readonly Dictionary<uint, DebugResource> _byID = [];

    public IReadOnlyDictionary<uint, DebugResource> Resources => _byID;
    public string? NameFor(uint id) => _byID.TryGetValue(id, out var r) ? r.Name : null;
    public string? TypeFor(uint id) => _byID.TryGetValue(id, out var r) ? r.Type : null;

    public static DebugXML Read(string xml)
    {
        var d = new DebugXML();
        foreach (var e in XDocument.Parse(xml).Descendants("Resource"))
        {
            string? idAttr = e.Attribute("id")?.Value;
            if (idAttr is null) continue;
            if (idAttr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) idAttr = idAttr[2..];
            if (!uint.TryParse(idAttr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint id)) continue;
            d._byID[id] = new DebugResource(id, e.Attribute("type")?.Value ?? "", e.Attribute("name")?.Value ?? "");
        }
        return d;
    }

    public static DebugXML? TryReadFile(string path)
    {
        if (!File.Exists(path)) return null;
        try { return Read(File.ReadAllText(path)); }
        catch { return null; }   // optional debug info — a malformed/half-written file degrades to hex names, never blocks bundle-open
    }

    /// <summary>
    /// Assign (or change) a resource's friendly name in the bundle's <c>.debug.xml</c>, creating the file/table if
    /// absent. The name is a cosmetic label keyed by id (the id is fixed by the resource's own hash), so any label is
    /// valid. The existing XML is preserved and only the one <c>&lt;Resource&gt;</c> entry is added/updated.
    /// </summary>
    public static void UpsertName(string bundleFolder, ResourceID id, string typeString, string name)
    {
        string path = Path.Combine(bundleFolder, ".debug.xml");
        XDocument doc;
        XElement root;
        if (File.Exists(path))
        {
            doc = XDocument.Load(path);
            root = doc.Root ?? new XElement("ResourceStringTable");
            if (doc.Root is null) doc.Add(root);
        }
        else
        {
            root = new XElement("ResourceStringTable");
            doc = new XDocument(root);
        }

        var existing = root.Descendants("Resource").FirstOrDefault(e => NormalizeID(e.Attribute("id")?.Value) == id.Value);
        if (existing is not null)
        {
            existing.SetAttributeValue("name", name);
            if (existing.Attribute("type") is null) existing.SetAttributeValue("type", typeString);
        }
        else
        {
            root.Add(new XElement("Resource",
                new XAttribute("id", id.Value.ToString("x8", CultureInfo.InvariantCulture)),
                new XAttribute("type", typeString),
                new XAttribute("name", name)));
        }

        // Match the game's .debug.xml exactly: no BOM, no <?xml …?> declaration, tab indent, and no space before the
        // self-closing "/>" (XmlWriter emits "<Resource … />" — tighten it).
        doc.Declaration = null;
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, IndentChars = "\t" };
        using (var w = XmlWriter.Create(sb, settings)) doc.Save(w);
        File.WriteAllText(path, sb.ToString().Replace(" />", "/>"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>The .debug.xml type string for a meta type (RwMaterial, Texture, …).</summary>
    public static string TypeString(int metaType) => metaType switch
    {
        0x1 => "RwMaterial",
        0xf => "MaterialState",
        0xe => "TextureState",
        0x0 => "Texture",
        0x32 => "Shader",
        0x12 => "ShaderProgramBuffer",
        _ => "Resource",
    };

    private static uint NormalizeID(string? s)
    {
        if (s is null) return 0;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v) ? v : 0;
    }
}
