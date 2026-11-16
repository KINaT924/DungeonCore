using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 이름과 실제 Health의 체력을 표시합니다
/// Health가 없으면 이름만 보이고 가짜 체력은 표시하지 않습니다
/// </summary>
public class BossHealthUI : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text valueText;
    public Slider healthSlider;
    public GameObject healthContent;
    Health health;

    //활성화 전 보스 참조를 지정, 이미 표시 중이면 이전 구독을 해제한 뒤 새 대상으로 갱신
    public void Bind(Health target, string displayName)
    {
        if (health != null) 
            health.OnHealthChanged -= Refresh;
        health = target; 

        nameText.text = displayName;
        healthContent.SetActive(health != null);

        if (health != null && isActiveAndEnabled)
        { 
            health.OnHealthChanged += Refresh;
            Refresh(health.CurrentHealth, health.MaxHealth); 
        }
    }

    // 보스전 HUD가 켜질 때만 체력 변경을 구독
    void OnEnable()
    {
        if (health != null)
        { 
            health.OnHealthChanged += Refresh;
            Refresh(health.CurrentHealth, health.MaxHealth);
        } 
    }

    // 모든 Awake가 끝난 뒤 초기 체력을 다시 갱신
    void Start()
    { 
        if (health != null)
            Refresh(health.CurrentHealth, health.MaxHealth);
    }

    // 씬 전환이나 HUD 비활성화 시 이전 보스의 이벤트 구독을 해제
    void OnDisable()
    { 
        if (health != null)
            health.OnHealthChanged -= Refresh;
    }

    // 현재/최대 체력을 게이지와 숫자로 변환합니다. 최대값 0도 안전하게 처리
    void Refresh(float current, float maximum)
    {
        healthSlider.SetValueWithoutNotify(maximum > 0 ? Mathf.Clamp01(current / maximum) : 0);
        valueText.text = $"{current:0} / {maximum:0}";
    }
}
