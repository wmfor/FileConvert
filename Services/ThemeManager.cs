using System;
using Avalonia;
using Avalonia.Media;
using FileConvert.Models;

namespace FileConvert.Services;

public static class ThemeManager
{
    public static readonly ThemeDefinition[] Themes =
    {
        new("Indigo",
            WindowBg:          "#0c0d16", HeaderBg:          "#08091a",
            LeftPanelBg:       "#0b0c1a", RightPanelBg:      "#0d0e1e",
            CardBg:            "#14152c", CardBorder:        "#1e2244",
            Accent:            "#6366f1", AccentLight:       "#c7d2fe",
            ChipActiveBg:      "#1b1e64", ChipActiveBgHover: "#222676",
            PanelTabActiveBg:  "#1e2264", TabActiveBg:       "#232768",
            ConvertBtnBg:      "#4f46e5", ConvertBtnBorder:  "#6060ff",
            Separator:         "#131640", ListItemSelected:  "#111430",
            ChipBg:            "#101228", ChipHover:         "#181b38",
            InputBg:           "#09091e", InputBorder:       "#1c1f40"),

        new("Midnight",
            WindowBg:          "#070a10", HeaderBg:          "#050810",
            LeftPanelBg:       "#070912", RightPanelBg:      "#080a14",
            CardBg:            "#0e1022", CardBorder:        "#182040",
            Accent:            "#3b82f6", AccentLight:       "#93c5fd",
            ChipActiveBg:      "#0d2050", ChipActiveBgHover: "#1a3060",
            PanelTabActiveBg:  "#0f2248", TabActiveBg:       "#102040",
            ConvertBtnBg:      "#1d6fde", ConvertBtnBorder:  "#4090ff",
            Separator:         "#0e1228", ListItemSelected:  "#0d1838",
            ChipBg:            "#0c0e1e", ChipHover:         "#14162a",
            InputBg:           "#060810", InputBorder:       "#152038"),

        new("Ember",
            WindowBg:          "#0f0c08", HeaderBg:          "#0a0806",
            LeftPanelBg:       "#0d0b08", RightPanelBg:      "#100d09",
            CardBg:            "#1a1510", CardBorder:        "#2c2014",
            Accent:            "#f59e0b", AccentLight:       "#fde68a",
            ChipActiveBg:      "#4a2e04", ChipActiveBgHover: "#5c3806",
            PanelTabActiveBg:  "#402808", TabActiveBg:       "#3c2606",
            ConvertBtnBg:      "#d97706", ConvertBtnBorder:  "#fbbf24",
            Separator:         "#1e180e", ListItemSelected:  "#28200a",
            ChipBg:            "#110e08", ChipHover:         "#1c1810",
            InputBg:           "#0a0806", InputBorder:       "#281c08"),

        new("Forest",
            WindowBg:          "#08100a", HeaderBg:          "#060e08",
            LeftPanelBg:       "#081009", RightPanelBg:      "#091208",
            CardBg:            "#0f1a10", CardBorder:        "#1a2e1c",
            Accent:            "#10b981", AccentLight:       "#6ee7b7",
            ChipActiveBg:      "#063b26", ChipActiveBgHover: "#0a4a30",
            PanelTabActiveBg:  "#083220", TabActiveBg:       "#0a3022",
            ConvertBtnBg:      "#059669", ConvertBtnBorder:  "#34d399",
            Separator:         "#0e1e12", ListItemSelected:  "#0c1e10",
            ChipBg:            "#0c1210", ChipHover:         "#141e16",
            InputBg:           "#070e08", InputBorder:       "#162618"),

        new("Rose",
            WindowBg:          "#100b0d", HeaderBg:          "#0a0709",
            LeftPanelBg:       "#0e0a0c", RightPanelBg:      "#100b0e",
            CardBg:            "#1c1015", CardBorder:        "#301820",
            Accent:            "#f43f5e", AccentLight:       "#fda4af",
            ChipActiveBg:      "#4a0a18", ChipActiveBgHover: "#5c0e20",
            PanelTabActiveBg:  "#400818", TabActiveBg:       "#3c0816",
            ConvertBtnBg:      "#e11d48", ConvertBtnBorder:  "#fb7185",
            Separator:         "#200e14", ListItemSelected:  "#200c14",
            ChipBg:            "#110c10", ChipHover:         "#1c1218",
            InputBg:           "#0a0709", InputBorder:       "#261018"),
    };

    public static void Apply(int index)
    {
        var t   = Themes[Math.Clamp(index, 0, Themes.Length - 1)];
        var res = Application.Current!.Resources;

        res["ThemeWindowBg"]          = Brush(t.WindowBg);
        res["ThemeHeaderBg"]          = Brush(t.HeaderBg);
        res["ThemeLeftPanelBg"]       = Brush(t.LeftPanelBg);
        res["ThemeRightPanelBg"]      = Brush(t.RightPanelBg);
        res["ThemeCardBg"]            = Brush(t.CardBg);
        res["ThemeCardBorder"]        = Brush(t.CardBorder);
        res["ThemeAccent"]            = Brush(t.Accent);
        res["ThemeAccentLight"]       = Brush(t.AccentLight);
        res["ThemeChipActiveBg"]      = Brush(t.ChipActiveBg);
        res["ThemeChipActiveBgHover"] = Brush(t.ChipActiveBgHover);
        res["ThemePanelTabActiveBg"]  = Brush(t.PanelTabActiveBg);
        res["ThemeTabActiveBg"]       = Brush(t.TabActiveBg);
        res["ThemeConvertBtnBg"]      = Brush(t.ConvertBtnBg);
        res["ThemeConvertBtnBorder"]  = Brush(t.ConvertBtnBorder);
        res["ThemeSeparator"]         = Brush(t.Separator);
        res["ThemeListItemSelected"]  = Brush(t.ListItemSelected);
        res["ThemeChipBg"]            = Brush(t.ChipBg);
        res["ThemeChipHover"]         = Brush(t.ChipHover);
        res["ThemeInputBg"]           = Brush(t.InputBg);
        res["ThemeInputBorder"]       = Brush(t.InputBorder);
    }

    private static SolidColorBrush Brush(string hex) => new(Color.Parse(hex));
}
