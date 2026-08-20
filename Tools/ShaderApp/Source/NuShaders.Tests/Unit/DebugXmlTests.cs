using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;

namespace NuShaders.Tests.Unit;

public class DebugXMLTests
{
    [Fact]
    public void UpsertName_creates_file_updates_and_coexists()
    {
        var dir = Directory.CreateTempSubdirectory("nudbg");
        try
        {
            string folder = dir.FullName, path = Path.Combine(folder, ".debug.xml");

            DebugXML.UpsertName(folder, new ResourceID(0x4036706f), DebugXML.TypeString(0x1), "MyMaterial");
            var r1 = DebugXML.TryReadFile(path);
            Assert.Equal("MyMaterial", r1!.NameFor(0x4036706f));
            Assert.Equal("RwMaterial", r1.TypeFor(0x4036706f));

            DebugXML.UpsertName(folder, new ResourceID(0x4036706f), DebugXML.TypeString(0x1), "Renamed");   // update
            DebugXML.UpsertName(folder, new ResourceID(0x11b669ed), DebugXML.TypeString(0x0), "MyTexture"); // add a second
            var r2 = DebugXML.TryReadFile(path);
            Assert.Equal("Renamed", r2!.NameFor(0x4036706f));
            Assert.Equal("MyTexture", r2.NameFor(0x11b669ed));
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void UpsertName_matches_game_format()
    {
        var dir = Directory.CreateTempSubdirectory("nudbgfmt");
        try
        {
            string path = Path.Combine(dir.FullName, ".debug.xml");
            DebugXML.UpsertName(dir.FullName, new ResourceID(0x12345678), "RwMaterial", "Foo");

            byte[] bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no BOM");

            string xml = File.ReadAllText(path);
            Assert.DoesNotContain("<?xml", xml);          // no declaration
            Assert.DoesNotContain(" />", xml);            // no space before the self-close
            Assert.Contains("name=\"Foo\"/>", xml);
            Assert.StartsWith("<ResourceStringTable>", xml);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void MaterialWriter_persists_the_name_to_debug_xml_on_merge()
    {
        var dir = Directory.CreateTempSubdirectory("numatdbg");
        try
        {
            var id = new ResourceID(0x12345678);
            MaterialWriter.MergeIntoBundle(dir.FullName, id, new byte[64], new ImportsYAML(), "gamedb://burnout5/Mods/Foo.Material?ID=1");
            var read = DebugXML.TryReadFile(Path.Combine(dir.FullName, ".debug.xml"));
            Assert.NotNull(read);
            Assert.Equal("gamedb://burnout5/Mods/Foo.Material?ID=1", read!.NameFor(0x12345678));
            Assert.Equal("RwMaterial", read.TypeFor(0x12345678));
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void UpsertName_preserves_existing_entries()
    {
        var dir = Directory.CreateTempSubdirectory("nudbg2");
        try
        {
            string path = Path.Combine(dir.FullName, ".debug.xml");
            File.WriteAllText(path, "<ResourceStringTable><Resource id=\"00000001\" type=\"Texture\" name=\"existing\" /></ResourceStringTable>");
            DebugXML.UpsertName(dir.FullName, new ResourceID(0x2), DebugXML.TypeString(0x1), "added");
            var read = DebugXML.TryReadFile(path);
            Assert.Equal("existing", read!.NameFor(0x1));
            Assert.Equal("added", read.NameFor(0x2));
        }
        finally { dir.Delete(true); }
    }
}
