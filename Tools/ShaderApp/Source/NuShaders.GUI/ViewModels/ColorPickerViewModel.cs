using System.Windows.Media;
using NuShaders.GUI.Common;
using NuShaders.Imaging;

namespace NuShaders.GUI.ViewModels;

/// <summary>
/// RGBA colour editor behind <c>ColorPickerDialog</c>. Values are raw [0,1] (no gamma — BPR material colours are
/// linear tint multipliers, verified in the shaders). Sliders span 0..max(1, seed) so an over-one seed (e.g. a scalar
/// param opened as a colour) isn't clamped on open; the numeric boxes are the precise control and preserve HDR.
/// </summary>
public sealed class ColorPickerViewModel : ViewModelBase
{
    private double _r, _g, _b, _a;
    private readonly float _origR, _origG, _origB, _origA;

    public ColorPickerViewModel(float r, float g, float b, float a)
    {
        _r = r; _g = g; _b = b; _a = a;
        _origR = r; _origG = g; _origB = b; _origA = a;
        SliderMax = Math.Max(1.0, Math.Max(Math.Max(r, g), Math.Max(b, a)));
        OkCommand = new RelayCommand(() => CloseRequested?.Invoke(true));
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }

    /// <summary>Upper bound for the sliders — never below 1, and at least the largest seeded component (no load-time clamp).</summary>
    public double SliderMax { get; }

    /// <summary>True only if the picked RGBA differs from the seed — lets the caller leave ValueText untouched on a no-op open+OK.</summary>
    public bool HasChanged => (float)_r != _origR || (float)_g != _origG || (float)_b != _origB || (float)_a != _origA;

    public double R { get => _r; set { if (SetProperty(ref _r, value)) ColorChanged(); } }
    public double G { get => _g; set { if (SetProperty(ref _g, value)) ColorChanged(); } }
    public double B { get => _b; set { if (SetProperty(ref _b, value)) ColorChanged(); } }
    public double A { get => _a; set { if (SetProperty(ref _a, value)) ColorChanged(); } }

    /// <summary>RGB as "RRGGBB" (LDR); editing it sets R/G/B. Bound LostFocus so it doesn't reformat mid-type.</summary>
    public string Hex
    {
        get => ColorConvert.ToHex((float)_r, (float)_g, (float)_b);
        set { if (ColorConvert.TryParseHex(value, out float r, out float g, out float b)) { R = r; G = g; B = b; } }
    }

    /// <summary>Alpha-aware preview (shown over a checkerboard in the dialog).</summary>
    public Brush PreviewBrush => new SolidColorBrush(Color.FromArgb(
        ColorConvert.ToByte((float)_a), ColorConvert.ToByte((float)_r), ColorConvert.ToByte((float)_g), ColorConvert.ToByte((float)_b)));

    public float[] Result => [(float)_r, (float)_g, (float)_b, (float)_a];

    public RelayCommand OkCommand { get; }
    public RelayCommand CancelCommand { get; }
    public event Action<bool>? CloseRequested;

    private void ColorChanged()
    {
        OnPropertyChanged(nameof(Hex));
        OnPropertyChanged(nameof(PreviewBrush));
    }
}
