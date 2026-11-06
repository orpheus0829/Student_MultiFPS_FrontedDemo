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
    private EventMgr.Subscription _damageSubscription;
    private SmartTimer.TimerTask _flashTask;

    private void Awake()
    {
        _hp = GetComponent<DamageableHealth>();
        if (!_hp)
        {
            Debug.LogError($"{name} 缺少受伤组件。", this);
            enabled = false;
            return;
        }

        _renderers = GetComponentsInChildren<Renderer>(true);
        if (_renderers.Length == 0)
        {
            Debug.LogWarning($"{name} 没有可用于受击闪色的 Renderer。", this);
            enabled = false;
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

    }

    private void OnEnable()
    {
        if (_hp && _renderers != null && _renderers.Length > 0)
        {
            _damageSubscription = EventMgr.Bind(this, StartListening, StopListening);
        }
    }

    private void OnDisable()
    {
        if (_damageSubscription != null)
        {
            _damageSubscription.Dispose();
            _damageSubscription = null;
        }
    }

    private void OnDamaged(float amount, float hp)
    {
        if (amount <= 0f || (_flashTask != null && _flashTask.IsRunning))
        {
            return;
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i])
            {
                _renderers[i].material.SetColor(_colorKeys[i], flashColor);
            }
        }

        float wait = duration;
        if (wait <= 0f)
        {
            wait = 0.08f;
        }

        _flashTask = SmartTimer.instance.SetTimer(this, wait, FinishFlash);
    }

    private void FinishFlash()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i])
            {
                _renderers[i].material.SetColor(_colorKeys[i], _originColors[i]);
            }
        }

        _flashTask = null;
    }

    private void StartListening()
    {
        _hp.onDamaged += OnDamaged;
    }

    private void StopListening()
    {
        _hp.onDamaged -= OnDamaged;
    }
}
