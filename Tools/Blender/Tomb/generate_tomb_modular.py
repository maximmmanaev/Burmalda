"""Generate Burmalda's mobile tomb floor and wall kit.

Run from the repository root:
    blender --background --python Tools/Blender/Tomb/generate_tomb_modular.py

The script is deterministic and writes the editable .blend source, one Unity
FBX, and three shared 256 px PBR textures. Dimensions are metres.
"""

from __future__ import annotations

import math
import random
from pathlib import Path

import bpy


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SOURCE_BLEND = REPOSITORY_ROOT / "Tools/Blender/Tomb/burmalda-tomb-modular.blend"
MODEL_PATH = REPOSITORY_ROOT / "Assets/Art/Tomb/Models/burmalda-tomb-modular.fbx"
TEXTURE_FOLDER = REPOSITORY_ROOT / "Assets/Art/Tomb/Textures"
MODEL_NAMES = ("Floor_Intact", "Floor_Worn", "Floor_Cracked", "Wall_Straight", "Wall_Corner")


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def sandstone_material() -> bpy.types.Material:
    material = bpy.data.materials.new("TombSandstone_Source")
    material.diffuse_color = (0.40, 0.18, 0.085, 1.0)
    material.roughness = 0.78
    material.metallic = 0.0
    return material


def create_prism(
    name: str,
    polygon: list[tuple[float, float]],
    bottom: float,
    top: float,
    material: bpy.types.Material,
    bevel: float = 0.012,
    bevel_segments: int = 2,
) -> bpy.types.Object:
    count = len(polygon)
    # Build directly in Unity's local convention (X across, Y up, Z forward).
    # Unity exposes FBX Mesh sub-assets before applying the model root's axis
    # transform, so baking this convention into vertices keeps Resources-loaded
    # meshes correctly oriented as well as model GameObjects.
    vertices = [(x, bottom, depth) for x, depth in polygon] + [(x, top, depth) for x, depth in polygon]
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, next_index, next_index + count, index + count))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()

    uv_layer = mesh.uv_layers.new(name="UVMap")
    for polygon_face in mesh.polygons:
        for loop_index in polygon_face.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            if polygon_face.index < 2:
                uv_layer.data[loop_index].uv = (vertex.x + 0.5, vertex.z + 0.5)
            else:
                uv_layer.data[loop_index].uv = (vertex.x + 0.5, (vertex.y - bottom) / max(0.001, top - bottom))

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bevel_modifier = obj.modifiers.new("Readable bevel", "BEVEL")
    bevel_modifier.width = bevel
    bevel_modifier.segments = bevel_segments
    bevel_modifier.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel_modifier.name)
    obj.select_set(False)

    for face in obj.data.polygons:
        face.use_smooth = face.normal.y < 0.99
    obj.data.update()
    return obj


def join_objects(objects: list[bpy.types.Object], name: str) -> bpy.types.Object:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = bpy.context.active_object
    result.name = name
    result.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.select_all(action="DESELECT")
    return result


def create_floor_modules(material: bpy.types.Material) -> list[bpy.types.Object]:
    intact = create_prism(
        "Floor_Intact",
        [(-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5)],
        -0.12,
        0.0,
        material,
        bevel=0.018,
    )

    worn = create_prism(
        "Floor_Worn",
        [
            (-0.5, -0.38), (-0.44, -0.5), (0.34, -0.5), (0.5, -0.42),
            (0.5, 0.34), (0.42, 0.5), (-0.37, 0.5), (-0.5, 0.40),
        ],
        -0.12,
        0.0,
        material,
        bevel=0.018,
    )

    fracture_regions = [
        [(-0.5, -0.5), (-0.08, -0.5), (-0.12, -0.05), (-0.5, -0.16)],
        [(-0.08, -0.5), (0.5, -0.5), (0.5, -0.12), (0.08, -0.04)],
        [(-0.5, -0.16), (-0.12, -0.05), (-0.05, 0.14), (-0.5, 0.5)],
        [(-0.05, 0.14), (0.03, -0.02), (0.5, -0.12), (0.5, 0.22), (0.18, 0.20)],
        [(-0.5, 0.5), (-0.05, 0.14), (0.18, 0.20), (0.08, 0.5)],
        [(0.08, 0.5), (0.18, 0.20), (0.5, 0.22), (0.5, 0.5)],
    ]
    cracked_parts = []
    for index, region in enumerate(fracture_regions):
        center_x = sum(point[0] for point in region) / len(region)
        center_y = sum(point[1] for point in region) / len(region)
        separated = []
        for x, y in region:
            separated.append((
                x if abs(x) == 0.5 else x + (center_x - x) * 0.055,
                y if abs(y) == 0.5 else y + (center_y - y) * 0.055,
            ))
        top = -(index % 3) * 0.002
        cracked_parts.append(create_prism(f"CrackedPiece_{index + 1}", separated, -0.12, top, material, bevel=0.010))
    cracked = join_objects(cracked_parts, "Floor_Cracked")
    return [intact, worn, cracked]


