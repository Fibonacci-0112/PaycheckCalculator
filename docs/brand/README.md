# Brand assets

`appicon-source.png` (1254×1254) is the master artwork for the app icon — a calculator
showing `$2,345.67` over a paycheck with a green check badge, on a blue rounded-square
plate. Everything under `PaycheckCalculator.App/Resources/AppIcon/` is derived from it.
Edit the master, then regenerate; don't hand-edit the derived layers.

## What gets derived

| File | What it is |
| --- | --- |
| `Resources/AppIcon/appicon.svg` | Background layer — the plate's vertical gradient, `#0063E9` → `#014DBD`, full bleed. |
| `Resources/AppIcon/appiconfg.png` | Foreground layer — the calculator/cheque/badge on transparency, 1024×1024. |

Both are wired from `PaycheckCalculator.App.csproj` as a single `<MauiIcon>`, which
makes Resizetizer emit an Android adaptive icon (plus the iOS/Mac Catalyst asset
catalogue and the Windows icons — MAUI single project uses one icon everywhere).

## Why the foreground is inset

Android composes adaptive icons from two 108dp layers and lets the launcher mask them
to whatever shape it likes — Pixel's default is a circle. Only the **central 66dp
circle is guaranteed visible** under every mask
([spec](https://developer.android.com/develop/ui/views/launch/icon_design_adaptive)).

In the master, the green check badge sits in the bottom-right corner and the cheque
runs nearly edge to edge, so drawing the artwork full bleed would let a circular mask
cut the badge. Instead the foreground is canvased on the **minimum enclosing circle**
of the artwork's opaque pixels, which makes `ForegroundScale` map straight onto the
safe zone: at `0.60` the content is 64.7dp across, ~2% inside the 66dp allowance.

The plate itself is not drawn into the foreground — the background layer provides it,
and the launcher's mask replaces its rounded-square outline.

`Color="#0158D3"` (the gradient's midpoint) is painted before the background layer, so
a solid plate-blue icon is the worst case if the background SVG ever fails to render.

## Regenerating

The layers were produced by measuring the master rather than by hand:

1. Flood-fill the near-white page in from the corners to find the plate, then erode
   12px so the anti-aliased plate rim can't survive as content.
2. Fit the plate colour per row (it's a linear vertical gradient, flat horizontally)
   and knock pixels near that colour out to transparency, ramping alpha between
   distance 12 and 45 so anti-aliased edges and the objects' shadows feather properly.
3. Compute the minimum enclosing circle of the remaining pixels and re-canvas the
   foreground on it, exporting at 1024×1024.

Any tool that reproduces those steps works; the numbers above are what the current
layers were built with.

## Splash screen

`Resources/Splash/splash.svg` is separate artwork — the wordmark on two lines over a
flat `#0158D3`, matching the icon's midpoint blue so it's seamless against the
`MauiSplashScreen Color`. The glyphs are `<path>` outlines rather than `<text>` so
rendering doesn't depend on a font being present on the build machine.
