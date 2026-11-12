import bpy
import mathutils
import os
import sys


args = sys.argv[sys.argv.index("--") + 1:]
source_path, target_path, output_path = args


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


clear_scene()
bpy.ops.import_scene.fbx(filepath=source_path)
source_objects = {obj.name: obj for obj in bpy.context.scene.objects}
source_actions = {
    name: obj.animation_data.action
    for name, obj in source_objects.items()
    if obj.animation_data and obj.animation_data.action
}

# FBX animation-only files arrive as an animated Empty hierarchy. Temporarily
# remove their actions to recover the bind/rest transform exported with them.
for name, action in source_actions.items():
    source_objects[name].animation_data.action = None
bpy.context.scene.frame_set(0)
bpy.context.view_layer.update()
source_rest_world = {
    name: source_objects[name].matrix_world.copy() for name in source_actions
}
for name, action in source_actions.items():
    source_objects[name].animation_data.action = action

bpy.ops.import_scene.fbx(filepath=target_path)
armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
if not armatures:
    raise RuntimeError("First-person arms FBX has no armature")
target = armatures[-1]
target.name = "root"


mapping = {
    "Bip001 Spine": "spine_01",
    "Bip001 Spine1": "spine_02",
    "Bip001 Spine2": "spine_03",
    "Bip001 L Clavicle": "clavicle_l",
    "Bip001 L UpperArm": "upperarm_l",
    "Bip001 L Forearm": "lowerarm_l",
    "Bip001 L Hand": "hand_l",
    "Bip001 R Clavicle": "clavicle_r",
    "Bip001 R UpperArm": "upperarm_r",
    "Bip001 R Forearm": "lowerarm_r",
    "Bip001 R Hand": "hand_r",
}

finger_names = [("Finger0", "thumb"), ("Finger1", "index"),
                ("Finger2", "middle"), ("Finger3", "ring"),
                ("Finger4", "pinky")]
for side_long, side_short in (("L", "l"), ("R", "r")):
    for source_finger, target_finger in finger_names:
        for source_suffix, target_index in (("", 1), ("1", 2), ("2", 3)):
            mapping[f"Bip001 {side_long} {source_finger}{source_suffix}"] = (
                f"{target_finger}_{target_index:02d}_{side_short}")

missing = [f"{src}->{dst}" for src, dst in mapping.items()
           if src not in source_objects or dst not in target.pose.bones]
if missing:
    raise RuntimeError("Missing mapped bones: " + ", ".join(missing))

target.animation_data_clear()
target.animation_data_create()
action = bpy.data.actions.new("FP_Anby_Attack_Normal_02")
target.animation_data.action = action

target_rest_world_rot = {}
for source_name, target_name in mapping.items():
    bone_rest_arm = target.data.bones[target_name].matrix_local
    target_rest_world_rot[target_name] = (
        target.matrix_world @ bone_rest_arm).to_quaternion()

scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start = 0
scene.frame_end = 20

for frame in range(0, 21):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    for source_name, target_name in mapping.items():
        source_rotation = source_objects[source_name].matrix_world.to_quaternion()
        source_rest_rotation = source_rest_world[source_name].to_quaternion()
        world_delta = source_rotation @ source_rest_rotation.inverted()
        desired_world_rotation = world_delta @ target_rest_world_rot[target_name]
        desired_arm_rotation = (
            target.matrix_world.to_quaternion().inverted() @ desired_world_rotation)

        pose_bone = target.pose.bones[target_name]
        current = pose_bone.matrix.copy()
        pose_bone.matrix = mathutils.Matrix.LocRotScale(
            current.translation, desired_arm_rotation, current.to_scale())
        pose_bone.keyframe_insert("rotation_quaternion", frame=frame, group=target_name)
    bpy.context.view_layer.update()

# Add a stable wrapper so Unity keeps the armature object name (`root`) in all
# curve paths. Runtime first-person Animators live one level above that root.
wrapper = bpy.data.objects.new("FP_AnbySlash02", None)
bpy.context.collection.objects.link(wrapper)
target.parent = wrapper

# Keep only the wrapper and animated armature in the exported clip. Unity will
# bind its Generic transform curves onto the existing first-person hierarchy.
bpy.ops.object.select_all(action="DESELECT")
target.select_set(True)
wrapper.select_set(True)
bpy.context.view_layer.objects.active = target
os.makedirs(os.path.dirname(output_path), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=output_path,
    use_selection=True,
    object_types={"ARMATURE"},
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    bake_anim_simplify_factor=0.0,
    apply_unit_scale=True,
)
print(f"EXPORTED={output_path}")
