using System.Collections;
using UnityEngine;

[AddComponentMenu("TimelineKit/HitFlash")]
[RequireComponent(typeof(DamageableHealth))]
public class HitFlash : MonoBehaviour
{
    [Header("受击反馈")]
    [Tooltip("闪红持续时长(秒)，受击瞬间材质颜色切到闪色，到时恢复")]
    public float duration = 0.08f;
    [Tooltip("命中瞬间的闪色")]
    public Color flashColor = new Color(1f, 0.25f, 0.25f, 1f);

    private DamageableHealth _hp;
    private Renderer[] _renderers;
    private Color[] _originColors;
    private string[] _colorKeys;
    private Coroutine _flashCo;

    private void Start()
    {
        _hp = GetComponent<DamageableHealth>();
        if (_hp == null)
        {
            return;
        }
        _renderers = GetComponentsInChildren<Renderer>(true);
        if (_renderers.Length == 0)
        {
            return;
        }
        _originColors = new Color[_renderers.Length];
        _colorKeys = new string[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            Material mat = r.sharedMaterial;
            _colorKeys[i] = mat != null && mat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            _originColors[i] = r.material.GetColor(_colorKeys[i]);
        }
        _hp.onDamaged += OnDamaged;
    }

    private void OnDamaged(float amount, float hp)
    {
        if (amount <= 0f || _flashCo != null)
        {
            return;
        }
        _flashCo = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].material.SetColor(_colorKeys[i], flashColor);
            }
        }
        float wait = duration;
        if (wait <= 0f)
        {
            wait = 0.08f;
        }
        yield return new WaitForSeconds(wait);
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].material.SetColor(_colorKeys[i], _originColors[i]);
            }
        }
        _flashCo = null;
    }
}
