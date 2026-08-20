using System.Globalization;
using System.Windows.Media;
using NuShaders.GUI.Common;
using NuShaders.Imaging;

namespace NuShaders.GUI.ViewModels;

/// <summary>One editable material parameter row: a comma-separated float value (handles any Size) joined to the shader's name + default.</summary>
public sealed class MaterialParamViewModel : ViewModelBase
{
    public string Name { get; }
    public uint NameHash { get; }
    public bool IsPixel { get; }
    public int ConstIndex { get; }
    public int Size { get; }
    public bool ResolvedInShader { get; }
    public string Stage => IsPixel ? "PS" : "VS";
    public string DefaultText { get; }

    private string _valueText;
    public string ValueText
    {
        get => _valueText;
        set { if (SetProperty(ref _valueText, value)) OnPropertyChanged(nameof(SwatchBrush)); }
    }

    /// <summary>A single-register (float4) param is editable as a colour — the row shows a swatch + colour picker.</summary>
    public bool IsColor => Size == 1;

    /// <summary>Opaque preview of the RGB (first three) components, clamped to [0,1]; refreshes when ValueText changes.</summary>
    public Brush SwatchBrush
    {
        get
        {
            var v = ParseValue();
            return new SolidColorBrush(Color.FromRgb(
                ColorConvert.ToByte(Comp(v, 0)), ColorConvert.ToByte(Comp(v, 1)), ColorConvert.ToByte(Comp(v, 2))));
        }
    }

    public MaterialParamViewModel(string name, uint nameHash, bool isPixel, int constIndex, int size, float[] current, float[] shaderDefault, bool resolved)
    {
        Name = name; NameHash = nameHash; IsPixel = isPixel; ConstIndex = constIndex; Size = size; ResolvedInShader = resolved;
        DefaultText = Format(shaderDefault);
        _valueText = Format(current);
    }

    public float[] ParseValue()
    {
        var parts = ValueText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var arr = new float[Size * 4];
        for (int i = 0; i < arr.Length && i < parts.Length; i++)
            float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out arr[i]);
        return arr;
    }

    /// <summary>Current RGBA for seeding the colour picker (alpha defaults to 1 when the param has no 4th component).</summary>
    public float[] GetRGBA()
    {
        var v = ParseValue();
        return [Comp(v, 0), Comp(v, 1), Comp(v, 2), v.Length > 3 ? v[3] : 1f];
    }

    /// <summary>Write picked RGBA back into ValueText (invariant, matching the param's formatting).</summary>
    public void SetRGBA(IReadOnlyList<float> rgba)
        => ValueText = string.Join(", ", rgba.Take(4).Select(x => x.ToString("0.####", CultureInfo.InvariantCulture)));

    public void ResetToDefault() => ValueText = DefaultText;

    private static float Comp(float[] v, int i) => i < v.Length ? v[i] : 0f;
    private static string Format(float[] v) => string.Join(", ", v.Select(x => x.ToString("0.####", CultureInfo.InvariantCulture)));
}
