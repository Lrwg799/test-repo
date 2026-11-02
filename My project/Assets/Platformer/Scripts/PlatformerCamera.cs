using UnityEngine;

/// <summary>
/// 平滑跟随 + 前瞻 + 关卡边界钳制 + 屏幕震动。
/// </summary>
public class PlatformerCamera : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.16f;
    public Vector2 offset = new Vector2(0f, 1.2f);
    public float lookAheadFactor = 0.35f;
    public float lookAheadMax = 2.5f;
    public bool clampToLevel = true;
    public float levelMinX = -15f;
    public float levelMaxX = 107f;
    public float levelMinY = 1.8f;
    public float levelMaxY = 12f;

    Camera cam;
    Vector3 velocity;
    float lookAhead;
    float shakeTimer;
    float shakeDuration;
    float shakeMagnitude;
    Vector2 shakeOffset;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;

        float desiredLook = 0f;
        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        if (body != null)
            desiredLook = Mathf.Clamp(body.linearVelocity.x * lookAheadFactor, -lookAheadMax, lookAheadMax);
        lookAhead = Mathf.Lerp(lookAhead, desiredLook, 1f - Mathf.Exp(-3.5f * dt));

        Vector3 desired = ComputeDesired();
        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, Mathf.Infinity, dt);

        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;
            float k = shakeDuration > 0.01f ? Mathf.Clamp01(shakeTimer / shakeDuration) : 0f;
            shakeOffset = Random.insideUnitCircle * (shakeMagnitude * k);
        }
        else
        {
            shakeOffset = Vector2.zero;
        }

        transform.position = new Vector3(pos.x + shakeOffset.x, pos.y + shakeOffset.y, pos.z);
    }

    Vector3 ComputeDesired()
    {
        Vector3 desired = target.position + new Vector3(lookAhead + offset.x, offset.y, 0f);
        if (clampToLevel && cam != null)
        {
            float halfWidth = cam.orthographicSize * cam.aspect;
            desired.x = ClampAxis(desired.x, levelMinX + halfWidth, levelMaxX - halfWidth);
            desired.y = ClampAxis(desired.y, levelMinY, levelMaxY);
        }
        desired.z = transform.position.z;
        return desired;
    }

    static float ClampAxis(float value, float min, float max)
    {
        if (min > max) return (min + max) * 0.5f;
        return Mathf.Clamp(value, min, max);
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        lookAhead = 0f;
        velocity = Vector3.zero;
        Vector3 desired = ComputeDesired();
        transform.position = new Vector3(desired.x, desired.y, transform.position.z);
    }

    public void Shake(float magnitude, float duration)
    {
        shakeMagnitude = magnitude;
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTimer = shakeDuration;
    }
}
