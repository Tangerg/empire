# Connected working yard

Three original buildings replace the remaining generic C01 parcels without
changing their fixed footprints or placements. The lower forge's cart bay faces
east, the covered rain yard is approached from the south, and the higher row
faces the market. Forge, yard and weighhouse now read as different uses along
the same street. The market remains the tactical focal point.

Stage each folder outside the repository before running Blender. Generated
previews and intermediate palettes are not game inputs.

| Source | Staged export | Runtime resource |
| --- | --- | --- |
| `north-terrace/build.py` | `c01_n2_service_compound.glb` | `c01_row_6x2.glb` |
| `forge/build.py` | `c01_market_6x3.glb` | `c01_market_6x3.glb` |
| `rain-yard/build_rainyard.py` | `c01_rainyard_4x4.glb` | `c01_store_4x4.glb` |

```sh
blender -b -t 2 --python build.py
# For the rain yard only:
blender -b -t 2 --python build_rainyard.py
```

The cart track turns south before going around E2 and follows the existing east
grade back to the court. It must not imply a shortcut over the retaining wall
between the forge and the upper court. The track is only a shader treatment
of canonical terrain; it does not add a movement or picking surface. Building
GLBs contain separate `Roof` and `Walls` meshes and no tactical metadata.
