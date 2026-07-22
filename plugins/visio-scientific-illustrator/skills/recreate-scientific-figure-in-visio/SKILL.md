---
name: recreate-scientific-figure-in-visio
description: Recreate, trace, revise, inspect, validate, save, or export scientific figures live in a visible Microsoft Visio desktop document through Visio COM automation. Use this skill whenever a user supplies a scientific reference image, paper figure, PDF panel, existing VSDX, or draw.io source and expects a faithful, editable Visio reconstruction with paced visible drawing, rich text, editable geometry, glued connectors, regional previews, visual comparison, or final VSDX/PNG deliverables.
---

# Recreate Scientific Figures in Microsoft Visio

Build the figure as native Visio content in a plugin-owned, visible Visio window. Prefer a complete `SceneGraph` and `visio_live_draw_scene` for substantial figures; use atomic `visio_live_*` tools for inspection, corrections, and small additions. Use file tools only after saving, except `visio_compile_drawio_scene` and `visio_compare_preview`, which do not mutate the live document.

## Non-negotiable boundaries

- Control Visio only through Visio COM automation. Do not use system mouse, keyboard, window-coordinate, clipboard, or generic screen automation.
- Start with `visio_live_launch` and draw into the visible page. Do not generate a completed VSDX first and open it afterward as a performance of live drawing.
- Keep scientific structure native and editable. Do not cover the page with a flattened reference screenshot, completed SVG, or page-sized image.
- Do not imitate scientific icons with punctuation or character sequences. Use editable geometry or `visio_live_add_scientific_component`.
- Do not fake cylinders, clouds, antennas, routers, tensors, video frames, latent vectors, or snowflakes with an unrelated primitive. Use a matching Master/component or editable freeform path.
- Do not accept a superficially white-page similarity metric as evidence of fidelity. Use structural and color-aware visual comparison plus semantic validation.
- Do not silently substitute unsupported geometry, missing Masters, missing fonts, or failed routing. Report the exact limitation and leave the document in an inspectable state.
- Do not attach to, modify, save, or close an unrelated user-owned Visio instance.
- Do not call destructive tools such as `visio_live_clear`, delete, close, or shutdown without the user's scope clearly authorizing that action.

## Fidelity model

Treat the reference as four simultaneous contracts:

1. **Semantic contract** — all meaningful stages, labels, formulas, legends, symbols, repeated items, and relationships exist.
2. **Geometric contract** — panels, shapes, paths, whitespace, alignment, rotation, overlap, and z-order match the reference.
3. **Visual contract** — colors, gradients, opacity, stroke styles, arrowheads, corner treatment, typography, and density are close.
4. **Editability contract** — text remains text; vectors remain Visio geometry; repeated objects remain separate or deliberately grouped; connection endpoints are genuinely glued.

Preserve all four. A low element count may look clean but will fail the primitive budget and usually loses scientific meaning.

## Phase 1: inspect and plan

1. Inspect every supplied reference at full useful resolution. For a PDF, render the relevant page or figure region before decomposing it.
2. Record the canvas aspect ratio, principal axes, panel boundaries, reading order, palette, typography, and visual hierarchy.
3. Divide the page into logical regions that can be completed and checked independently. Typical regions are data input, feature decomposition, model/training, output, and legends.
4. Build an element inventory per region:
   - panels and stage backgrounds;
   - editable labels and formulas;
   - repeated scientific objects;
   - editable freeform path geometry;
   - 1D relationships and their routing;
   - intentional raster content;
   - grouping, layers, protection, and z-order.
5. Estimate a primitive budget from visible repeated objects, symbols, skeleton segments, tensor faces, frames, and connectors. Do not collapse dozens of meaningful items into a few decorative boxes.
6. Define stable semantic IDs before drawing. IDs must describe meaning rather than Visio's transient numeric shape ID.
7. If an editable draw.io source is supplied, call `visio_compile_drawio_scene` to obtain a `SceneGraph`, review its warnings and statistics, then enrich any weak or non-scientific constructs before live execution. Compilation is a scene translation step, not a VSDX generation step.

