namespace FileConvert.Models;

public record ThemeDefinition(
    string Name,
    string WindowBg,
    string HeaderBg,
    string LeftPanelBg,
    string RightPanelBg,
    string CardBg,
    string CardBorder,
    string Accent,
    string AccentLight,
    string ChipActiveBg,
    string ChipActiveBgHover,
    string PanelTabActiveBg,
    string TabActiveBg,
    string ConvertBtnBg,
    string ConvertBtnBorder,
    string Separator,
    string ListItemSelected,
    // Inactive chip / button backgrounds
    string ChipBg,
    string ChipHover,
    // Input / textbox backgrounds and borders
    string InputBg,
    string InputBorder
);
