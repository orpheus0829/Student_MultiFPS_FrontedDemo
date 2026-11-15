using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class DeathKillerOutline : MonoBehaviour
{
    private const string MaterialResourcePath = "Combat/DeathKillerOutline";
    private readonly List<Renderer> _bodyRenderers = new List<Renderer>();
    private Camera _camera;
    private CommandBuffer _commandBuffer;
    private Material _material;

    public static DeathKillerOutline Show(GameObject killer, Camera deathCamera)
    {
        return Show(killer, deathCamera, Color.red, "死亡视角击杀者穿墙红色描边");
    }

    public static DeathKillerOutline Show(
        GameObject actor,
        Camera camera,
        Color color,
        string bufferName)
    {
        if (!actor || !camera)
        {
            return null;
        }
        DeathKillerOutline outline = actor.GetComponent<DeathKillerOutline>();
        if (!outline)
        {
            outline = actor.AddComponent<DeathKillerOutline>();
        }
        outline.Rebuild(camera, color, bufferName);
        return outline;
    }

    public void Hide()
    {
        ReleaseBuffer();
        Destroy(this);
    }

    private void OnDestroy() { ReleaseBuffer(); }

    private void Rebuild(Camera camera, Color color, string bufferName)
    {
        ReleaseBuffer();
        Material sourceMaterial = Resources.Load<Material>(MaterialResourcePath);
        if (!sourceMaterial)
        {
            Debug.LogWarning("死亡击杀者描边材质未找到。", this);
            return;
        }
        _material = new Material(sourceMaterial);
        _material.SetColor("_OutlineColor", color);

        PlayerAnimationCtrl animation = GetComponent<PlayerAnimationCtrl>();
        Transform bodyRoot = animation && animation.ThirdPersonView &&
            animation.ThirdPersonView.CharacterAnimator
            ? animation.ThirdPersonView.CharacterAnimator.transform
            : transform;
        Transform firstPersonRoot = animation && animation.FirstPersonView
            ? animation.FirstPersonView.transform
            : null;
        Renderer[] renderers = bodyRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer && !(renderer is ParticleSystemRenderer) &&
                !(renderer is TrailRenderer) &&
                renderer.gameObject.layer != 9 &&
                (!firstPersonRoot || !renderer.transform.IsChildOf(firstPersonRoot)))
            {
                _bodyRenderers.Add(renderer);
            }
        }

        _camera = camera;
        _commandBuffer = new CommandBuffer
        {
            name = string.IsNullOrWhiteSpace(bufferName) ? "角色描边" : bufferName
        };
        for (int i = 0; i < _bodyRenderers.Count; i++)
        {
            int count = GetSubMeshCount(_bodyRenderers[i]);
            for (int subMesh = 0; subMesh < count; subMesh++)
            {
                _commandBuffer.DrawRenderer(_bodyRenderers[i], _material, subMesh, 0);
            }
        }
        RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
    }

    private static int GetSubMeshCount(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh)
        {
            return Mathf.Max(1, skinned.sharedMesh.subMeshCount);
        }
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter && filter.sharedMesh
            ? Mathf.Max(1, filter.sharedMesh.subMeshCount)
            : 1;
    }

    private void ReleaseBuffer()
    {
        RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
        _commandBuffer?.Release();
        _commandBuffer = null;
        if (_material)
        {
            Destroy(_material);
            _material = null;
        }
        _camera = null;
        _bodyRenderers.Clear();
    }

    private void HandleEndCameraRendering(
        ScriptableRenderContext context, Camera renderedCamera)
    {
        if (!_camera || renderedCamera != _camera || _commandBuffer == null)
        {
            return;
        }
        context.ExecuteCommandBuffer(_commandBuffer);
        context.Submit();
    }
}
