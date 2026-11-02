using UnityEngine;

/// <summary>
/// 视差背景层：按相机位移的比例移动自己（factor 越小离得越远）。
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    public Transform cameraTransform;
    public float factorX = 0.5f;
    public float factorY = 0.1f;
    public bool lockY;

    Vector3 startPosition;
    Vector3 startCamera;

    void Start()
    {
        startPosition = transform.position;
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (cameraTransform != null) startCamera = cameraTransform.position;
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;
        Vector3 cam = cameraTransform.position;
        float x = startPosition.x + (cam.x - startCamera.x) * factorX;
        float y = startPosition.y + (lockY ? 0f : (cam.y - startCamera.y) * factorY);
        transform.position = new Vector3(x, y, startPosition.z);
    }
}