## Phase 2: construct the SceneGraph

Use top-left pixel coordinates. The Bridge performs the pixel-to-page conversion.

### Layers and order

- Declare layers for backgrounds, structures, connectors, labels, and annotations when the figure is dense.
- Draw page background and large panels first.
- Draw structural shapes, scientific components, paths, and repeated primitives next.
- Create groups only after their members exist.
- Add connectors after both endpoints exist.
- Add labels and annotations after geometry so they remain legible.
- Apply explicit z-order only after grouping and connector creation.

### Shapes and scientific components

- Use `visio_live_add_shape` for rectangles, rounded rectangles, ellipses, lines, polygons, and other true basic shapes.
- Use `visio_live_add_scientific_component` for cylinders, clouds, tensor faces, video frames, latent vectors, wireless links, routers, antennas, stage panels, snowflakes, and other bundled editable scientific symbols.
- Use `visio_live_add_path` for custom curves, splines, silhouettes, irregular arrows, skeleton segments, and reference-specific vector contours. Keep path commands bounded and editable.
- Prefer separate repeated primitives when the reference communicates count, temporal sequence, layers, samples, or uncertainty through repetition.
- Group a scientific object only after its internal parts have the correct styles and relative geometry.

### Rich text and formulas

- Recreate words and formulas as editable Visio text, not raster labels.
- Use rich text runs for font family, size, color, bold, italic, underline, subscript, and superscript differences within one label.
- Preserve exact spelling, capitalization, mathematical signs, subscripts, superscripts, and line breaks.
- Size the text box from the actual reference bounds. Use shrink-to-fit only when it preserves legibility; do not compensate for wrong geometry with tiny text.
- Keep font choices consistent across equivalent semantic roles and disclose unavailable fonts.

### Fill, line, and effects

- Reproduce solid fills, gradients, transparency, dash patterns, line caps, joins, arrowheads, rounded corners, rotation, and shadow only when visible in the reference.
- Use explicit gradient stops and direction where tonal transitions carry structure.
- Keep stroke weight and arrowhead size proportional to the figure scale.
- Use `CellsU`, `FormulaU`, and `NameU` for universal ShapeSheet access. Do not depend on localized ShapeSheet cell or Master names.
- Use `visio_live_get_cells`, `visio_live_set_cells`, and `visio_live_get_geometry` for controlled advanced corrections. Only permitted, typed cells may be changed.

### Connectors and topology

- Model scientific relationships as glued 1D connectors with distinct source and target shapes.
- Choose `straight`, `right_angle`, `dynamic`, or explicit polyline routing from the reference, not convenience.
- Add stable connection points with `visio_live_add_connection_point` when the required attachment location is not represented by the default geometry.
- Use waypoints to avoid unrelated shapes and to reproduce intentional bends.
- Verify both Begin and End glue after drawing and again before delivery.
- Decorative separators or contour strokes may be paths; semantic arrows and flow lines must remain connectors.

### local image policy

- Use `visio_live_add_image` only when the reference contains inherently raster material such as a photograph, microscopy image, heatmap, spectrogram, rendered molecular surface, or dense plot that the user does not require as editable geometry.
- Crop the specific raster region; never use a whole-page reference image as an underlay or cover.
- State which image regions are raster and which surrounding annotations remain editable.
- When the apparent image is actually a schematic frame, icon, device, tensor, waveform, or skeleton, reconstruct it with Visio shapes, components, and paths.

## Phase 3: execute visibly and progressively

