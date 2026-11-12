import bpy
import sys


def import_fbx(path):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=path)
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    print(f"FILE={path}")
    for scene_object in bpy.context.scene.objects:
        print(f"OBJECT={scene_object.type}:{scene_object.name}")
    for armature in armatures:
        print(f"ARMATURE={armature.name}")
        for bone in armature.data.bones:
            print(f"BONE={bone.name}")


for source_path in sys.argv[sys.argv.index("--") + 1:]:
    import_fbx(source_path)
