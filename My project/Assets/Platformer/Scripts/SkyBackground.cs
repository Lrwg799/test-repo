using UnityEngine;

/// <summary>天空渐变：始终铺满相机视野，跟着相机走（编辑模式下也生效）。</summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class SkyBackground : MonoBehaviour
{
    public Camera targetCamera;
    SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        Refresh();
    }

    void OnEnable()
    {
        Refresh();
    }

    void LateUpdate()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null || spriteRenderer == null || spriteRenderer.sprite == null) return;

        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;

        Vector3 size = spriteRenderer.sprite.bounds.size;
        transform.position = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, transform.position.z);
        transform.localScale = new Vector3(width / Mathf.Max(0.01f, size.x) * 1.02f,
                                           height / Mathf.Max(0.01f, size.y) * 1.02f, 1f);
    }
}
