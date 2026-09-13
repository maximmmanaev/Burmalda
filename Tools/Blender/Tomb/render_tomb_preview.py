"""Render an inspection sheet from the generated Blender source.

Run from the repository root after generate_tomb_modular.py:
    blender --background Tools/Blender/Tomb/burmalda-tomb-modular.blend \
      --python Tools/Blender/Tomb/render_tomb_preview.py
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
OUTPUT_PATH = Path("/tmp/burmalda-tomb-modular-preview.png")
TEXTURE_FOLDER = REPOSITORY_ROOT / "Assets/Art/Tomb/Textures"


def look_at(obj: bpy.types.Object, target: tuple[float, float, float]) -> None:
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_material() -> bpy.types.Material:
    material = bpy.data.materials.get("TombSandstone_Source")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.78
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    base = nodes.new("ShaderNodeTexImage")
    base.image = bpy.data.images.load(str(TEXTURE_FOLDER / "tomb-sandstone-basecolor.png"))
    links.new(base.outputs["Color"], shader.inputs["Base Color"])

    normal_texture = nodes.new("ShaderNodeTexImage")
    normal_texture.image = bpy.data.images.load(str(TEXTURE_FOLDER / "tomb-sandstone-normal.png"))
    normal_texture.image.colorspace_settings.name = "Non-Color"
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.35
    links.new(normal_texture.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
    return material


def upright_copy(source: bpy.types.Object, name: str, x: float, y: float, rotation_z: float = 0.0) -> bpy.types.Object:
    copy = source.copy()
    copy.data = source.data
    copy.name = name
    bpy.context.collection.objects.link(copy)
    copy.hide_render = False
    copy.rotation_euler = (math.radians(90.0), 0.0, rotation_z)
    copy.location = (x, y, 0.0)
    return copy


def main() -> None:
    material = configure_material()
    sources = {name: bpy.data.objects[name] for name in (
        "Floor_Intact", "Floor_Worn", "Floor_Cracked", "Wall_Straight", "Wall_Corner")}
    for source in sources.values():
        source.hide_render = True

    floor_pattern = (
        "Floor_Intact", "Floor_Worn", "Floor_Cracked",
        "Floor_Worn", "Floor_Intact", "Floor_Cracked",
        "Floor_Intact", "Floor_Worn", "Floor_Intact",
    )
    for index, mesh_name in enumerate(floor_pattern):
        x = index % 3 - 1.0
        y = index // 3 - 1.0
        obj = upright_copy(sources[mesh_name], f"Preview_{mesh_name}_{index}", x, y)
        obj.data.materials.clear()
        obj.data.materials.append(material)

    for index in range(3):
        wall = upright_copy(sources["Wall_Straight"], f"Preview_Wall_Back_{index}", index - 1.0, 2.0)
        wall.data.materials.clear()
        wall.data.materials.append(material)
    corner = upright_copy(sources["Wall_Corner"], "Preview_Wall_Corner", 2.0, 1.0)
    corner.data.materials.clear()
    corner.data.materials.append(material)

    world = bpy.context.scene.world
    world.color = (0.006, 0.004, 0.003)
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.008, 0.004, 0.002, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.18

    key_data = bpy.data.lights.new("Warm key", "AREA")
    key_data.energy = 900.0
    key_data.color = (1.0, 0.30, 0.08)
    key_data.shape = "DISK"
    key_data.size = 4.0
    key = bpy.data.objects.new("Warm key", key_data)
    bpy.context.collection.objects.link(key)
    key.location = (-2.5, -2.5, 4.5)
    look_at(key, (0.0, 0.5, 0.0))

    fill_data = bpy.data.lights.new("Soft fill", "AREA")
    fill_data.energy = 500.0
    fill_data.color = (0.35, 0.18, 0.10)
    fill_data.size = 5.0
    fill = bpy.data.objects.new("Soft fill", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (3.0, -1.0, 3.5)
    look_at(fill, (0.0, 0.7, 0.0))

    camera_data = bpy.data.cameras.new("Preview Camera")
    camera = bpy.data.objects.new("Preview Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (4.8, -6.8, 5.8)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 6.2
    look_at(camera, (0.0, 0.55, 0.0))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(OUTPUT_PATH)
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.ops.render.render(write_still=True)
    print(f"Rendered {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
