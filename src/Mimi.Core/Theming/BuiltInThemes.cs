namespace Mimi.Theming;

/// <summary>The four themes shipped with Mimi's first Theme Engine slice.</summary>
public static class BuiltInThemes
{
    public const string NeonStageId = "neon-stage";
    public const string SakuraDreamId = "sakura-dream";
    public const string MidnightLiveId = "midnight-live";
    public const string MonoFocusId = "mono-focus";
    public const string DefaultId = NeonStageId;

    private static readonly ThemeDefinition[] AllThemes =
    [
        CreateNeonStage(),
        CreateSakuraDream(),
        CreateMidnightLive(),
        CreateMonoFocus()
    ];

    private static readonly Dictionary<string, ThemeDefinition> ByCompactId =
        AllThemes.ToDictionary(
            theme => CompactId(theme.Id),
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ThemeDefinition> All => AllThemes;

    public static ThemeDefinition Default => AllThemes[0];

    public static string NormalizeId(string? value) =>
        TryGet(value, out var theme) ? theme.Id : DefaultId;

    public static ThemeDefinition GetOrDefault(string? value) =>
        TryGet(value, out var theme) ? theme : Default;

    public static bool TryGet(string? value, out ThemeDefinition theme)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            ByCompactId.TryGetValue(CompactId(value), out var found))
        {
            theme = found;
            return true;
        }

