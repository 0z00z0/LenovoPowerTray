using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace LenovoTray.Helpers;

/// <summary>
/// Shared color constants and pre-allocated brushes.
/// Centralises magic hex values and avoids allocating new brush objects on every UI refresh.
/// </summary>
internal static class AppColors
{
    // ── Semantic state ─────────────────────────────────────────────────────────
    internal static readonly Color Green       = Color.FromArgb(255, 0x10, 0xB9, 0x81);
    internal static readonly Color Orange      = Color.FromArgb(255, 0xFF, 0x8C, 0x00);
    internal static readonly Color Grey        = Color.FromArgb(255, 0x9E, 0x9E, 0x9E);
    internal static readonly Color Teal        = Color.FromArgb(255, 0x27, 0xE0, 0xC8);  // brand teal
    internal static readonly Color Amber       = Color.FromArgb(255, 0xD8, 0xA6, 0x57);  // brand amber
    internal static readonly Color Blue        = Color.FromArgb(255, 0x36, 0xB0, 0xE6);  // brand blue (idle)

    // ── Battery status glyph (gauge centre) ─────────────────────────────────────
    internal static readonly SolidColorBrush StatusChargingBrush    = new(Green);   // charging  ▲
    internal static readonly SolidColorBrush StatusIdleBrush        = new(Blue);    // full/idle ●
    internal static readonly SolidColorBrush StatusDischargingBrush = new(Amber);   // draining  ▼
    internal static readonly SolidColorBrush StatusUnknownBrush     = new(Grey);    // none / —

    // ── Badge backgrounds (semi-transparent fills) ──────────────────────────────
    internal static readonly SolidColorBrush BadgeActiveBrush   = new(Color.FromArgb(20, 0x10, 0xB9, 0x81));
    internal static readonly SolidColorBrush BadgeInactiveBrush = new(Color.FromArgb(12, 0x80, 0x80, 0x80));

    // ── Indicator dots ─────────────────────────────────────────────────────────
    internal static readonly SolidColorBrush IndicatorGreenBrush  = new(Green);
    internal static readonly SolidColorBrush IndicatorOrangeBrush = new(Orange);
    internal static readonly SolidColorBrush IndicatorGreyBrush   = new(Grey);

    // ── Arc gauge fills (by battery level) ─────────────────────────────────────
    // Brighter / more luminous than the muted semantic dot colours above so the battery
    // ring (and the history line, which reuses these brushes) reads clearly at a glance.
    internal static readonly Color GaugeRed   = Color.FromArgb(255, 0xFF, 0x3B, 0x30);  // bright red
    internal static readonly Color GaugeAmber = Color.FromArgb(255, 0xFF, 0xA5, 0x1F);  // bright amber
    internal static readonly Color GaugeGreen = Color.FromArgb(255, 0x16, 0xDD, 0x9A);  // bright green
    internal static readonly SolidColorBrush GaugeLowBrush    = new(GaugeRed);    // ≤ 20 %
    internal static readonly SolidColorBrush GaugeMedBrush    = new(GaugeAmber);  // ≤ 50 %
    internal static readonly SolidColorBrush GaugeHighBrush   = new(GaugeGreen);  // > 50 %
}
