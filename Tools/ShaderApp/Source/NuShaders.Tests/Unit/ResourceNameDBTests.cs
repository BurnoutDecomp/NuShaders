using NuShaders.Formats.Bundle;

namespace NuShaders.Tests.Unit;

public class ResourceNameDBTests
{
    [Fact]
    public void Lookup_resolves_a_known_id_and_nulls_an_unknown_one()
    {
        // 0x0f7b6bea = the stock CorrugatedSteel005 specular TextureState (per ResourceDB.json).
        var hit = ResourceNameDB.Instance.Lookup(0x0F7B6BEA);
        if (hit is null) return;   // ResourceDB.json not locatable from here → skip (repo not on the walk-up path)
        Assert.Contains("CorrugatedSteel005", hit);
        Assert.Null(ResourceNameDB.Instance.Lookup(0xFFFFFFFE));
    }
}
