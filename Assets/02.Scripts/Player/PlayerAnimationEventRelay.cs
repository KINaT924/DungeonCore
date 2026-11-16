using UnityEngine;

public class PlayerAnimationEventRelay : MonoBehaviour
{
    /// <summary>
    /// 자식 캐릭터 모델의 애니메이터에서 발생한 애니메이션 이벤트를
    /// 부모 오브젝트의 컴포넌트로 전달하는 컴포넌트입니다
    /// </summary>

    [Header("애니메이션 전달 대상")]
    [SerializeField] PlayerCombat playerCombat;         // 기본공격의 판정 및 활성or비활성화, 공격 종료를 처리할 컴포넌트 참조 변수
    [SerializeField] PlayerSkillController skillCtr;    // 스킬 판정 활성화or비활성화, 스킬 종료를 처리할 컴포넌트 참조 변수


    private void Awake()
    {
        if (playerCombat == null)
            playerCombat = GetComponentInParent<PlayerCombat>();
        if(skillCtr == null)
            skillCtr = GetComponentInParent<PlayerSkillController>();
    }


    // 부모 컴포넌트에서 기본공격 판정을 활성화or비활성화하는 메소드들
    public void EnableAttackArea()
    {
        
        if(playerCombat != null)
            playerCombat.EnableAttackArea();
    }
    public void DisableAttackArea()
    {
        if(playerCombat != null)
            playerCombat.DisableAttackArea();
    }

    // 부모 컴포넌트에서 기본 공격 판정이 끝났을때 사용하는 호출 메소드
    public void EndAttack()
    {
        if(playerCombat != null)
            playerCombat.EndAttack();
    }

    // 부모 컴포넌트에서 스킬 판정 활성화or비활성화, 끝났을때 호출 메소드
    public void EnableSkillArea(int skillNum)
    {
        if(skillCtr != null)
            skillCtr.EnableSkillArea(skillNum);
    }
    public void DisableSkillArea(int skillNum)
    {
        if(skillCtr != null)
            skillCtr.DisableSkillArea(skillNum);
    }
    public void EndSkill(int skillNum)
    {
        if (skillCtr != null)
            skillCtr.EndSkill(skillNum);
    }
}