        theme = Default;
        return false;
    }

    private static string CompactId(string value) =>
        new(value.Trim().Where(character =>
            character is not ('-' or '_' or ' ')).ToArray());

    private static ThemeDefinition CreateNeonStage() => new(
        NeonStageId,
        "Neon Stage",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ThemeTokens.AppBackground] = "#0B101B",
            [ThemeTokens.PanelBackground] = "#121C2B",
            [ThemeTokens.RaisedBackground] = "#172638",
            [ThemeTokens.SunkenBackground] = "#101A29",
            [ThemeTokens.ControlBackground] = "#203044",
            [ThemeTokens.SelectedBackground] = "#203A48",
            [ThemeTokens.HeaderBackground] = "#1A2A3D",
            [ThemeTokens.Border] = "#2B405A",
            [ThemeTokens.Divider] = "#24344A",
            [ThemeTokens.Accent] = "#47E6D0",
            [ThemeTokens.AccentSecondary] = "#FF8FCB",
            [ThemeTokens.AccentHot] = "#FF5CAE",
            [ThemeTokens.OnAccent] = "#07151B",
            [ThemeTokens.TextPrimary] = "#F3F7FF",
            [ThemeTokens.TextStrong] = "#EAF2FC",
            [ThemeTokens.TextSecondary] = "#C7D6E7",
            [ThemeTokens.TextMuted] = "#9FB2C8",
            [ThemeTokens.TextFaint] = "#94A9C2",
            [ThemeTokens.TextDisabled] = "#8498B0",
            [ThemeTokens.Warning] = "#FFB020",
            [ThemeTokens.WarningText] = "#FFD9A0",
            [ThemeTokens.WarningBackground] = "#34251A",
            [ThemeTokens.WarningBorder] = "#7A5327",
            [ThemeTokens.DangerBackground] = "#5A2A3D",
            [ThemeTokens.HeroGradientStart] = "#183E4D",
            [ThemeTokens.HeroGradientMiddle] = "#252542",
            [ThemeTokens.HeroGradientEnd] = "#171A2A",
            [ThemeTokens.StageGradientStart] = "#0B202B",
            [ThemeTokens.StageGradientMiddle] = "#171A2A",
            [ThemeTokens.StageGradientEnd] = "#0B101B",
            [ThemeTokens.CoverGradientStart] = "#235D6A",
            [ThemeTokens.CoverGradientEnd] = "#302A55",
            [ThemeTokens.StageOverlay] = "#590B101B"
        });

    private static ThemeDefinition CreateSakuraDream() => new(
        SakuraDreamId,
        "Sakura Dream",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ThemeTokens.AppBackground] = "#160D18",
            [ThemeTokens.PanelBackground] = "#211222",
            [ThemeTokens.RaisedBackground] = "#2B1830",
            [ThemeTokens.SunkenBackground] = "#180C1E",
            [ThemeTokens.ControlBackground] = "#37203C",
            [ThemeTokens.SelectedBackground] = "#47233F",
            [ThemeTokens.HeaderBackground] = "#2E1A33",
            [ThemeTokens.Border] = "#533154",
            [ThemeTokens.Divider] = "#402544",
            [ThemeTokens.Accent] = "#FF8FCB",
            [ThemeTokens.AccentSecondary] = "#C77DFF",
            [ThemeTokens.AccentHot] = "#FF5CAE",
            [ThemeTokens.OnAccent] = "#2A071A",
            [ThemeTokens.TextPrimary] = "#FFF3FA",
            [ThemeTokens.TextStrong] = "#FCE7F4",
            [ThemeTokens.TextSecondary] = "#E7C7DC",
            [ThemeTokens.TextMuted] = "#C9A3BE",
            [ThemeTokens.TextFaint] = "#B58CA8",
            [ThemeTokens.TextDisabled] = "#A87F99",
            [ThemeTokens.Warning] = "#FFC24D",
            [ThemeTokens.WarningText] = "#FFE0A3",
            [ThemeTokens.WarningBackground] = "#382313",
            [ThemeTokens.WarningBorder] = "#7A5327",
            [ThemeTokens.DangerBackground] = "#61263A",
            [ThemeTokens.HeroGradientStart] = "#3B1733",
            [ThemeTokens.HeroGradientMiddle] = "#2A1738",
            [ThemeTokens.HeroGradientEnd] = "#180D22",
            [ThemeTokens.StageGradientStart] = "#2A1028",
            [ThemeTokens.StageGradientMiddle] = "#1D102C",
            [ThemeTokens.StageGradientEnd] = "#120716",
            [ThemeTokens.CoverGradientStart] = "#5A2047",
            [ThemeTokens.CoverGradientEnd] = "#35204E",
            [ThemeTokens.StageOverlay] = "#59160D18"
        });

    private static ThemeDefinition CreateMidnightLive() => new(
        MidnightLiveId,
        "Midnight Live",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ThemeTokens.AppBackground] = "#050812",
            [ThemeTokens.PanelBackground] = "#0A1020",
            [ThemeTokens.RaisedBackground] = "#101A30",
            [ThemeTokens.SunkenBackground] = "#070D1A",
            [ThemeTokens.ControlBackground] = "#182744",
            [ThemeTokens.SelectedBackground] = "#123A52",
            [ThemeTokens.HeaderBackground] = "#111E36",
            [ThemeTokens.Border] = "#263B63",
            [ThemeTokens.Divider] = "#1C2C4E",
            [ThemeTokens.Accent] = "#5AD1FF",
            [ThemeTokens.AccentSecondary] = "#8B7CFF",
            [ThemeTokens.AccentHot] = "#FF6BCE",
            [ThemeTokens.OnAccent] = "#03131F",
            [ThemeTokens.TextPrimary] = "#F2F7FF",
            [ThemeTokens.TextStrong] = "#E2ECFF",
            [ThemeTokens.TextSecondary] = "#BCD0F5",
            [ThemeTokens.TextMuted] = "#91A9D1",
            [ThemeTokens.TextFaint] = "#7F97BE",
            [ThemeTokens.TextDisabled] = "#718DB3",
            [ThemeTokens.Warning] = "#FFB020",
            [ThemeTokens.WarningText] = "#FFD9A0",
            [ThemeTokens.WarningBackground] = "#2E2113",
            [ThemeTokens.WarningBorder] = "#7A5327",
            [ThemeTokens.DangerBackground] = "#4A2132",
            [ThemeTokens.HeroGradientStart] = "#0B2A44",
            [ThemeTokens.HeroGradientMiddle] = "#18204A",
            [ThemeTokens.HeroGradientEnd] = "#090D20",
            [ThemeTokens.StageGradientStart] = "#06182C",
            [ThemeTokens.StageGradientMiddle] = "#101736",
            [ThemeTokens.StageGradientEnd] = "#04060F",
            [ThemeTokens.CoverGradientStart] = "#12405E",
            [ThemeTokens.CoverGradientEnd] = "#25245C",
            [ThemeTokens.StageOverlay] = "#59050812"
        });

    private static ThemeDefinition CreateMonoFocus() => new(
        MonoFocusId,
        "Mono Focus",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ThemeTokens.AppBackground] = "#0D0D0F",
            [ThemeTokens.PanelBackground] = "#16161A",
            [ThemeTokens.RaisedBackground] = "#1E1E23",
            [ThemeTokens.SunkenBackground] = "#111114",
            [ThemeTokens.ControlBackground] = "#2A2A30",
            [ThemeTokens.SelectedBackground] = "#34343B",
            [ThemeTokens.HeaderBackground] = "#232329",
            [ThemeTokens.Border] = "#3A3A42",
            [ThemeTokens.Divider] = "#2C2C33",
            [ThemeTokens.Accent] = "#F2F2F2",
            [ThemeTokens.AccentSecondary] = "#B8B8C0",
            [ThemeTokens.AccentHot] = "#FFFFFF",
            [ThemeTokens.OnAccent] = "#111114",
            [ThemeTokens.TextPrimary] = "#F5F5F7",
            [ThemeTokens.TextStrong] = "#E8E8EC",
            [ThemeTokens.TextSecondary] = "#C9C9D1",
            [ThemeTokens.TextMuted] = "#A7A7B0",
            [ThemeTokens.TextFaint] = "#92929B",
            [ThemeTokens.TextDisabled] = "#8B8B95",
            [ThemeTokens.Warning] = "#FFB020",
            [ThemeTokens.WarningText] = "#FFD9A0",
            [ThemeTokens.WarningBackground] = "#2E2417",
            [ThemeTokens.WarningBorder] = "#7A6434",
            [ThemeTokens.DangerBackground] = "#3B2A30",
            [ThemeTokens.HeroGradientStart] = "#232329",
            [ThemeTokens.HeroGradientMiddle] = "#1A1A20",
            [ThemeTokens.HeroGradientEnd] = "#111114",
            [ThemeTokens.StageGradientStart] = "#1A1A1F",
            [ThemeTokens.StageGradientMiddle] = "#121216",
            [ThemeTokens.StageGradientEnd] = "#0A0A0C",
            [ThemeTokens.CoverGradientStart] = "#33333B",
            [ThemeTokens.CoverGradientEnd] = "#222228",
            [ThemeTokens.StageOverlay] = "#590D0D0F"
        });
}
