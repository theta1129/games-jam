using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [SerializeField] private HorizontalLayoutGroup healthGroup;
    [SerializeField] private List<Sprite> healthSprites;
    public List<ColorType> pattern { get; private set; }

    [SerializeField] private EnemyData testenemydata;
    [SerializeField] private SpriteRenderer healthPointPrefab;
    [Header("Hit reactions")]
    [SerializeField] private float blueStunDuration = 0.35f;
    [SerializeField] private float yellowStunDuration = 0.9f;
    [SerializeField] private float redShakeIntensity = 0.35f;
    [SerializeField] private float blueShakeIntensity = 0.16f;
    [SerializeField] private float yellowShakeIntensity = 0.2f;
    private List<SpriteRenderer> healthPoints = new();
    private EnemyMovement movement;
    private EnemyAttack attack;

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        if (movement == null) movement = gameObject.AddComponent<EnemyMovement>();
        attack = GetComponent<EnemyAttack>();
        if (attack == null) attack = gameObject.AddComponent<EnemyAttack>();
    }

    void OnEnable()
    {
        Setup(testenemydata);
    }

    public void Setup(EnemyData enemyData)
    {
        pattern = new(enemyData.pattern);
        UpdateHealth();
    }


    private void Update()
    {
        movement.Tick(Player.Instance);
        attack.Tick(Player.Instance, !movement.IsStunned);
    }

    public bool OnHit(ColorType colorType, Vector2 sourcePosition, float knockbackForce)
    {
        if (pattern == null || pattern.Count == 0) return false;
        if (pattern[0] == colorType)
        {
            pattern.Remove(colorType);
            if (knockbackForce > 0f) movement.KnockBack(sourcePosition, knockbackForce);
            HitBurstVfx.Spawn(transform.position, colorType);
            HitFlash flash = GetComponent<HitFlash>() ?? gameObject.AddComponent<HitFlash>();
            flash.Flash(Color.white, 0.08f);
            PlayHitReaction(colorType);
            UpdateHealth();
            if (pattern.Count == 0)
            {
                Death();
            }
            return true;
        }

        return false;
    }

    public void Stun(float duration) => movement.Stun(duration);

    private void PlayHitReaction(ColorType colorType)
    {
        switch (colorType)
        {
            case ColorType.Red:
                ShakeCamera(redShakeIntensity);
                HitStop(0.08f);
                break;
            case ColorType.Blue:
                Stun(blueStunDuration);
                ShakeCamera(blueShakeIntensity);
                HitStop(0.06f);
                break;
            case ColorType.Yellow:
                Stun(yellowStunDuration);
                ShakeCamera(yellowShakeIntensity);
                HitStop(0.06f);
                break;
        }
    }

    private static void ShakeCamera(float intensity)
    {
        Camera gameCamera = Camera.main;
        if (gameCamera == null) return;
        CameraShake shake = gameCamera.GetComponent<CameraShake>() ?? gameCamera.gameObject.AddComponent<CameraShake>();
        shake.ShakeScreen(0.18f, intensity);
    }

    private static void HitStop(float duration)
    {
        Stop.Pause(duration);
    }

    private IEnumerator Attack()
    {
        yield break;
    }

    private void Death()
    {
        Destroy(gameObject);
    }

    private void UpdateHealth()
    {
        foreach (var h in healthPoints) Destroy(h.gameObject);
        healthPoints.Clear();
        foreach (var color in pattern)
        {
            SpriteRenderer hP = Instantiate(healthPointPrefab, healthGroup.transform);
            hP.sprite = healthSprites[(int)color];
            healthPoints.Add(hP);
        }
    }
}
