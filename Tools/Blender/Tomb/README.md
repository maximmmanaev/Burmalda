# Tomb modular kit

`generate_tomb_modular.py` deterministically builds the editable Blender source,
the Unity FBX, and the shared 256 px URP texture set.

From the repository root, run:

```sh
blender --background --python Tools/Blender/Tomb/generate_tomb_modular.py
```

The five mesh pivots and dimensions are part of the Unity EditMode test contract.
Floor pivots sit at the top centre of a 1 m cell; wall pivots sit at bottom centre.

For a review render of the generated meshes:

```sh
blender --background Tools/Blender/Tomb/burmalda-tomb-modular.blend \
  --python Tools/Blender/Tomb/render_tomb_preview.py
```
