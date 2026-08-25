using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CombatCameraController : MonoBehaviour
{
    public static CombatCameraController Instance { get; private set; }

    [SerializeField] private Transform player;
    [SerializeField] private Vector3 followOffset = new(0f, 0f, -10f);
    [SerializeField] private float followSmoothTime = 0.14f;
    [SerializeField] private float lockSmoothTime = 0.09f;
    [SerializeField] private float zoomSmoothTime = 0.12f;
    [SerializeField] private float lockDuration = 0.85f;
    [SerializeField] private float targetMemoryDuration = 1.05f;
    [SerializeField] private float singleTargetZoom = 3.75f;
    [SerializeField] private float minLockZoom = 3.45f;
    [SerializeField] private float maxLockZoom = 6.25f;
    [SerializeField] private float hitFocusPadding = 1.35f;
    [SerializeField, Range(0f, 1f)] private float enemyFocusBias = 0.62f;

    private readonly List<LockTarget> hitTargets = new();
    private Camera controlledCamera;
    private CameraShake cameraShake;
    private Vector3 basePosition;
    private Vector3 positionVelocity;
    private float baseOrthographicSize;
    private float zoomVelocity;
    private float lockEndTime;

    private struct LockTarget
    {
        public Transform Transform;
        public Vector3 Position;
        public float LastHitTime;

        public LockTarget(Transform target, float time)
        {
            Transform = target;
            Position = target != null ? target.position : Vector3.zero;
            LastHitTime = time;
        }
    }

    private void Awake()
    {
        Instance = this;
        controlledCamera = GetComponent<Camera>();
        cameraShake = GetComponent<CameraShake>();
        basePosition = transform.position;
        baseOrthographicSize = controlledCamera.orthographicSize;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        cameraShake ??= GetComponent<CameraShake>();
        EnsurePlayer();
        PruneExpiredTargets();

        bool locked = Time.unscaledTime < lockEndTime && hitTargets.Count > 0;
        Vector3 desiredPosition = locked ? GetLockPosition() : GetFollowPosition();
        float desiredSize = locked ? GetLockZoom() : baseOrthographicSize;
        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float smoothTime = locked ? lockSmoothTime : followSmoothTime;

        basePosition = Vector3.SmoothDamp(basePosition, desiredPosition, ref positionVelocity, smoothTime, Mathf.Infinity, deltaTime);
        controlledCamera.orthographicSize = Mathf.SmoothDamp(
            controlledCamera.orthographicSize,
            desiredSize,
            ref zoomVelocity,
            zoomSmoothTime,
            Mathf.Infinity,
            deltaTime);

        Vector3 shakeOffset = cameraShake != null ? cameraShake.CurrentOffset : Vector3.zero;
        transform.position = basePosition + shakeOffset;
    }

    public static CombatCameraController Ensure(Camera camera, Transform playerTransform)
    {
        if (camera == null) return null;

        CombatCameraController controller = camera.GetComponent<CombatCameraController>();
        if (controller == null)
        {
            controller = camera.gameObject.AddComponent<CombatCameraController>();
        }

        controller.SetPlayer(playerTransform);
        return controller;
    }

    public static void RegisterHit(Transform hitTarget)
    {
        if (hitTarget == null) return;

        Transform playerTransform = Player.Instance != null ? Player.Instance.transform : null;
        CombatCameraController controller = Instance != null ? Instance : Ensure(Camera.main, playerTransform);
        if (controller == null) return;

        controller.SetPlayer(playerTransform);
        controller.AddHitTarget(hitTarget);
    }

    public void SetPlayer(Transform playerTransform)
    {
        if (playerTransform == null) return;

        bool needsInitialOffset = player == null;
        player = playerTransform;
        if (needsInitialOffset)
        {
            Vector3 currentOffset = transform.position - player.position;
            followOffset = new Vector3(currentOffset.x, currentOffset.y, currentOffset.z);
            basePosition = transform.position - (cameraShake != null ? cameraShake.CurrentOffset : Vector3.zero);
        }

        if (controlledCamera != null && baseOrthographicSize <= 0f)
        {
            baseOrthographicSize = controlledCamera.orthographicSize;
        }
    }

    private void AddHitTarget(Transform target)
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < hitTargets.Count; i++)
        {
            if (hitTargets[i].Transform == target)
            {
                hitTargets[i] = new LockTarget(target, now);
                lockEndTime = now + lockDuration;
                return;
            }
        }

        hitTargets.Add(new LockTarget(target, now));
        lockEndTime = now + lockDuration;
    }

    private void EnsurePlayer()
    {
        if (player != null) return;

        if (Player.Instance != null)
        {
            SetPlayer(Player.Instance.transform);
        }
    }

    private void PruneExpiredTargets()
    {
        float now = Time.unscaledTime;
        for (int i = hitTargets.Count - 1; i >= 0; i--)
        {
            if (now - hitTargets[i].LastHitTime > targetMemoryDuration)
            {
                hitTargets.RemoveAt(i);
            }
        }
    }

    private Vector3 GetFollowPosition()
    {
        if (player == null)
        {
            return basePosition;
        }

        return player.position + followOffset;
    }

    private Vector3 GetLockPosition()
    {
        Bounds bounds = GetFocusBounds();
        Vector3 center = bounds.center;

        if (hitTargets.Count == 1 && player != null)
        {
            center = Vector3.Lerp(player.position, GetTargetPosition(hitTargets[0]), enemyFocusBias);
        }

        return new Vector3(center.x + followOffset.x, center.y + followOffset.y, GetCameraZ());
    }

    private float GetLockZoom()
    {
        Bounds bounds = GetFocusBounds();
        float aspect = controlledCamera.aspect > 0f ? controlledCamera.aspect : 16f / 9f;
        float widthSize = (bounds.size.x + hitFocusPadding * 2f) / (2f * aspect);
        float heightSize = (bounds.size.y + hitFocusPadding * 2f) * 0.5f;
        float fitSize = Mathf.Max(widthSize, heightSize);
        float maxZoom = Mathf.Max(maxLockZoom, baseOrthographicSize);
        return Mathf.Clamp(Mathf.Max(singleTargetZoom, fitSize), minLockZoom, maxZoom);
    }

    private Bounds GetFocusBounds()
    {
        Vector3 start = player != null ? player.position : transform.position;
        Bounds bounds = new(start, Vector3.zero);

        foreach (LockTarget target in hitTargets)
        {
            bounds.Encapsulate(GetTargetPosition(target));
        }

        return bounds;
    }

    private static Vector3 GetTargetPosition(LockTarget target)
    {
        return target.Transform != null ? target.Transform.position : target.Position;
    }

    private float GetCameraZ()
    {
        return player != null ? player.position.z + followOffset.z : basePosition.z;
    }

    private void OnValidate()
    {
        followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
        lockSmoothTime = Mathf.Max(0.01f, lockSmoothTime);
        zoomSmoothTime = Mathf.Max(0.01f, zoomSmoothTime);
        lockDuration = Mathf.Max(0.05f, lockDuration);
        targetMemoryDuration = Mathf.Max(lockDuration, targetMemoryDuration);
        minLockZoom = Mathf.Max(0.5f, minLockZoom);
        maxLockZoom = Mathf.Max(minLockZoom, maxLockZoom);
        hitFocusPadding = Mathf.Max(0.1f, hitFocusPadding);
        singleTargetZoom = Mathf.Clamp(singleTargetZoom, minLockZoom, maxLockZoom);
    }
}
