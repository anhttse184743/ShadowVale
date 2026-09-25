# Working with assets without Git LFS

The current `Tai` snapshot stores all tracked assets directly in Git. Clone or
pull this branch normally; no `git lfs pull` is required for its current files.
Older commits still contain LFS pointers because published history was preserved.
Checking out those older commits can still require Git LFS.

Keep every committed file below 100 MiB. Splitting commits does not bypass the
per-file limit. Commit Unity `.meta` files together with their assets, and do not
restore the old `filter=lfs` rules in `.gitattributes` when merging branches.

`Natural_Rice.asset` previously exceeded the limit. It is now split into
`Natural_Rice.asset` and `Natural_Rice_Part02.asset`, about 76.61 MiB each, in both
the main project and `Tools/Map01HorizonBuild`. All original triangles, vertex
attributes and winding are retained. The first part keeps its original GUID.
The six historical `Map2_before_botanical` scenes render the second part as a
child of the original rice object, with the same material and renderer settings.
Current gameplay scenes did not reference this historical mesh and were unchanged.

The natural-layout generator also batches rice meshes, so rebuilding it does not
create one oversized asset again. Keep all generated parts and their `.meta` files.

For an existing checkout, save local work before pulling. A normal fast-forward
pull of `Tai` is sufficient when there are no divergent local commits. Do not
reset or force-push a teammate's changes to resolve a divergence.

## Migration validation

- All 72 former LFS working files were verified against their LFS SHA-256 hashes.
- The split preserves all 608,544 triangles, including the exact indexed bytes
  for positions, normals and colors, and the original bounds.
- The scene changes preserve the original object and add one child per backup.
- Unity Editor validation could not run on the migration machine because Unity
  reported no valid Editor license. Open the project with Unity 6000.3.24f1 and
  check the historical scenes before using them for further editing.
