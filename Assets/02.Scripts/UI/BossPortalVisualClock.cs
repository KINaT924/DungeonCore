using UnityEngine;

/// <summary>
/// 포탈 소용돌이의 시간만 제어하는 스크립트
/// 등장 중에는 비배율, 진입 준비 이후에는 게임 시간을 사용
/// </summary>
public sealed class BossPortalVisualClock : MonoBehaviour
{
    [SerializeField] Renderer vortex;
    [SerializeField] Collider entryTrigger;
    MaterialPropertyBlock properties;

    float elapsed;
    bool opened;
    static readonly int PortalTime = Shader.PropertyToID("_PortalTime");

    // 외형이 활성화될 때 시간을 초기화하고 머티리얼 복제 없이 값을 전달할 블록을 준비
    void OnEnable()
    {
        elapsed = 0f;
        opened = false;
        properties = new MaterialPropertyBlock();
    }

    // 기존 Arm 호출로 Trigger가 켜지면 등장 완료로 기억시키고
    // 진입 시 Trigger가 꺼져도 시간을 되돌리지 않습니다
    void LateUpdate()
    {
        if (vortex == null)
            return;
        if (entryTrigger != null && entryTrigger.enabled)
            opened = true;

        elapsed += opened ? Time.deltaTime : Time.unscaledDeltaTime;
        vortex.GetPropertyBlock(properties);
        properties.SetFloat(PortalTime, elapsed);
        vortex.SetPropertyBlock(properties);
    }
}