1. Call `visio_live_launch` with explicit `new` or `open` mode and a visible, nonzero step delay. For user-requested paced drawing, pass the exact delay to `visio_live_draw_scene`.
2. Execute the planned `SceneGraph` with `visio_live_draw_scene`. Use conflict mode `error` for a new page and `preserve` only when intentionally augmenting user-edited content.
3. Complete one logical region at a time. Configure a region checkpoint so the Bridge refreshes Visio and exports a preview after each region.
4. For an atomic correction outside scene execution, call `visio_live_preview` after the region is coherent; do not inspect an arbitrary half-step as if it were a completed region.
5. Keep every atomic mutation in its own visible step and undo scope. A successful batch must not hide which operation failed.
6. Use `visio_live_fit` as needed so the active content remains visible without changing geometry.
7. Do not save the newly created final VSDX until visible content has actually been drawn.

For small drawings or targeted fixes, `visio_live_draw_sequence` is acceptable. For a multi-region scientific figure, use the scene executor because it enforces dependency order, pacing, conflicts, regional checkpoints, and deterministic IDs.

## Phase 4: inspect and refine

After every region checkpoint:

1. Review the preview against the same reference region.
2. Call `visio_compare_preview` for visual comparison. Interpret MAE together with edge precision/recall/F1, foreground coverage ratio, and mean color difference; no single metric is sufficient.
3. Inspect the active page with `visio_live_inspect` before corrections, especially when the user may have edited Visio manually.
4. Correct only the fields that are wrong. Do not resend omitted geometry or style values during a label-only correction.
5. Use `visio_live_align_shapes`, `visio_live_distribute_shapes`, and `visio_live_match_size` for regular structures only when the reference is actually regular.
6. Use `visio_live_set_z_order`, grouping tools, layers, and protection deliberately; do not flatten editable structure to simplify ordering.
7. Re-export and compare the corrected region before moving on.

If a semantic ID is missing or duplicated after manual editing, stop and report it. Do not guess a shape from proximity.

## Phase 5: final acceptance gate

Do not save and deliver merely because all tool calls returned success. Apply this final acceptance gate:

1. **Semantic completeness** — exact text and expected semantic IDs are present; the primitive budget is credible; no compiler warnings remain unexplained.
2. **Topology** — every semantic connector has two genuine glued endpoints, correct direction, and reference-appropriate routing.
3. **Geometry** — panel proportions, repeated-item spacing, alignment, rotation, z-order, clipping, and whitespace have been reviewed at full-page and regional scale.
4. **Visual comparison** — `visio_compare_preview` reports acceptable edge structure, foreground coverage, and color difference, and the heatmap has no large unexplained regions.
5. **Editability** — text, paths, components, connectors, and meaningful repeated objects can be selected and edited independently or through intentional groups.
6. **File validation** — persist the completed visible page with `visio_live_save`, then run `visio_validate`, `visio_inspect_file`, and `visio_validate_fidelity` using the planned semantic IDs, exact text, primitive budget, connector-glue requirement, and allowed warning count.
7. **Reopen stability** — for high-value figures, reopen the saved VSDX and confirm semantic IDs, grouping, layers, geometry, text, and glue survived serialization.
8. **Final export** — export the requested PNG width, normally 2000 px for review, and any other requested formats from the validated VSDX.

If a gate fails, refine the live page and repeat the relevant checks. Do not lower the acceptance criteria without explaining the specific tradeoff.

## Manual edits and shared state

- Treat the visible Visio page as shared state with the user.
- Inspect immediately before any targeted correction when manual edits may have occurred.
- Preserve `User.CodexId` through grouping, duplication, saving, reopening, and updates. The corresponding `NameU` begins with `Codex_`.
- Use `visio_live_duplicate_shape` for repeated structures only when the duplicate is meant to inherit geometry and style; assign a new semantic ID.
- Use `visio_live_set_protection` only for stable background or template elements, never to conceal uneditable output.
- Close or shut down with an explicit save, discard, or cancel action.

## Delivery report

Return clickable paths for the VSDX and every export. Report:

- Visio version, page count, total shape count, connector count, and group count;
- semantic validation and visual comparison results;
- primitive budget result and connector glue result;
- any remaining warnings or uncertain reference details;
- every intentionally rasterized region;
- the final PNG dimensions and the exact saved VSDX path.