def add_block(
    name: str,
    x_min: float,
    x_max: float,
    depth_min: float,
    depth_max: float,
    bottom: float,
    top: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    return create_prism(
        name,
        [(x_min, depth_min), (x_max, depth_min), (x_max, depth_max), (x_min, depth_max)],
        bottom,
        top,
        material,
        bevel=0.012,
        bevel_segments=1,
    )


def straight_wall_parts(material: bpy.types.Material, prefix: str, depth_min: float, depth_max: float) -> list[bpy.types.Object]:
    rows = [
        (0.00, 0.34, [(-0.50, -0.18), (-0.16, 0.17), (0.19, 0.50)]),
        (0.37, 0.71, [(-0.50, 0.02), (0.04, 0.50)]),
        (0.74, 1.10, [(-0.50, -0.25), (-0.23, 0.24), (0.26, 0.50)]),
    ]
    parts = []
    for row_index, (bottom, top, spans) in enumerate(rows):
        for block_index, (x_min, x_max) in enumerate(spans):
            parts.append(add_block(
                f"{prefix}_R{row_index}_B{block_index}",
                x_min, x_max, depth_min, depth_max, bottom, top, material,
            ))
    return parts


def create_wall_modules(material: bpy.types.Material) -> list[bpy.types.Object]:
    straight = join_objects(straight_wall_parts(material, "Straight", -0.09, 0.09), "Wall_Straight")

    corner_parts = straight_wall_parts(material, "CornerX", -0.50, -0.32)
    second_leg = straight_wall_parts(material, "CornerZ", -0.50, -0.32)
    for obj in second_leg:
        obj.rotation_euler[1] = math.radians(90.0)
        obj.location.x = 0.0
        obj.location.y = 0.0
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        obj.select_set(False)
    # Move the rotated arm to the right edge; both arms overlap only at the corner pillar.
    for obj in second_leg:
        obj.location.x += 0.82
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=False, scale=True)
        obj.select_set(False)
    corner = join_objects(corner_parts + second_leg, "Wall_Corner")
    return [straight, corner]


def write_texture(name: str, pixel_function, colorspace: str = "sRGB") -> None:
    size = 256
    image = bpy.data.images.new(name, width=size, height=size, alpha=True, float_buffer=False)
    image.colorspace_settings.name = colorspace
    pixels = [0.0] * (size * size * 4)
    for y in range(size):
        for x in range(size):
            color = pixel_function(x, y, size)
            offset = (y * size + x) * 4
            pixels[offset:offset + 4] = color
    image.pixels.foreach_set(pixels)
    image.file_format = "PNG"
    image.filepath_raw = str(TEXTURE_FOLDER / name)
    image.save()
    bpy.data.images.remove(image)


def height_value(x: int, y: int, size: int) -> float:
    u = x / size
    v = y / size
    return (
        math.sin(u * math.tau * 3.0 + math.sin(v * 8.0)) * 0.35
        + math.sin(v * math.tau * 4.0 + u * 5.0) * 0.25
        + math.sin((u + v) * math.tau * 9.0) * 0.08
    )


def create_textures() -> None:
    random.seed(240905)
    grain = [random.uniform(-1.0, 1.0) for _ in range(256 * 256)]

    def base_color(x: int, y: int, size: int):
        variation = height_value(x, y, size) * 0.035 + grain[y * size + x] * 0.012
        return (0.40 + variation, 0.19 + variation * 0.65, 0.095 + variation * 0.35, 1.0)

    def normal(x: int, y: int, size: int):
        dx = height_value((x + 1) % size, y, size) - height_value((x - 1) % size, y, size)
        dy = height_value(x, (y + 1) % size, size) - height_value(x, (y - 1) % size, size)
        nx, ny, nz = -dx * 0.8, -dy * 0.8, 1.0
        length = math.sqrt(nx * nx + ny * ny + nz * nz)
        return (nx / length * 0.5 + 0.5, ny / length * 0.5 + 0.5, nz / length * 0.5 + 0.5, 1.0)

    def mask(_x: int, _y: int, _size: int):
        # URP metallic map: R metallic, G occlusion, B detail mask, A smoothness.
        return (0.0, 0.86, 0.0, 0.18)

    write_texture("tomb-sandstone-basecolor.png", base_color, "sRGB")
    write_texture("tomb-sandstone-normal.png", normal, "Non-Color")
    write_texture("tomb-sandstone-mask.png", mask, "Non-Color")


def export_assets(objects: list[bpy.types.Object]) -> None:
    for obj in objects:
        obj.rotation_mode = "XYZ"
        obj.location = (0.0, 0.0, 0.0)
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj.scale = (1.0, 1.0, 1.0)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND))
    bpy.ops.export_scene.fbx(
        filepath=str(MODEL_PATH),
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="AUTO",
        embed_textures=False,
    )


def main() -> None:
    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    TEXTURE_FOLDER.mkdir(parents=True, exist_ok=True)
    SOURCE_BLEND.parent.mkdir(parents=True, exist_ok=True)
    reset_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    material = sandstone_material()
    objects = create_floor_modules(material) + create_wall_modules(material)
    assert tuple(obj.name for obj in objects) == MODEL_NAMES
    create_textures()
    export_assets(objects)
    print("Generated:")
    print(SOURCE_BLEND)
    print(MODEL_PATH)
    print(TEXTURE_FOLDER)


if __name__ == "__main__":
    main()
