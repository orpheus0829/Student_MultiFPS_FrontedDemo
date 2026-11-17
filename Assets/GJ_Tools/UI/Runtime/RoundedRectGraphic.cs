using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/GJ Tools/Rounded Rect Graphic")]
[DisallowMultipleComponent]
public sealed class RoundedRectGraphic : MaskableGraphic
{
    [SerializeField, Min(0f)] private float _radius = 18f;
    [SerializeField, Range(2, 16)] private int _cornerSegments = 6;

    public float Radius
    {
        get { return _radius; }
        set
        {
            _radius = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(
            _radius,
            Mathf.Min(rect.width, rect.height) * 0.5f);
        if (radius <= 0.01f)
        {
            AddQuad(vertexHelper, rect);
            return;
        }

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = rect.center;
        vertexHelper.AddVert(vertex);

        Vector2[] centers =
        {
            new Vector2(rect.xMax - radius, rect.yMax - radius),
            new Vector2(rect.xMin + radius, rect.yMax - radius),
            new Vector2(rect.xMin + radius, rect.yMin + radius),
            new Vector2(rect.xMax - radius, rect.yMin + radius)
        };
        float[] startAngles = { 0f, 90f, 180f, 270f };
        int rimCount = 4 * (_cornerSegments + 1);
        for (int corner = 0; corner < 4; corner++)
        {
            for (int segment = 0; segment <= _cornerSegments; segment++)
            {
                float angle = (startAngles[corner] +
                    90f * segment / _cornerSegments) * Mathf.Deg2Rad;
                vertex.position = centers[corner] +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vertexHelper.AddVert(vertex);
            }
        }

        for (int i = 0; i < rimCount; i++)
        {
            vertexHelper.AddTriangle(0, i + 1, (i + 1) % rimCount + 1);
        }
    }

    private void OnValidate()
    {
        _radius = Mathf.Max(0f, _radius);
        _cornerSegments = Mathf.Clamp(_cornerSegments, 2, 16);
        SetVerticesDirty();
    }

    private void AddQuad(VertexHelper vertexHelper, Rect rect)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = new Vector2(rect.xMin, rect.yMin);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector2(rect.xMin, rect.yMax);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector2(rect.xMax, rect.yMax);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector2(rect.xMax, rect.yMin);
        vertexHelper.AddVert(vertex);
        vertexHelper.AddTriangle(0, 1, 2);
        vertexHelper.AddTriangle(2, 3, 0);
    }
}
