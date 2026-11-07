using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public sealed class BulletImpactVisual : MonoBehaviour
{
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _properties;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _properties = new MaterialPropertyBlock();
        // Addressables 实例先激活，再由命中逻辑赋贴图；这段间隔不能显示默认白色方片。
        _renderer.enabled = false;
    }

    public void SetTexture(Texture texture)
    {
        if (!_renderer || !texture)
        {
            return;
        }

        _properties.SetTexture(MainTex, texture);
        _properties.SetColor(ColorProperty, Color.white);
        _renderer.SetPropertyBlock(_properties);
        _renderer.enabled = true;
    }

    public void SetAlpha(float alpha)
    {
        if (!_renderer)
        {
            return;
        }

        _properties.SetColor(ColorProperty, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
        _renderer.SetPropertyBlock(_properties);
    }
}
