# Texture / geo data credits

| File | Source / basis |
|------|----------------|
| `earth_day.jpg` | NASA Blue Marble style (webgl-earth) |
| `earth_night.jpg` | Solar System Scope / city lights |
| `earth_clouds.jpg` | Solar System Scope |
| `earth_water.png` | three-globe water mask (ocean vs land) |
| `earth_topology.png` | three-globe topology |

## Aurora placement

Auroral ovals are generated in `EarthGeo` from approximate **2024–2025 magnetic poles**:

- Magnetic North ≈ 86.5°N, 164°W
- Magnetic South ≈ 64.1°S, 135.9°E
- Quiet-time oval center ≈ **67° magnetic latitude**, half-width ≈ 5.5°

Not a live NOAA feed yet — static WMM-style poles + oval model.
