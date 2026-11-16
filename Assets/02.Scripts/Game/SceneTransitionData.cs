using UnityEngine;

/// <summary>
/// 씬 이동할 때 요청하는 쪽과 로딩 씬의 사이에서 다음 목적지를 전달하는 스크립트
/// </summary>
public class SceneTransitionData : MonoBehaviour
{
    // 씬이 교체되어도 유지되는 다음 목적지
    static string nextScene;

    // Play를 시작할 때 이전 목적지를 제거
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        nextScene = null;
    }

    // 로딩씬으로 이동하기 직전에 호출 sceneName에 도착지를 입력해놔 최종 이동지로 설정
    public static void SetTarget(string sceneName)
    {
        nextScene = sceneName;
    }

    // 로딩 씬이 시작 될 때 목적지를 가져오고 저장된 값을 null
    // 전달된 요청이 없을 시 인게임으로 진입
    public static string ConsumeTarget(string fallbackScene)
    {
        string target = string.IsNullOrWhiteSpace(nextScene) ? fallbackScene : nextScene;
        nextScene = null;
        return target;
    }
}
