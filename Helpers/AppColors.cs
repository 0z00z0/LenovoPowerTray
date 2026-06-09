using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace LenovoTray.Helpers;

/// <summary>
/// Shared color constants and pre-allocated brushes.
/// Centralises magic hex values and avoids allocating new brush objects on every UI refresh.
/// </summary>
internal static class AppColors
{
    // ── Brand ──────────────────────────────────────────────────────────────────
    internal static readonly Color LenovoRed   = Color.FromArgb(255, 0xE2, 0x00, 0x1A);

    // ── Semantic state ─────────────────────────────────────────────────────────
    internal static readonly Color Green       = Color.FromArgb(255, 0x10, 0xB9, 0x81);
    internal static readonly Color Orange      = Color.FromArgb(255, 0xFF, 0x8C, 0x00);
    internal static readonly Color Grey        = Color.FromArgb(255, 0x9E, 0x9E, 0x9E);
    internal static readonly Color Teal        = Color.FromArgb(255, 0x27, 0xE0, 0xC8);  // brand teal
    internal static readonly Color Amber       = Color.FromArgb(255, 0xD8, 0xA6, 0x57);  // brand amber

    // ── Battery status glyph (gauge centre) ─────────────────────────────────────
    internal static readonly SolidColorBrush StatusChargingBrush    = new(Green);   // charging  ▲
    internal static readonly SolidColorBrush StatusIdleBrush        = new(Teal);    // full/idle ●
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
    internal static readonly SolidColorBrush GaugeLowBrush    = new(LenovoRed);  // ≤ 20 %
    internal static readonly SolidColorBrush GaugeMedBrush    = new(Orange);     // ≤ 50 %
    internal static readonly SolidColorBrush GaugeHighBrush   = new(Green);      // > 50 %
}
