using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableOutline : MonoBehaviour
{
    [Header("Viền vàng cho vật có thể tương tác")]
    public Color outlineColor = Color.yellow;
    public float lineWidth = 0.03f;
    public float padding = 0.05f;
    [Tooltip("Nhấp nháy nhẹ để gây chú ý")]
    public bool pulse = true;
    public float pulseSpeed = 4f;
    [Tooltip("Bật/Tắt viền vàng (cả Game view lẫn Scene view)")]
    public bool drawOutline = true;

    private BoxCollider boxCol;
    private Collider col; // Cache: không GetComponent lại mỗi lần DrawOutline

    // Hai nguồn highlight độc lập:
    // proximity = người chơi đứng trong vùng trigger
    // aimed     = tâm ngắm (crosshair) đang chỉ vào vật
    private bool proximity;
    private bool aimed;
    private bool applied;
    private bool lineShown; // Trạng thái thực tế của LineRenderer ( tách riêng để xử lý drawOutline đổi lúc runtime )

    public bool IsHighlightActive => proximity || aimed;

    // Cho biết người chơi có đang đứng trong vùng trigger tương tác hay không
    public bool IsProximityActive => proximity;

    private LineRenderer line;

    private void Awake()
    {
        boxCol = GetComponent<BoxCollider>();
        col = GetComponent<Collider>();

        line = GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();

        // Ưu tiên shader hỗ trợ vertex color cho LineRenderer (URP project)
        Shader shader = Shader.Find("Universal Render Pipeline/Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Diffuse");
        line.material = new Material(shader);
        line.useWorldSpace = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.loop = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
    }

    // Tự phát hiện người chơi bước vào/ra khỏi vùng trigger
    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            SetProximity(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            SetProximity(false);
        }
    }

    private void Update()
    {
        if (!IsHighlightActive || line == null || !line.enabled) return;

        Color c = outlineColor;
        if (pulse)
        {
            c.a = 0.5f + 0.5f * Mathf.PingPong(Time.time * pulseSpeed, 1f);
        }
        line.startColor = c;
        line.endColor = c;
    }

    // Một trong hai nguồn highlight có thay đổi -> cập nhật lại viền + prompt.
    // Trạng thái prompt (dựa vào "có tương tác được không") tách riêng khỏi
    // trạng thái LineRenderer (tùy thêm drawOutline) để đồng bộ đúng với nhau.
    private void ApplyVisuals()
    {
        bool active = IsHighlightActive;
        bool shouldShow = active && drawOutline;

        if (active != applied)
        {
            applied = active;
            if (active && line != null)
            {
                DrawOutline();
            }

            if (DialogueUIManager.Instance != null)
            {
                DialogueUIManager.Instance.RequestPrompt(active);
            }
        }

        if (shouldShow != lineShown && line != null)
        {
            lineShown = shouldShow;
            line.enabled = shouldShow;
        }
    }

    public void SetProximity(bool on)
    {
        if (proximity == on) return;
        proximity = on;
        ApplyVisuals();
    }

    public void SetAimed(bool on)
    {
        if (aimed == on) return;
        aimed = on;
        ApplyVisuals();
    }

    private void DrawOutline()
    {
        if (col == null) col = GetComponent<Collider>();
        if (col == null) return;

        Bounds b = col.bounds;
        Vector3 center = b.center;
        Vector3 size = b.size;

        float halfX = size.x * 0.5f + padding;
        float halfY = size.y * 0.5f + padding;

        // Vẽ khung quanh mặt trước của vật (theo hướng nhìn của vật)
        Vector3 f = center + transform.forward * (size.z * 0.5f + padding);
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        Vector3 tr = f + right * halfX + up * halfY;
        Vector3 tl = f - right * halfX + up * halfY;
        Vector3 bl = f - right * halfX - up * halfY;
        Vector3 br = f + right * halfX - up * halfY;

        line.positionCount = 4;
        line.SetPosition(0, tr);
        line.SetPosition(1, tl);
        line.SetPosition(2, bl);
        line.SetPosition(3, br);
    }

    // Vẽ khung vàng trong cửa sổ Scene khi highlight đang bật
    private void OnDrawGizmos()
    {
        if (!drawOutline || !IsHighlightActive) return;

        if (boxCol == null) boxCol = GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            Gizmos.color = outlineColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCol.center, boxCol.size);
        }
    }
}