using MudBlazor;

namespace Workbench.Web;

/// <summary>
/// MudBlazor theme mapped to the DeltaZulu Design System tokens.
/// Orange (#CF733A) is CTA-only. Slate (#275668) is structural emphasis.
/// Newsreader is marketing-only; product UI uses IBM Plex Sans exclusively.
/// </summary>
public static class WorkbenchTheme
{
    // Brand primitives
    private const string Ink = "#131F2A";

    private const string Paper = "#F6F5F3";
    private const string Navy = "#122F42";
    private const string Slate = "#275668";
    private const string Accent = "#CF733A";
    private const string AccentHover = "#BB6733";

    // Semantic
    private const string SurfacePrimary = "#FFFFFF";

    private const string SurfaceSecondary = "#EEF2F4";
    private const string TextSecondary = "#44515B";
    private const string TextMuted = "#5C6770";

    // Status
    private const string Success = "#1F7A53";

    private const string Warning = "#8A5A12";
    private const string Error = "#B73E3E";

    // Typography
    private const string FontSans = "'IBM Plex Sans', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";

    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Accent,
            PrimaryDarken = AccentHover,
            Secondary = Slate,
            Tertiary = Navy,
            Info = Slate,
            InfoLighten = "#E8EFF2",
            Success = Success,
            SuccessLighten = "#E6F5EE",
            Warning = Warning,
            WarningLighten = "#FDF3E4",
            Error = Error,
            ErrorLighten = "#FBE9E9",

            Background = Paper,
            Surface = SurfacePrimary,
            AppbarBackground = Ink,
            AppbarText = "#FFFFFF",
            DrawerBackground = SurfacePrimary,
            DrawerText = Ink,
            DrawerIcon = Slate,

            TextPrimary = Ink,
            TextSecondary = TextSecondary,
            TextDisabled = TextMuted,

            ActionDefault = Slate,
            ActionDisabled = TextMuted,
            ActionDisabledBackground = SurfaceSecondary,

            Divider = "rgba(19,31,42,0.08)",
            DividerLight = "rgba(19,31,42,0.05)",

            LinesDefault = "rgba(19,31,42,0.10)",
            LinesInputs = "rgba(19,31,42,0.18)",

            TableLines = "rgba(19,31,42,0.08)",
            TableStriped = "rgba(19,31,42,0.03)",
            TableHover = "rgba(39,86,104,0.06)",

            HoverOpacity = 0.06,
            RippleOpacity = 0.08,
        },

        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = [FontSans],
                FontSize = "1rem",
                LineHeight = "1.50",
                LetterSpacing = "normal",
                FontWeight = "400",
            },
            H1 = new H1Typography
            {
                FontFamily = [FontSans],
                FontSize = "2.5rem",
                LineHeight = "1.08",
                LetterSpacing = "-0.030em",
                FontWeight = "700",
            },
            H2 = new H2Typography
            {
                FontFamily = [FontSans],
                FontSize = "1.75rem",
                LineHeight = "1.12",
                LetterSpacing = "-0.020em",
                FontWeight = "700",
            },
            H3 = new H3Typography
            {
                FontFamily = [FontSans],
                FontSize = "1.25rem",
                LineHeight = "1.20",
                LetterSpacing = "-0.010em",
                FontWeight = "700",
            },
            H4 = new H4Typography
            {
                FontFamily = [FontSans],
                FontSize = "1.25rem",
                LineHeight = "1.20",
                LetterSpacing = "-0.010em",
                FontWeight = "600",
            },
            H5 = new H5Typography
            {
                FontFamily = [FontSans],
                FontSize = "1.125rem",
                LineHeight = "1.25",
                FontWeight = "600",
            },
            H6 = new H6Typography
            {
                FontFamily = [FontSans],
                FontSize = "1rem",
                LineHeight = "1.30",
                FontWeight = "600",
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontFamily = [FontSans],
                FontSize = "1rem",
                LineHeight = "1.50",
                FontWeight = "500",
            },
            Subtitle2 = new Subtitle2Typography
            {
                FontFamily = [FontSans],
                FontSize = "0.875rem",
                LineHeight = "1.45",
                FontWeight = "600",
            },
            Body1 = new Body1Typography
            {
                FontFamily = [FontSans],
                FontSize = "1rem",
                LineHeight = "1.50",
                FontWeight = "400",
            },
            Body2 = new Body2Typography
            {
                FontFamily = [FontSans],
                FontSize = "0.875rem",
                LineHeight = "1.45",
                FontWeight = "400",
            },
            Button = new ButtonTypography
            {
                FontFamily = [FontSans],
                FontSize = "0.875rem",
                FontWeight = "600",
                LetterSpacing = "0.010em",
            },
            Caption = new CaptionTypography
            {
                FontFamily = [FontSans],
                FontSize = "0.8125rem",
                LineHeight = "1.30",
                FontWeight = "400",
            },
            Overline = new OverlineTypography
            {
                FontFamily = [FontSans],
                FontSize = "0.8125rem",
                LineHeight = "1.30",
                LetterSpacing = "0.080em",
                FontWeight = "700",
            },
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
            DrawerWidthLeft = "260px",
        },

        Shadows = new Shadow
        {
            Elevation =
            [
                "none",
                "0 8px 20px rgba(19,31,42,0.04)",    // 1 — card default
                "0 8px 20px rgba(19,31,42,0.04)",    // 2
                "0 14px 36px rgba(19,31,42,0.08)",   // 3 — featured
                "0 14px 36px rgba(19,31,42,0.08)",   // 4
                "0 14px 36px rgba(19,31,42,0.08)",   // 5
                "0 18px 44px rgba(19,31,42,0.12)",   // 6+
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
                "0 18px 44px rgba(19,31,42,0.12)",
            ],
        },
    };
}
