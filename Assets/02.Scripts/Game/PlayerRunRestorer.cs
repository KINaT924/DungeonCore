using UnityEngine;
using UnityEngine.Rendering;


/// <summary>
/// 보스전에서의 플레이어가 초기화 된 일반전에서의 저장 상태를 복원
/// 성장, 경험치, 체력, 총기 컴포넌트가 있는 동일한 오브젝트에 부착
/// </summary>

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class PlayerRunRestorer : MonoBehaviour
{
    // 일반적인 Start함수보다 먼저 실행하여 복원을 진행
    private void Start()
    {
        if (!PlayerRunState.HasSnapshot)
            return;

        if(!PlayerRunState.TryRestore(gameObject))
        {
            Debug.LogError("성장값을 불러오지 못했습니다", this);
        }
    }
}
