using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 실제 회피 컨트롤러의 남은 시간과 행동 상태를 읽어 준비/쿨타임/사망 표시를 갱신하는 스크립트
/// </summary>

[DefaultExecutionOrder(310)]
public class DodgeCooldownUI : MonoBehaviour
{
    public PlayerDodgeController dodge;
    public Health health;
    public TMP_Text stateText;
    public Image progressFill;
    public Image accent;

    public Color readyColor = new Color(.35f,.85f,.65f,1);
    public Color cooldownColor = new Color(.7f,.62f,.88f,1);
    public Color unavailableColor = new Color(.43f,.46f,.53f,1);

    // 새 회피가 시작됐을 때 기준 시간을 보관해 진행 중 강화가 적용돼도 막대가 갑자기 뛰지 않게 합니다.
    float cooldownDuration;
    float previousRemaining;
    int lastState = -1;
    int lastTenths = -1;

    // HUD를 다시 켜면 현재 회피 상태에서 표시를 재동기화
    void OnEnable() 
    { 
        lastState = lastTenths = -1;
        previousRemaining = cooldownDuration = 0f;
        RefreshNow(); 
    }

    // 기존 회피의 Update가 타이머를 갱신한 뒤 실제 남은 시간을 표시
    void LateUpdate() 
    {
        RefreshNow();
    }

    // 읽기 전용 상태를 UI로 변환
    // 쿨타임은 0.1초 단위로 올림해 아직 남았는데 0초로 보이는 일을 방지
    public void RefreshNow()
    {
        float remaining = dodge != null ? dodge.DodgeCooldownRemaining : 0f;

        if (remaining > previousRemaining+.01f || (remaining > 0f && cooldownDuration <= 0f))
            cooldownDuration = Mathf.Max(remaining,dodge.CurrentDodgeCooldown);
        previousRemaining = remaining;

        bool unavailable = dodge == null || !dodge.isActiveAndEnabled || (health != null && health.IsDead);
        int state = unavailable ? 0 : remaining > 0f ? 1 : dodge.IsDodging ? 2 : 3;
        Color tint = state == 3 ? readyColor : state == 0 ? unavailableColor : cooldownColor;

        if (progressFill != null)
        {
            progressFill.fillAmount = state == 0 ? 0 : remaining > 0 ? 1f-Mathf.Clamp01(remaining/Mathf.Max(.01f,cooldownDuration)) : 1f;
            progressFill.color = tint;
        }
        if (accent != null) 
            accent.color = tint;
        int tenths = Mathf.CeilToInt(remaining*10f);

        if (lastState == state && lastTenths == tenths)
            return;
        lastState = state; lastTenths = tenths;

        if (stateText == null)
            return;
            
        stateText.color = tint;
        stateText.text = state == 0 ? "—" : state == 1 ? $"{tenths/10f:0.0}s" : state == 2 ? "DODGING" : "READY";
    }
}
