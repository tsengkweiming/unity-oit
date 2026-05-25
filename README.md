# Order-Independent Transparency

Three OIT techniques implemented in Unity's Built-in Render Pipeline with GPU instancing, command buffers, and compute shaders.

> **Platform:** PC · DirectX 11 / Shader Model 5.0 · Unity 2022.3 LTS

---

## Weighted Blended OIT (WBOIT)

![WBOIT](Screenshots/Screenshot%20WeightedBlendedOit.png)

A single-pass approximation. Each fragment writes into a weighted colour accumulation buffer and a revealage buffer simultaneously via MRT. A composite pass resolves them analytically — no sorting, no extra geometry passes.

Four weight functions are available (`Weight0–3`) to control how much depth influences each fragment's contribution, tunable per scene.

**Scene:** `Assets/Scenes/WBOIT.unity`

---

## Depth Peeling

Two modes implemented:

### Front-to-Back

![Depth Peeling Front2Back](Screenshots/Screenshot%20DepthPeeling-Front2back.png)

Each pass peels the single frontmost unresolved layer by clipping against the previous pass's depth texture. After `N` passes, layers are composited back-to-front.

### Dual Depth Peeling

![Depth Peeling Dual](Screenshots/Screenshot%20DepthPeeling-Dual.png)

Peels the frontmost and backmost layers simultaneously using a min/max depth buffer (`RGFloat`) with `BlendOp Max`. Halves the pass count needed for the same quality as front-to-back.

Both modes support configurable layer count (1–50) and independent resolution scaling for peel passes and the composite step.

**Scene:** `Assets/Scenes/DepthPeeling.unity`

---

## Per-Pixel Linked List OIT (LLOIT)

![LLOIT](Screenshots/Screenshot%20LLOIT.png)

An exact single-pass technique using SM 5.0 UAV writes. Each fragment atomically prepends itself to a per-pixel linked list stored in a structured buffer. A compute shader resets the buffers each frame. The composite shader walks each pixel's list, sorts fragments by depth, and blends back-to-front over the skybox. Supports alpha blend and additive composite modes.

Configurable `_maxNodesPerPixel` and `_resolutionScale` let you trade VRAM against quality. At 1080p with 4 nodes per pixel the node buffer is ~126 MB. 

**Scene:** `Assets/Scenes/LLOIT.unity`

---

## Technique Comparison

| | WBOIT | Depth Peeling (F2B) | Depth Peeling (Dual) | Linked List OIT |
|---|:---:|:---:|:---:|:---:|
| **Correctness** | Approximate | Exact | Exact | Exact |
| **Geometry passes** | 1 | N (layers) | N/2 (layers) | 1 |
| **Sorting** | None | Implicit | Implicit | Per-pixel, runtime |
| **VRAM overhead** | Low | Medium | Medium | High |
| **SM requirement** | 4.0 | 4.0 | 4.0 | **5.0** |
| **Dense overdraw** | Degrades gracefully | Capped by `_layers` | Capped by `_layers/2` | Capped by `_maxNodes` |

## Reference
- [Order-Independent Transparency](https://developer.download.nvidia.com/assets/gamedev/docs/OrderIndependentTransparency.pdf)
- Order Independent Transparency with Dual Depth Peeling by nvidia https://developer.download.nvidia.com/SDK/10/opengl/src/dual_depth_peeling/doc/DualDepthPeeling.pdf
- Order Independent Transparency with Per-Pixel Linked Lists https://www.cs.cornell.edu/~bkovacs/resources/TUBudapest-Barta-Pal.pdf
- Weighted Blended Order-Independent Transparency https://jcgt.org/published/0002/02/09/
