using System;
using UnityEngine;


/// <summary>
/// 포탈 외형의 등장을 관리하는 스크립트
/// </summary>


[RequireComponent(typeof(BoxCollider))]
public class BossPortalEntrance : MonoBehaviour
{
    [SerializeField] Transform visualRoot;

    BoxCollider entrance;
    Health targetPlayer;
    Vector3 originalScale;
    bool armed;
    bool entered;

    public event Action OnEntered;


    // 생성 직후에 연출이 끝날 때 까진 진입 판정을 off
    private void Awake()
    {
        entrance = GetComponent<BoxCollider>();
        entrance.isTrigger = true;
        entrance.enabled = false;

        if(visualRoot == null || visualRoot == transform)
        {
            Debug.LogError("보스 포탈 별도의 연결참조를 확인", this);
            enabled = false;
            return;
        }

        originalScale = visualRoot.localScale;
        SetOpening(0f);
    }


    // 포탈이 등장할 때 스케일을 0~1을 받아 외형 확대
    public void SetOpening(float progress)
    {
        if (visualRoot == null)
            return;

        float scale = Mathf.Lerp(0.03f, 1f, Mathf.Clamp01(progress));

        visualRoot.localScale = originalScale * scale;
    }

    // 등장 완료 후 플레이어만 진입이 가능하게 설정
    public void Arm(Health player)
    {
        if (!enabled || player == null)
            return;

        targetPlayer = player;
        armed = true;
        entrance.enabled = true;
    }

    // 동록된 플레이어의 콜라이더가 감지되면 이벤트를 한번 전달
    private void OnTriggerEnter(Collider other)
    {
        if (!armed || entered || Time.timeScale <= 0f)
            return;

        Health health = other.GetComponentInParent<Health>();

        if (health != targetPlayer || health.IsDead)
            return;

        entered = true;
        entrance.enabled = false;
        OnEntered?.Invoke();
    }
}
