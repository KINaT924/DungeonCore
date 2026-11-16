using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    /// <summary>
    /// 체력이 존재하는 오브젝트 혹은 캐릭터라면 필요한 체력 관리 컴포넌트
    /// 현재 체력을 저장 및 감소하거나 0이 되었을 시 사망 처리를 실행합니다
    /// </summary>

    [Header("레퍼런스")]
    [SerializeField] PlayerDodgeController dodgeCtr;                 //  회피컨트롤러 컴포넌트 참조 변수

    [Header("체력 설정")]
    [SerializeField] float maxHealth = 100f;        // 대상의 최대 체력
    float currentHealth;                            // 현재 체력
    float maxHealthBonus;                           // 플레이중 추가된 Hp최대치
    float challengeMultiplier = 1f;                 // 생성 시 한 번 확정합니다. 풀에서 ResetHealth를 반복해도 배율을 다시 곱하지 않습니다.
    bool isDead;                                    // 대상의 사망여부

    // 외부에서의  체력, 사망을 직접 변경하는걸 막는 용도의 프로퍼티
    public float MaxHealth => maxHealth * challengeMultiplier + maxHealthBonus;
    public float CurrentHealth { get { return currentHealth; } }
    public bool IsDead { get { return isDead; } }

    // 현재 체력이 변경되었다면 호출되는 이벤트
    // <변경된 현재 체력, 최대 체력> 의 값을 가지고 있습니다
    public event Action<float, float> OnHealthChanged;
    // 피해를 받았지만 살아있는 경우 호출되는 이벤트
    public event Action<float> OnDamaged;
    // 대상이 사망한 순간에 호출되는 이벤트
    public event Action OnDied;

    void Awake()
    {
        // 일반 몬스터와 보스만 강화하며 플레이어와 코어의 체력은 기존 값을 유지합니다.
        if (GetComponent<EnemyController>() != null || GetComponent<BossQueenController>() != null)
            challengeMultiplier = ChallengeRunState.EnemyMultiplier;
        if(dodgeCtr == null)
            dodgeCtr = GetComponent<PlayerDodgeController>();

        InitializeHealth();
    }

    // 게임이 시작 될 때 현재 체력을 최대 체력으로 초기화
    void InitializeHealth()
    {
        ResetHealth();
    }

    // IDamageable 인터페이스로부터 정의한 피해 처리 메소드
    // 전달받은 피해량 만큼 체력이 감소하는 역할을 담당

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;
        if (dodgeCtr != null && dodgeCtr.IsDodging)
            return;

        // 0 이하의 피해가 들어온다면 변경 x
        if (damage <= 0f)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);

        Debug.Log($"{gameObject.name}이 {damage}의 피해를 받았습니다. 현재 체력 : {currentHealth}");

        // 체력 변경 사실을 전달
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);

        // 체력이 0이되면 피격 모션을 재생하지않고 바로 사망처리
        if (currentHealth <= 0f)
            Die();
        // 아직 살아 있다면 피격 반응을 전달
        else
            OnDamaged?.Invoke(damage);
    }


    // 현재 체력이 0이 되면 사망 상태로 전환하는 메소드
    void Die()
    {
        if (isDead)
            return;

        isDead = true;


        Debug.Log($"{gameObject.name}이 사망하였습니다");

        OnDied?.Invoke();
    }

    // 사망 혹은 비활성화 된 대상이 다시 사용할 수 있도록
    // 체력과 상태를 리셋하는 메소드
    public void ResetHealth()
    {
        currentHealth = MaxHealth;
        isDead = false;

        // UI가 존재하는 경우 최대 체력으로 돌아간 사실을 전달
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    // 최대 체력 카드를 선택하였을 때 호출되는 메소드
    public bool TryIncreaseMaxHealth(float amount)
    {
        if (isDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            return false;

        maxHealthBonus += amount;

        // 최대 체력과 현재 체력을 같은 양만큼 증가
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, MaxHealth);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);

        Debug.Log($"[체력강화] 현재 체력 : {currentHealth}/{MaxHealth}", this);

        return true;
    }

    // 보스전 이동 직전에 플레이어의 체력 상태를 저장
    public void CaptureSnapshot(PlayerGrowthSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        // 체력 상태 저장
        snapshot.CurrentHealth = currentHealth;
        snapshot.MaxHealthBonus = maxHealthBonus;
    }

    // 저장된 상태를 복원
    public bool RestoreSnapshot(PlayerGrowthSnapshot snapshot)
    {
        if (snapshot == null || snapshot.CurrentHealth <= 0f || snapshot.MaxHealthBonus < 0f)
            return false;

        // 추가량을 더하지 않고 저장되어있던 값을 교체
        maxHealthBonus = snapshot.MaxHealthBonus;

        // 최대 체력 강화량을 먼저 복원시켜 현재체력의 범위를 맞추도록 설정
        currentHealth = Mathf.Clamp(snapshot.CurrentHealth, 0f, MaxHealth);
        isDead = false;

        // 피격 or 사망 이벤트 없이 체력 표시만 갱신
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);

        return true;
    }
}
