# Atomic Raster Assets

Use an image only for an irreducible, tightly cropped unit whose internal pixels do not need to be editable. Surrounding text, borders, arrows, labels, and connectors must remain native Visio objects.

## Mandatory declaration

Every `visio_test_live_add_image` request must include:

```json
{
  "raster_reason": "A dense reference-specific icon is retained as one object.",
  "source_is_tightly_cropped": true,
  "atomic_raster_unit": true,
  "contains_reconstructable_content": false,
  "decomposition_note": "Text, border, and connectors were rebuilt as editable Visio objects."
}
```

Missing, false, contradictory, or vague declarations return `RASTER_DECLARATION_INVALID`.

## Allowed sources

- a crop registered from the supplied reference;
- a user-provided local image;
- a locally generated asset whose generation path is explicitly available and recorded.

Do not silently call an external image service. If the requested source cannot be produced, return `CAPABILITY_NOT_AVAILABLE`.

## Prohibited uses

- a whole page or whole panel;
- text, arrows, borders, legends, or simple scientific symbols;
- tables, charts, matrices, regular grids, or editable schematic modules;
- an image used to hide wrong geometry or failed routing;
- a loose crop containing unrelated whitespace or neighboring elements.

## Reference crop workflow

1. Register the reference through `visio_test_prepare_reference`.
2. Inspect the target region through `visio_test_get_reference_region`.
3. Extract the smallest coherent unit with `visio_test_extract_reference_asset`.
4. Verify crop bounds, source hash, asset hash, and editable scope.
5. Add the object with the mandatory declaration.
6. Re-audit the object and its native surroundings after any resize or replacement.

## Persistence

Persist the declaration, source kind, source hash, crop bounds, asset hash, editable scope, semantic ID, region ID, and Test Edition protocol version in both Shape Data and User Cells. Save/reopen validation must prove the fields survived serialization.

The image object may be moved, resized, replaced, layered, and grouped. Its internal pixels remain intentionally non-editable and must be reported in final delivery notes.
