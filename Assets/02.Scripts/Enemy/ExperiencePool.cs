using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ExperiencePool : MonoBehaviour
{
    /// <summary>
    /// 경험치 알갱이의 생성과 재사용을 담당합니다.
    /// 알갱이가 스스로 비활성화하면 반환된 것으로 취급하며 다음 요청에서 재사용합니다.
    /// </summary>

    [SerializeField] ExperiencePickup expPrefab;        // 경험치 알갱이 프리팹
    [SerializeField] PlayerExperience playerExp;        // 경험치를 받을 플레이어
    [SerializeField] Health health;                     // 지급 가능한 생존 상태인지
    [SerializeField] Transform absorbPoint;             // 흡수 위치
    readonly List<ExperiencePickup> pickups = new List<ExperiencePickup>();

    public void Spawn(Vector3 pos, int amount)
    {
        if (amount <= 0 || !gameObject.activeInHierarchy)
            return;
        if (expPrefab == null || playerExp == null || health == null || absorbPoint == null)
        {
            Debug.LogError("ExperiencePool의 참조 연결을 확인하세요", this);
            return;
        }
        if (health.IsDead || !playerExp.gameObject.activeInHierarchy)
            return;

        ExperiencePickup selected = null;
        // 비활성 경험치는 반환된 개체이므로 재사용 x
        foreach (ExperiencePickup pickup in pickups)
        {
            if (pickup == null || pickup.gameObject.activeSelf)
                continue;
            selected = pickup;
            break;
        }

        // 재사용 개체가 없을때 새로 생성하고 목록에 등록
        if(selected == null)
        {
            selected = Instantiate(expPrefab, pos, Quaternion.identity, transform);
            pickups.Add(selected);
        }
        // 현재 파일에서 사용하는 변수 일므과 매개변수 순서에 맞춰 전달
        selected.Initialize(playerExp, health, absorbPoint, amount, pos);
    }

    [ContextMenu("테스트 스폰")]
    void TestSpawn()
    {
        if (!Application.isPlaying)
            return;
        for (int i = 0; i < 3; i++)
            Spawn(transform.position + Vector3.right * i * 0.3f, 10);
    }
}
