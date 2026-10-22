# TaskBard icon assets

> **Placeholder artwork.** The music-note-over-taskbar-buttons mark is a
> stand-in until the brand artwork from #5 lands. Replacing it means editing
> `mark.txt` and the palettes in `generate.ps1`, or dropping in new outputs
> under the same file names. No code or manifest changes are needed.

## Sources

- `mark.txt`: the 16×16 fill grid (`.` transparent, `n` note, `b` taskbar
  buttons). The generator adds a 1 px outline (`o`) around every filled pixel.
- `generate.ps1`: the palettes, output sizes and file names.

## Palettes

All variants share the same pixels and differ only in palette.

| Palette | Used for |
|---------|----------|
| `canonical` | **The canonical palette.** Executable icon, Start menu, Store and every MSIX package image. These surfaces don't follow the live Windows theme. |
| `light` | Tray icon on a light taskbar |
| `dark` | Tray icon on a dark taskbar |
| `hc` | Tray icon in High Contrast (black and white only) |

## Outputs

| File | Contents |
|------|----------|
| `../TaskBard/TaskBard.ico` | Executable icon (`ApplicationIcon`): 16, 24, 32, 48, 64, 128 and 256 px |
| `../TaskBard.Package/Images/*.png` | Every image `Package.appxmanifest` references, at scale-100/125/150/200/400, plus `Square44x44Logo` target sizes 16/24/32/48/256 (plated and `altform-unplated`) |
| `tray/tray-{light,dark,hc}.ico` | Tray variants: 16, 20, 24, 32, 40 and 48 px |

`TaskBard.Package/Images/` is the only package-image tree. The generator
clears it before writing, so don't hand-edit files there.

## Regenerating

```powershell
.\assets\generate.ps1
.\make.ps1 test   # PackageAssetTests checks every size and manifest reference
```

Generation is deliberately outside the build. Builds and publishes use only
the committed outputs. The script needs Windows PowerShell or PowerShell 7
on Windows (it uses `System.Drawing`). The same inputs give byte-identical
outputs.

The mark is scaled by whole pixels only (nearest neighbour, no
anti-aliasing) and centred. A size that isn't a multiple of 16 gets
transparent padding, not a blurry resample.
