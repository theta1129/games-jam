using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CombatCameraController : MonoBehaviour
{
    public static CombatCameraController Instance
    {
        get;
        private set;
    }


    // =========================================================
    // Follow
    // =========================================================

    [Header("Follow")]

    [SerializeField]
    private Transform player;

    [SerializeField]
    private Vector3 followOffset =
        new Vector3(
            0f,
            0f,
            -10f
        );

    [SerializeField]
    private float followSmoothTime =
        0.14f;


    // =========================================================
    // Mouse Focus
    // =========================================================

    [Header("Mouse Focus")]

    [Tooltip(
        "평상시 플레이어에서 마우스 방향으로 " +
        "카메라가 얼마나 이동할지"
    )]
    [SerializeField]
    private float mouseFocusDistance =
        1.8f;


    [Tooltip(
        "마우스가 멀리 있을수록 포커스가 강해지는 최대 월드 거리"
    )]
    [SerializeField]
    private float mouseMaxWorldDistance =
        7f;


    [Tooltip(
        "마우스 방향 포커스가 카메라에 반영되는 비율"
    )]
    [SerializeField, Range(0f, 1f)]
    private float mouseFocusWeight =
        0.65f;


    [Tooltip(
        "마우스를 플레이어 근처에 뒀을 때 " +
        "카메라가 흔들리지 않도록 무시하는 거리"
    )]
    [SerializeField]
    private float mouseDeadZone =
        0.35f;


    [Tooltip(
        "전투 록온 중 마우스 포커스 영향도"
    )]
    [SerializeField, Range(0f, 1f)]
    private float combatMouseFocusWeight =
        0.30f;


    // =========================================================
    // Combat Lock
    // =========================================================

    [Header("Combat Lock")]

    [SerializeField]
    private float lockSmoothTime =
        0.09f;

    [SerializeField]
    private float zoomSmoothTime =
        0.12f;

    [Tooltip("마지막 타격 이후 록온 유지 시간")]
    [SerializeField]
    private float lockDuration =
        0.85f;


    [Tooltip(
        "이 시간 안에 연속으로 맞은 적들은 " +
        "같은 공격에 맞은 적으로 취급"
    )]
    [SerializeField]
    private float hitGroupWindow =
        0.30f;


    // =========================================================
    // Combat Focus
    // =========================================================

    [Header("Combat Focus")]

    [Tooltip(
        "0 = 플레이어 중심\n" +
        "1 = 맞은 적들의 평균 위치 중심"
    )]
    [SerializeField, Range(0f, 1f)]
    private float enemyFocusBias =
        0.58f;


    // =========================================================
    // Combat Zoom
    // =========================================================

    [Header("Combat Zoom")]

    [SerializeField]
    private float singleTargetZoom =
        3.75f;

    [SerializeField]
    private float minLockZoom =
        3.45f;

    [SerializeField]
    private float maxLockZoom =
        7f;

    [Tooltip("전투 대상 주변 화면 여백")]
    [SerializeField]
    private float hitFocusPadding =
        1.35f;

    [SerializeField]
    private float edgeSafetyPadding =
        0.35f;


    // =========================================================
    // Runtime
    // =========================================================

    private readonly List<LockTarget> hitTargets =
        new List<LockTarget>();


    private Camera controlledCamera;

    private CameraShake cameraShake;


    private Vector3 basePosition;

    private Vector3 positionVelocity;


    private float baseOrthographicSize;

    private float zoomVelocity;


    private float lockEndTime;

    private float lastHitRegisterTime =
        -999f;


    // =========================================================
    // Target
    // =========================================================

    private struct LockTarget
    {
        public Transform Transform;

        public Vector3 LastPosition;


        public LockTarget(
            Transform target
        )
        {
            Transform =
                target;


            LastPosition =
                target != null
                    ? target.position
                    : Vector3.zero;
        }
    }


    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        Instance =
            this;


        controlledCamera =
            GetComponent<Camera>();


        cameraShake =
            GetComponent<CameraShake>();


        basePosition =
            transform.position;


        baseOrthographicSize =
            controlledCamera.orthographicSize;
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance =
                null;
        }
    }


    // =========================================================
    // Late Update
    // =========================================================

    private void LateUpdate()
    {
        cameraShake ??=
            GetComponent<CameraShake>();


        EnsurePlayer();

        UpdateTargetPositions();


        bool locked =
            Time.unscaledTime <
            lockEndTime
            &&
            hitTargets.Count > 0;


        Vector3 desiredPosition;

        float desiredSize;


        if (locked)
        {
            desiredPosition =
                GetCombatFocusPosition();


            desiredSize =
                GetLockZoom(
                    desiredPosition
                );
        }
        else
        {
            desiredPosition =
                GetMouseFollowPosition();


            desiredSize =
                baseOrthographicSize;


            if (
                Time.unscaledTime >=
                lockEndTime
            )
            {
                hitTargets.Clear();
            }
        }


        // 슬로모션과 상관없이
        // 카메라는 실제 시간 기준으로 움직임
        float deltaTime =
            Mathf.Max(
                Time.unscaledDeltaTime,
                0.0001f
            );


        float smoothTime =
            locked
                ? lockSmoothTime
                : followSmoothTime;


        // =====================================================
        // Position
        // =====================================================

        basePosition =
            Vector3.SmoothDamp(
                basePosition,
                desiredPosition,
                ref positionVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime
            );


        // =====================================================
        // Zoom
        // =====================================================

        controlledCamera.orthographicSize =
            Mathf.SmoothDamp(
                controlledCamera.orthographicSize,
                desiredSize,
                ref zoomVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                deltaTime
            );


        // =====================================================
        // Shake
        // =====================================================

        Vector3 shakeOffset =
            cameraShake != null
                ? cameraShake.CurrentOffset
                : Vector3.zero;


        transform.position =
            basePosition +
            shakeOffset;
    }


    // =========================================================
    // Ensure
    // =========================================================

    public static CombatCameraController Ensure(
        Camera camera,
        Transform playerTransform
    )
    {
        if (camera == null)
        {
            return null;
        }


        CombatCameraController controller =
            camera.GetComponent<CombatCameraController>();


        if (controller == null)
        {
            controller =
                camera.gameObject
                    .AddComponent<CombatCameraController>();
        }


        controller.SetPlayer(
            playerTransform
        );


        return controller;
    }


    // =========================================================
    // Register Hit
    // =========================================================

    public static void RegisterHit(
        Transform hitTarget
    )
    {
        if (hitTarget == null)
        {
            return;
        }


        Transform playerTransform =
            Player.Instance != null
                ? Player.Instance.transform
                : null;


        CombatCameraController controller =
            Instance != null
                ? Instance
                : Ensure(
                    Camera.main,
                    playerTransform
                );


        if (controller == null)
        {
            return;
        }


        controller.SetPlayer(
            playerTransform
        );


        controller.AddHitTarget(
            hitTarget
        );
    }


    // =========================================================
    // Player
    // =========================================================

    public void SetPlayer(
        Transform playerTransform
    )
    {
        if (playerTransform == null)
        {
            return;
        }


        bool firstPlayer =
            player == null;


        player =
            playerTransform;


        if (firstPlayer)
        {
            Vector3 currentOffset =
                transform.position -
                player.position;


            followOffset =
                new Vector3(
                    currentOffset.x,
                    currentOffset.y,
                    currentOffset.z
                );


            basePosition =
                transform.position -
                (
                    cameraShake != null
                        ? cameraShake.CurrentOffset
                        : Vector3.zero
                );
        }


        if (
            controlledCamera != null &&
            baseOrthographicSize <= 0f
        )
        {
            baseOrthographicSize =
                controlledCamera.orthographicSize;
        }
    }


    private void EnsurePlayer()
    {
        if (player != null)
        {
            return;
        }


        if (Player.Instance != null)
        {
            SetPlayer(
                Player.Instance.transform
            );
        }
    }


    // =========================================================
    // 평상시 마우스 Follow
    // =========================================================

    private Vector3 GetMouseFollowPosition()
    {
        if (player == null)
        {
            return basePosition;
        }


        Vector3 mouseFocus =
            GetMouseFocusWorldPosition();


        // Player → Mouse Focus 사이
        Vector3 center =
            Vector3.Lerp(
                player.position,
                mouseFocus,
                mouseFocusWeight
            );


        return new Vector3(
            center.x +
            followOffset.x,

            center.y +
            followOffset.y,

            GetCameraZ()
        );
    }


    // =========================================================
    // 마우스 방향 포커스 계산
    // =========================================================

    private Vector3 GetMouseFocusWorldPosition()
    {
        if (
            player == null ||
            controlledCamera == null ||
            Mouse.current == null
        )
        {
            return
                player != null
                    ? player.position
                    : basePosition;
        }


        // =========================================
        // Screen → World
        // =========================================

        Vector2 mouseScreenPosition =
            Mouse.current.position.ReadValue();


        Vector3 mouseScreen =
            new Vector3(
                mouseScreenPosition.x,
                mouseScreenPosition.y,
                Mathf.Abs(
                    controlledCamera.transform.position.z -
                    player.position.z
                )
            );


        Vector3 mouseWorld =
            controlledCamera.ScreenToWorldPoint(
                mouseScreen
            );


        mouseWorld.z =
            player.position.z;


        // =========================================
        // Player → Mouse
        // =========================================

        Vector2 toMouse =
            (Vector2)(
                mouseWorld -
                player.position
            );


        float distance =
            toMouse.magnitude;


        // 플레이어 바로 근처에 마우스가 있으면
        // 카메라 포커스를 움직이지 않음
        if (
            distance <=
            mouseDeadZone
        )
        {
            return player.position;
        }


        Vector2 direction =
            toMouse.normalized;


        // =========================================
        // 마우스가 플레이어에서 얼마나 멀리 있냐에 따라
        // Focus 강도 변화
        // =========================================

        float normalizedDistance =
            Mathf.InverseLerp(
                mouseDeadZone,
                Mathf.Max(
                    mouseDeadZone + 0.01f,
                    mouseMaxWorldDistance
                ),
                distance
            );


        float focusDistance =
            mouseFocusDistance *
            normalizedDistance;


        Vector3 focusPosition =
            player.position +
            (Vector3)(
                direction *
                focusDistance
            );


        focusPosition.z =
            player.position.z;


        return focusPosition;
    }


    // =========================================================
    // Combat Focus
    // =========================================================

    private Vector3 GetCombatFocusPosition()
    {
        if (
            hitTargets.Count == 0
        )
        {
            return GetMouseFollowPosition();
        }


        // =========================================
        // 맞은 Enemy들의 평균
        // =========================================

        Vector3 enemyAverage =
            Vector3.zero;


        int count =
            0;


        foreach (
            LockTarget target
            in hitTargets
        )
        {
            enemyAverage +=
                GetTargetPosition(
                    target
                );


            count++;
        }


        if (count > 0)
        {
            enemyAverage /=
                count;
        }
        else if (player != null)
        {
            enemyAverage =
                player.position;
        }


        // =========================================
        // Player + Enemy 평균
        // =========================================

        Vector3 combatCenter;


        if (player != null)
        {
            combatCenter =
                Vector3.Lerp(
                    player.position,
                    enemyAverage,
                    enemyFocusBias
                );
        }
        else
        {
            combatCenter =
                enemyAverage;
        }


        // =========================================
        // 여기에 마우스 방향 포커스 추가
        // =========================================

        if (player != null)
        {
            Vector3 mouseFocus =
                GetMouseFocusWorldPosition();


            combatCenter =
                Vector3.Lerp(
                    combatCenter,
                    mouseFocus,
                    combatMouseFocusWeight
                );
        }


        return new Vector3(
            combatCenter.x +
            followOffset.x,

            combatCenter.y +
            followOffset.y,

            GetCameraZ()
        );
    }


    // =========================================================
    // Add Hit Target
    // =========================================================

    private void AddHitTarget(
        Transform target
    )
    {
        float now =
            Time.unscaledTime;


        // 이전 공격과 충분히 떨어져 있으면
        // 새 공격 그룹
        if (
            now -
            lastHitRegisterTime >
            hitGroupWindow
        )
        {
            hitTargets.Clear();
        }


        lastHitRegisterTime =
            now;


        // 이미 등록된 적
        for (
            int i = 0;
            i < hitTargets.Count;
            i++
        )
        {
            if (
                hitTargets[i].Transform ==
                target
            )
            {
                LockTarget updated =
                    hitTargets[i];


                updated.LastPosition =
                    target.position;


                hitTargets[i] =
                    updated;


                lockEndTime =
                    now +
                    lockDuration;


                return;
            }
        }


        // 새 적
        hitTargets.Add(
            new LockTarget(
                target
            )
        );


        lockEndTime =
            now +
            lockDuration;
    }


    // =========================================================
    // Enemy Position Memory
    // =========================================================

    private void UpdateTargetPositions()
    {
        for (
            int i = 0;
            i < hitTargets.Count;
            i++
        )
        {
            LockTarget target =
                hitTargets[i];


            if (
                target.Transform != null
            )
            {
                target.LastPosition =
                    target.Transform.position;


                hitTargets[i] =
                    target;
            }
        }
    }


    // =========================================================
    // Lock Zoom
    // =========================================================

    private float GetLockZoom(
        Vector3 desiredCameraPosition
    )
    {
        if (controlledCamera == null)
        {
            return baseOrthographicSize;
        }


        float aspect =
            controlledCamera.aspect > 0f
                ? controlledCamera.aspect
                : 16f / 9f;


        Vector2 cameraCenter =
            new Vector2(
                desiredCameraPosition.x,
                desiredCameraPosition.y
            );


        float maxHorizontalDistance =
            0f;

        float maxVerticalDistance =
            0f;


        // =========================================
        // Player
        // =========================================

        if (player != null)
        {
            IncludeFocusPoint(
                player.position,
                cameraCenter,
                ref maxHorizontalDistance,
                ref maxVerticalDistance
            );
        }


        // =========================================
        // 맞은 Enemy 모두
        // =========================================

        foreach (
            LockTarget target
            in hitTargets
        )
        {
            IncludeTargetBounds(
                target,
                cameraCenter,
                ref maxHorizontalDistance,
                ref maxVerticalDistance
            );
        }


        // =========================================
        // Mouse Focus도 화면 밖으로 너무 나가지 않게
        // 약하게 포함
        // =========================================

        if (player != null)
        {
            Vector3 mouseFocus =
                GetMouseFocusWorldPosition();


            IncludeFocusPoint(
                mouseFocus,
                cameraCenter,
                ref maxHorizontalDistance,
                ref maxVerticalDistance
            );
        }


        float padding =
            hitFocusPadding +
            edgeSafetyPadding;


        float verticalSize =
            maxVerticalDistance +
            padding;


        float horizontalSize =
            (
                maxHorizontalDistance +
                padding
            )
            /
            Mathf.Max(
                0.01f,
                aspect
            );


        float requiredSize =
            Mathf.Max(
                verticalSize,
                horizontalSize
            );


        if (
            hitTargets.Count ==
            1
        )
        {
            requiredSize =
                Mathf.Max(
                    requiredSize,
                    singleTargetZoom
                );
        }


        requiredSize =
            Mathf.Max(
                requiredSize,
                minLockZoom
            );


        // 맞은 적 전원이 화면에 들어오는 것을 우선
        float effectiveMaxZoom =
            Mathf.Max(
                maxLockZoom,
                requiredSize
            );


        return Mathf.Clamp(
            requiredSize,
            minLockZoom,
            effectiveMaxZoom
        );
    }


    // =========================================================
    // Focus Point
    // =========================================================

    private static void IncludeFocusPoint(
        Vector3 worldPosition,
        Vector2 cameraCenter,
        ref float maxHorizontalDistance,
        ref float maxVerticalDistance
    )
    {
        float horizontal =
            Mathf.Abs(
                worldPosition.x -
                cameraCenter.x
            );


        float vertical =
            Mathf.Abs(
                worldPosition.y -
                cameraCenter.y
            );


        maxHorizontalDistance =
            Mathf.Max(
                maxHorizontalDistance,
                horizontal
            );


        maxVerticalDistance =
            Mathf.Max(
                maxVerticalDistance,
                vertical
            );
    }


    // =========================================================
    // Enemy Bounds
    // =========================================================

    private static void IncludeTargetBounds(
        LockTarget target,
        Vector2 cameraCenter,
        ref float maxHorizontalDistance,
        ref float maxVerticalDistance
    )
    {
        if (
            target.Transform != null
        )
        {
            Collider2D[] colliders =
                target.Transform
                    .GetComponentsInChildren<Collider2D>(
                        true
                    );


            bool foundCollider =
                false;


            foreach (
                Collider2D collider
                in colliders
            )
            {
                if (
                    collider == null ||
                    !collider.enabled
                )
                {
                    continue;
                }


                foundCollider =
                    true;


                Bounds bounds =
                    collider.bounds;


                float left =
                    Mathf.Abs(
                        bounds.min.x -
                        cameraCenter.x
                    );


                float right =
                    Mathf.Abs(
                        bounds.max.x -
                        cameraCenter.x
                    );


                float bottom =
                    Mathf.Abs(
                        bounds.min.y -
                        cameraCenter.y
                    );


                float top =
                    Mathf.Abs(
                        bounds.max.y -
                        cameraCenter.y
                    );


                maxHorizontalDistance =
                    Mathf.Max(
                        maxHorizontalDistance,
                        left,
                        right
                    );


                maxVerticalDistance =
                    Mathf.Max(
                        maxVerticalDistance,
                        bottom,
                        top
                    );
            }


            if (foundCollider)
            {
                return;
            }
        }


        IncludeFocusPoint(
            GetTargetPosition(target),
            cameraCenter,
            ref maxHorizontalDistance,
            ref maxVerticalDistance
        );
    }


    // =========================================================
    // Target Position
    // =========================================================

    private static Vector3 GetTargetPosition(
        LockTarget target
    )
    {
        if (
            target.Transform != null
        )
        {
            return
                target.Transform.position;
        }


        return
            target.LastPosition;
    }


    // =========================================================
    // Camera Z
    // =========================================================

    private float GetCameraZ()
    {
        return
            player != null
                ? player.position.z +
                  followOffset.z
                : basePosition.z;
    }


    // =========================================================
    // Inspector
    // =========================================================

    private void OnValidate()
    {
        followSmoothTime =
            Mathf.Max(
                0.01f,
                followSmoothTime
            );


        lockSmoothTime =
            Mathf.Max(
                0.01f,
                lockSmoothTime
            );


        zoomSmoothTime =
            Mathf.Max(
                0.01f,
                zoomSmoothTime
            );


        mouseFocusDistance =
            Mathf.Max(
                0f,
                mouseFocusDistance
            );


        mouseMaxWorldDistance =
            Mathf.Max(
                0.1f,
                mouseMaxWorldDistance
            );


        mouseDeadZone =
            Mathf.Max(
                0f,
                mouseDeadZone
            );


        mouseFocusWeight =
            Mathf.Clamp01(
                mouseFocusWeight
            );


        combatMouseFocusWeight =
            Mathf.Clamp01(
                combatMouseFocusWeight
            );


        lockDuration =
            Mathf.Max(
                0.05f,
                lockDuration
            );


        hitGroupWindow =
            Mathf.Max(
                0.02f,
                hitGroupWindow
            );


        enemyFocusBias =
            Mathf.Clamp01(
                enemyFocusBias
            );


        minLockZoom =
            Mathf.Max(
                0.5f,
                minLockZoom
            );


        maxLockZoom =
            Mathf.Max(
                minLockZoom,
                maxLockZoom
            );


        singleTargetZoom =
            Mathf.Max(
                minLockZoom,
                singleTargetZoom
            );


        hitFocusPadding =
            Mathf.Max(
                0.1f,
                hitFocusPadding
            );


        edgeSafetyPadding =
            Mathf.Max(
                0f,
                edgeSafetyPadding
            );
    }
}