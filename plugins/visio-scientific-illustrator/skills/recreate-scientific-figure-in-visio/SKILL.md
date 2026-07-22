---
name: recreate-scientific-figure-in-visio
description: Recreate, trace, revise, inspect, save, or export scientific figures live inside a visible Microsoft Visio desktop document through Visio COM automation. Use when the user wants to watch editable Visio shapes, text, images, and glued connectors appear step by step, refine an existing live page, preserve manual edits, or receive final VSDX and exported deliverables.
---

# Recreate Scientific Figures in Microsoft Visio

Use the `visio_live_*` MCP tools for visible drawing. Use `visio_validate`, `visio_inspect_file`, and `visio_export` only after the live document has been saved.

## Hard boundaries

- Control only the plugin-owned Microsoft Visio instance through Visio COM automation.
- Never use operating-system mouse, keyboard, window, or general screen automation.
- Never construct a VSDX package first and open it as the live drawing method.
- Never import a completed screenshot or SVG and claim it is an editable reconstruction.
- Page-export previews are allowed only to inspect the active Visio page.
- Do not attach to or close an unrelated user-owned Visio instance.
- Do not silently replace a missing Master or unsupported operation with another Shape.
- Do not save a newly created final VSDX before visible drawing content exists.

## Workflow

1. Inspect every supplied reference with vision. Render the relevant PDF page first when necessary.
2. Decompose the reference into editable Visio primitives: page, panels, 2D Shapes, text, images, glued 1D connectors, groups, containers, legends, colors, typography, line weights, z-order, and routing.
3. Call `visio_live_launch` with an explicit `new` or `open` mode and a visible nonzero step delay. Require a ready document and page before drawing.
4. Use stable semantic IDs. The Bridge stores them in `User.CodexId` and a `NameU` beginning with `Codex_`.
5. Add Shapes and images first, then add connectors whose endpoints are glued to real Shapes.
6. After each logical section, call `visio_live_preview` and compare the exported page with the reference.
7. Before targeted corrections, call `visio_live_inspect` because the user may have edited the page manually.
8. Update only requested fields so manual position, size, styling, grouping, and routing adjustments are preserved.
9. Use `visio_live_fit` to keep the active page visible.
10. After the visible figure is complete, call `visio_live_save`. This is the first final VSDX save for a newly created document.
11. Call `visio_validate`, then export a 2000 px review PNG. Check wording, element count, topology, glued endpoints, alignment, overlap, clipping, z-order, color, whitespace, and correspondence to the reference.
12. After approval, export the requested final deliverables with `visio_export`.

## Tool selection

- Use `visio_live_add_shape` for editable basic Shapes and bundled scientific Masters.
- Use `visio_live_add_image` only for explicitly raster or externally rendered content.
- Use `visio_live_add_connector` for arrows and relationships; require distinct source and target Shapes plus two verified glued endpoints. Choose `dynamic`, `right_angle`, or `straight` routing, or use `polyline` with explicit pixel waypoints. Use explicit connection-point row numbers only when the target Shape already has those `Connections.Xn` cells.
- Use `visio_live_draw_sequence` for paced batches of individually visible operations. Stop at the first reported failure.
- Use `visio_live_group_shapes`, `visio_live_ungroup_shape`, and `visio_live_add_to_container` only with stable semantic IDs on the current page.
- Use `visio_live_clear` and `visio_live_shutdown` only with explicit confirmation.
- Use `visio_live_close_document` and `visio_live_shutdown` with an explicit save, discard, or cancel action.

## Visio-specific fidelity rules

- Treat tool coordinates as top-left pixels; do not pre-convert them to Visio inches.
- Recreate text as editable Shape text and arrows as glued 1D connectors.
- Use universal ShapeSheet names and values through `CellsU`, `FormulaU`, and `NameU`; never rely on localized Cell or Master names.
- Use consistent Master families, line weights, fonts, arrowheads, and semantic colors.
- Route connectors around unrelated Shapes, use explicit polyline waypoints when the reference requires a fixed path, and inspect Begin/End connection records after creation.
- Preserve `User.CodexId` across grouping, saving, reopening, and targeted edits.
- For photographs, microscopy, heatmaps, molecular renders, or dense plots, use an explicit image Shape and disclose that its internal pixels are not editable Visio geometry.
- If the reference is ambiguous or unreadable, state the uncertain parts instead of inventing content.
- If a capability is unavailable, return the limitation; do not use an unrelated fallback.

## Collaboration and manual edits

- Treat the visible Visio page as shared state with the user.
- Inspect again immediately before a targeted correction when manual edits may have occurred.
- Never resend omitted geometry or style fields during a label-only update.
- If a semantic ID is missing or duplicated after manual editing, stop and report the exact error instead of guessing a Shape.
- Keep each logical mutation in its own undo scope so the user can reverse it from Visio.

## Delivery

Return clickable paths for the VSDX source and every export. State validation status, Visio version, page count, Shape count, connector count, and any intentionally rasterized portions.
