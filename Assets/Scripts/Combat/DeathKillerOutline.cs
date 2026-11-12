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

    public static DeathKillerOutline Show(GameObject killer, Camera deathCamera)
    {
        if (!killer || !deathCamera) return null;
        DeathKillerOutline outline = killer.GetComponent<DeathKillerOutline>();
        if (!outline) outline = killer.AddComponent<DeathKillerOutline>();
        outline.Rebuild(deathCamera);
        return outline;
    }

    public void Hide()
    {
        ReleaseBuffer();
        Destroy(this);
    }

    private void OnDestroy() { ReleaseBuffer(); }

    private void Rebuild(Camera deathCamera)
    {
        ReleaseBuffer();
        Material material = Resources.Load<Material>(MaterialResourcePath);
        if (!material)
        {
            Debug.LogWarning("死亡击杀者描边材质未找到。", this);
            return;
        }

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

        _camera = deathCamera;
        _commandBuffer = new CommandBuffer { name = "死亡视角击杀者穿墙红色描边" };
        for (int i = 0; i < _bodyRenderers.Count; i++)
        {
            int count = GetSubMeshCount(_bodyRenderers[i]);
            for (int subMesh = 0; subMesh < count; subMesh++)
            {
                _commandBuffer.DrawRenderer(_bodyRenderers[i], material, subMesh, 0);
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
