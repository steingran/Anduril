using Anduril.App.Tests.Infrastructure;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Anduril.App.Tests;

/// <summary>
/// Verifies AC-A1 / AC-A3 / AC-A4 from ANDA-71: the design-system merged
/// dictionaries load without XAML errors and every Anduril brush key
/// declared in §2.5 of the design plan resolves under both Light and
/// Dark theme variants.
///
/// <see cref="TestApp"/> mirrors the production <c>App.axaml</c> resource
/// wiring so this test exercises the same Fluent-aliased DynamicResource
/// forwarding that the live app does.
/// </summary>
public sealed class DesignSystemResourceTests : AvaloniaHeadlessTestBase
{
    private static readonly string[] BrushKeys =
    {
        // Surface
        "AndurilSurfaceBrush",
        "AndurilSurfaceAltBrush",
        "AndurilSurfaceElevatedBrush",
        "AndurilSurfaceSubtleBrush",
        "AndurilSidebarBrush",
        "AndurilCodeSurfaceBrush",
        // Stroke
        "AndurilStrokeBrush",
        "AndurilStrokeStrongBrush",
        "AndurilDividerBrush",
        // Text
        "AndurilTextPrimaryBrush",
        "AndurilTextSecondaryBrush",
        "AndurilTextTertiaryBrush",
        "AndurilTextDisabledBrush",
        "AndurilTextOnAccentBrush",
        // Accent
        "AndurilAccentBrush",
        "AndurilAccentHoverBrush",
        "AndurilAccentPressedBrush",
        "AndurilAccentSubtleBrush",
        "AndurilAccentBorderBrush",
        "AndurilOnAccentBrush",
        // Status
        "AndurilSuccessBrush",
        "AndurilSuccessSubtleBrush",
        "AndurilWarningBrush",
        "AndurilWarningSubtleBrush",
        "AndurilDangerBrush",
        "AndurilDangerSubtleBrush",
        "AndurilNeutralBrush",
        // Diff
        "AndurilDiffAddBrush",
        "AndurilDiffAddTextBrush",
        "AndurilDiffRemoveBrush",
        "AndurilDiffRemoveTextBrush",
        "AndurilDiffContextBrush",
    };

    [Test]
    public async Task AllBrushKeys_ResolveUnderLight()
        => await AssertAllBrushKeysResolveUnder(ThemeVariant.Light);

    [Test]
    public async Task AllBrushKeys_ResolveUnderDark()
        => await AssertAllBrushKeysResolveUnder(ThemeVariant.Dark);

    private static async Task AssertAllBrushKeysResolveUnder(ThemeVariant variant)
    {
        await RunOnUIThread(async () =>
        {
            var window = new Window { RequestedThemeVariant = variant };

            try
            {
                window.Show();

                foreach (var key in BrushKeys)
                {
                    var resolved = window.FindResource(variant, key);
                    await Assert.That(resolved)
                        .IsNotNull()
                        .Because($"brush '{key}' must resolve under {variant}");
                    await Assert.That(resolved is IBrush)
                        .IsTrue()
                        .Because($"brush '{key}' must be an IBrush under {variant} (got {resolved?.GetType().Name})");
                }
            }
            finally
            {
                window.Close();
            }
        });
    }
}
