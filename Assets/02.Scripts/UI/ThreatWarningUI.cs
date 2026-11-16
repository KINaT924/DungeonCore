using TMPro;
using UnityEngine;

/// <summary>
/// 공용 HUD의 사각지대,낙석 경고를 표시합니다
/// 공격 판정은 읽기만 하며 이동, 피해, 조준에는 관여하지 않습니다
/// GameHudView.Bind에서 현재 씬을 받고, 줌을 포함한 실제 카메라 기준으로 매 프레임 방향을 갱신합니다
/// </summary>

[DisallowMultipleComponent]
public sealed class ThreatWarningUI : MonoBehaviour
{
    [Header("프리팹 내부 연결")]
    public ThreatArcGraphic arc;
    public CanvasGroup rockNotice;
    public TMP_Text rockText;

    [Header("경고 표시")]
    [Min(.1f)] public float blinkHz = 1.5f;
    [Range(0f, 1f)] public float dimAlpha = .55f;
    GameUISpawner context;
    Health health;
    Camera viewCamera;
    float pulseTime;

    // HUD가 활성화되기 전에 현재 씬의 플레이어와 카메라를 전달받고 잔여 표시를 초기화
    public void Bind(GameUISpawner source)
    {
        context = source;
        health = source.player.GetComponent<Health>();
        viewCamera = source.uiCamera;
        pulseTime = 0;
        Hide();
    }

    // 카메라 이동 후 위험을 읽습니다
    // 카드,포탈 일시정지, 전투 종료와 사망 중에는 경고를 숨깁니다
    void LateUpdate()
    {
        if (context == null || health == null || health.IsDead || !health.gameObject.activeInHierarchy
            || context.Result != GameUISpawner.BattleResult.None || Time.timeScale <= 0f)
        {
            Hide();
            return;
        }
        // 씬의 실제 카메라가 늦게 활성화된 경우에만 다시 구합니다
        // 줌 FOV는 이 카메라의 현재 값을 사용합니다
        if (viewCamera == null || !viewCamera.isActiveAndEnabled) 
            viewCamera = Camera.main;

        arc.ClearDirections();
        bool rockDanger = false;

        if (context.mode == GameUISpawner.BattleMode.Boss)
        {
            var rocks = BossAttackProjectile.PendingRocks;
            for (int i = 0; i < rocks.Count; i++)
            {
                if (rocks[i] != null && rocks[i].Threatens(health))
                { 
                    rockDanger = true;
                    break;
                }
            }

            if (rockDanger)
                arc.AddDirection(90f);
        }
        else if (viewCamera != null)
        {
            var enemies = EnemyController.ActiveThreats;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAttackThreatTo(health))
                    continue;

                Vector3 eyePoint = enemy.transform.position + Vector3.up;
                Vector3 viewport = viewCamera.WorldToViewportPoint(eyePoint);

                // 현재 줌 화각 밖에 있는 근접 적만 경고합니다
                // 화면 안의 적은 기존 외형으로 확인합니다
                if (viewport.z > viewCamera.nearClipPlane && viewport.x >= 0 && viewport.x <= 1
                    && viewport.y >= 0 && viewport.y <= 1)
                    continue;
                Vector3 delta = enemy.transform.position - health.transform.position;
                delta.y = 0;

                Vector3 forward = viewCamera.transform.forward; forward.y = 0;
                Vector3 right = viewCamera.transform.right; right.y = 0;

                if (delta.sqrMagnitude < .0001f)
                    arc.AddDirection(-90f);
                else 
                    arc.AddDirection(90f - Mathf.Atan2(Vector3.Dot(delta, right.normalized),
                                        Vector3.Dot(delta, forward.normalized)) * Mathf.Rad2Deg);
            }
        }
        pulseTime += Time.deltaTime;
        float pulse = Mathf.Lerp(dimAlpha, 1f, .5f + .5f * Mathf.Sin(pulseTime * blinkHz * Mathf.PI * 2f));
        arc.Refresh(pulse);

        rockNotice.alpha = rockDanger ? pulse : 0f;
        rockNotice.interactable = rockNotice.blocksRaycasts = false;
    }

    // 상태 전환과 비활성화 때 남아 있는 표시를 즉시 숨깁니다
    void Hide()
    {
        if (arc != null)
        { 
            arc.ClearDirections();
            arc.Refresh(0f); 
        }

        if (rockNotice != null) 
            rockNotice.alpha = 0;
    }

    // 씬 전환과 HUD 비활성화 때 이전 플레이어의 경고가 남지 않도록 호출합
    void OnDisable() 
    {
        Hide();
    }
}
