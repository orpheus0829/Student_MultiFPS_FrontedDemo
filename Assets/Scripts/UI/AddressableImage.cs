using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class AddressableImage : MonoBehaviour
{
    [Header("图片资源")]
    [SerializeField, ResourceId(typeof(Sprite)), InspectorName("图片资源 ID")]
    private string _spriteResourceId;

    private Image _image;
    private ResourceMgr _resources;
    private string _loadedResourceId;
    private int _loadVersion;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private async void OnEnable()
    {
        int version = ++_loadVersion;
        if (!_image || string.IsNullOrWhiteSpace(_spriteResourceId))
        {
            return;
        }

        _resources = ResourceMgr.instance;
        try
        {
            Sprite sprite = await _resources.LoadAsync<Sprite>(_spriteResourceId);
            if (version != _loadVersion || !this || !isActiveAndEnabled)
            {
                _resources.Release<Sprite>(_spriteResourceId);
                return;
            }

            ReleaseLoadedSprite();
            _loadedResourceId = _spriteResourceId;
            _image.sprite = sprite;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"地址图片加载失败：{_spriteResourceId}，{exception.Message}", this);
        }
    }

    private void OnDisable()
    {
        _loadVersion++;
        ReleaseLoadedSprite();
    }

    public void SetResourceId(string resourceId)
    {
        _spriteResourceId = resourceId;
    }

    private void ReleaseLoadedSprite()
    {
        if (_resources && !string.IsNullOrWhiteSpace(_loadedResourceId))
        {
            _resources.Release<Sprite>(_loadedResourceId);
        }
        _loadedResourceId = null;
        if (_image)
        {
            _image.sprite = null;
        }
    }
}
