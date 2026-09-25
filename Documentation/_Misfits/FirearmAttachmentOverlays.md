# Firearm attachment overlays

Configure overlays on each gun's `FirearmAttachmentHost` component. These are layers on the gun's `Sprite`; they do not replace the separate in-hand or equipped clothing artwork. Keep the gun's existing slot configuration when adding these fields.

## Position and rotation

Offsets are `x, y` in tiles, relative to the center of the gun sprite. Positive X moves right; positive Y moves up in the sprite's local coordinates. One tile is 32 artwork pixels, so divide a desired pixel shift by 32:

| Pixels | Offset |
| --- | --- |
| 1 | 0.03125 |
| 2 | 0.0625 |
| 4 | 0.125 |
| 8 | 0.25 |
| 16 | 0.5 |
| 32 | 1 |

For example, `0.125, -0.0625` shifts the overlay four pixels right and two pixels down. These are artwork pixels before the gun's overall sprite scale and camera zoom. A gun with `Sprite.scale: 0.8, 0.8` scales its attachment layers and their offsets too.

The origin is the **center of the overlay's entire image canvas**, including transparent pixels. It is not automatically the attachment's mounting point. For easiest alignment, draw a gun-specific overlay on a transparent canvas the same size as the gun, with the attachment already positioned over the gun; start with `0, 0`. For a tightly cropped attachment, position its image center and account for the distance from that center to the mounting point. Larger canvases can extend beyond the gun's original canvas.

Rotation values without a suffix are degrees. `0` keeps the artwork's original orientation. Rotation is around the overlay's canvas center; the gun's overall transform also applies. Tune rotation first, then position. `visualOrientation` only selects the generic horizontal/diagonal state; it does not rotate custom artwork.

## Fields

| Field | Purpose |
| --- | --- |
| `muzzleVisualRsi` | RSI directory containing both muzzle attachment states; defaults to the shared attachment RSI. |
| `suppressorVisualState` | Gun-specific suppressor state; omitted uses `suppressor-horizontal` or `suppressor-diagonal`. |
| `bayonetVisualState` | Gun-specific bayonet state; omitted uses `bayonet-horizontal` or `bayonet-diagonal`. |
| `muzzleVisualOffset` | Shared fallback position for either muzzle attachment. |
| `suppressorVisualOffset` / `bayonetVisualOffset` | Independent positions, overriding the shared fallback when provided. |
| `suppressorVisualRotation` / `bayonetVisualRotation` | Independent rotations, default zero. |
| `showMuzzleVisual` | Set false if the gun artwork already includes an integral attachment. |
| `opticVisualRsi` | RSI directory containing the scope state. |
| `opticVisualState` | Scope state name; no optic overlay is drawn without this setting. |
| `opticVisualOffset` / `opticVisualRotation` | Scope position and rotation. |

An RSI state must exist in its `meta.json` and have its corresponding image. Paths start with `/Textures/`. State names below are illustrative; create the artwork before assigning them.

```yaml
  - type: FirearmAttachmentHost
    muzzleVisualRsi: /Textures/_Misfits/Objects/Weapons/Guns/Attachments/m14.rsi
    suppressorVisualState: suppressor
    suppressorVisualOffset: 0.125, 0
    suppressorVisualRotation: 0
    bayonetVisualState: bayonet
    bayonetVisualOffset: 0, -0.0625
    bayonetVisualRotation: 0
    opticVisualRsi: /Textures/_Misfits/Objects/Weapons/Guns/Attachments/m14.rsi
    opticVisualState: scope
    opticVisualOffset: 0, 0.03125
    opticVisualRotation: 0
```

These visual fields do not grant attachment compatibility. `muzzleSlot.whitelist` controls allowed muzzle attachments, and `enableOpticSlot` plus `opticSlot` enables scopes. Bayonets and suppressors occupy one shared slot; the scope has its own slot. Prototype children inherit these settings unless overridden.

After changing a prototype, reload prototypes or restart the client/server and spawn a fresh gun. Install the relevant attachment to see its overlay. Fine-tune in steps of `0.03125` for one artwork pixel; test both muzzle attachment types and the scope together.
