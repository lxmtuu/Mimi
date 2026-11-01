namespace Mimi.Theming;

/// <summary>
/// Stable color-token names shared by theme definitions and the WPF
/// resource layer. Token names are part of the future community-theme
/// schema, so they intentionally do not encode a particular palette.
/// </summary>
public static class ThemeTokens
{
    public const string AppBackground = "AppBackground";
    public const string PanelBackground = "PanelBackground";
    public const string RaisedBackground = "RaisedBackground";
    public const string SunkenBackground = "SunkenBackground";
    public const string ControlBackground = "ControlBackground";
    public const string SelectedBackground = "SelectedBackground";
    public const string HeaderBackground = "HeaderBackground";
    public const string Border = "Border";
    public const string Divider = "Divider";

    public const string Accent = "Accent";
    public const string AccentSecondary = "AccentSecondary";
    public const string AccentHot = "AccentHot";
    public const string OnAccent = "OnAccent";

    public const string TextPrimary = "TextPrimary";
    public const string TextStrong = "TextStrong";
    public const string TextSecondary = "TextSecondary";
    public const string TextMuted = "TextMuted";
    public const string TextFaint = "TextFaint";
    public const string TextDisabled = "TextDisabled";

    public const string Warning = "Warning";
    public const string WarningText = "WarningText";
    public const string WarningBackground = "WarningBackground";
    public const string WarningBorder = "WarningBorder";
    public const string DangerBackground = "DangerBackground";

    public const string HeroGradientStart = "HeroGradientStart";
    public const string HeroGradientMiddle = "HeroGradientMiddle";
    public const string HeroGradientEnd = "HeroGradientEnd";
    public const string StageGradientStart = "StageGradientStart";
    public const string StageGradientMiddle = "StageGradientMiddle";
    public const string StageGradientEnd = "StageGradientEnd";
    public const string CoverGradientStart = "CoverGradientStart";
    public const string CoverGradientEnd = "CoverGradientEnd";

    public const string StageOverlay = "StageOverlay";

    private static readonly string[] RequiredColorTokenValues =
    [
        AppBackground,
        PanelBackground,
        RaisedBackground,
        SunkenBackground,
        ControlBackground,
        SelectedBackground,
        HeaderBackground,
        Border,
        Divider,
        Accent,
        AccentSecondary,
        AccentHot,
        OnAccent,
        TextPrimary,
        TextStrong,
        TextSecondary,
        TextMuted,
        TextFaint,
        TextDisabled,
        Warning,
        WarningText,
        WarningBackground,
        WarningBorder,
        DangerBackground,
        HeroGradientStart,
        HeroGradientMiddle,
        HeroGradientEnd,
        StageGradientStart,
        StageGradientMiddle,
        StageGradientEnd,
        CoverGradientStart,
        CoverGradientEnd,
        StageOverlay
    ];

    public static IReadOnlyList<string> RequiredColorTokens =>
        RequiredColorTokenValues;
}
