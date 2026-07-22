# Scientific Figure Fidelity Baseline

- Date: 2026-07-23
- Purpose: record the pre-enhancement fidelity of the Figure 1 reconstruction.

## Inputs

| Artifact | Size | SHA-256 |
|---|---:|---|
| `figure1_sdmore_overview.png` | 1817 × 866 | `BA30...` |
| `fig1_recreated_2000px.png` | 2000 px wide | recorded in the source workspace |
| `figure1_sdmore_visio_2000px.png` | 2000 × 953 | `9E33...` |
| `figure1_sdmore_visio.vsdx` | 43,724 bytes | `8265...` |

The abbreviated hashes above identify the artifacts inspected during diagnosis. Task 14 records complete hashes for the new acceptance outputs.

## Structural baseline

| Metric | draw.io Figure 1 | Current Visio Figure 1 |
|---|---:|---:|
| Editable vertices or Shapes | 273 | 111 |
| Edges or connectors | 59 | 24 |
| Distinct style keys | 29 | approximately 8 |
| Gradient Shapes | 30 | 0 |
| Rich HTML labels | 18 | 0 |
| Rotated Shapes | 24 | few |

The draw.io source uses independent primitives for people, grids, tensors, waves, routers, latent vectors and skeletons. The current Visio output replaces several of these structures with plain text or ASCII-like symbols.

## Image baseline

After resizing both outputs to the reference dimensions:

| Output | SSIM | MAE | Edge precision | Edge recall | Edge F1 |
|---|---:|---:|---:|---:|---:|
| draw.io Figure 1 | 0.4709 | 0.1036 | 0.5072 | 0.4566 | 0.4806 |
| current Visio Figure 1 | 0.5003 | 0.0946 | 0.5042 | 0.4059 | 0.4497 |

SSIM and MAE reward the large smooth background in the simplified Visio output. Edge recall and regional inspection better reveal missing scientific detail. No future acceptance may rely on global SSIM alone.

## Known visual failures

- Typography lacks the reference font family, bold hierarchy and editable subscript or superscript formatting.
- Waveforms, skeletons, tensor grids and wireless symbols are under-decomposed.
- The video-frame content is replaced by schematic text-like placeholders.
- The scientific stencil does not provide genuine cylinder or cloud geometry.
- Preview checks do not enforce regional similarity or a minimum primitive budget.

## CI fixture

`tests/fixtures/scientific-scene/reference-scene.json` and `reference-preview.png` provide a repository-owned synthetic regression fixture. The fixture covers the same capability classes without depending on the user's external paper directory.
