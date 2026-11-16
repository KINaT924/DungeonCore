using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// 포탈의 위치, 입력, 잠금, 등장 연출을 관리하는 스크립트
/// </summary>

public class BossPortalPresentation : MonoBehaviour
{
    [Header("레퍼런스")]
    [SerializeField] DungeonRunController run;
    [SerializeField] Health playerHealth;
    [SerializeField] PlayerInput playerInput;
    [SerializeField] PlayerCameraController cameraController;
    [SerializeField] GrowthCardSelectionController cards;

    [Header("포탈 관련")]
    [SerializeField] BossPortalEntrance portalPrefab;
    [SerializeField] CanvasGroup announcement;

    [Header("연출 관련")]
    [SerializeField, Min(0.1f)] float openingSeconds = 1.5f;
    [SerializeField, Min(0f)] float messageHoldSeconds = 3f;
    [SerializeField] LayerMask obstacleMask = 65;

    readonly List<InputAction> restoreActions = new List<InputAction>();

    BossPortalEntrance portal;
    bool requested;
    bool ownsPause;
    bool previousCameraEnabled;
    float previousTimeScale;

    // 생성 요청
    private void OnEnable()
    {
        if (run != null)
            run.OnPortalRequested += HandleRequest;

        if(announcement != null)
        {
            announcement.alpha = 0f;
            announcement.interactable = false;
            announcement.blocksRaycasts = false;
        }
    }

    // 필수참조 확인
    private void Start()
    {
        if (run == null || playerHealth == null || playerInput == null || cameraController == null || cards == null || portalPrefab == null || announcement == null)
        {
            Debug.LogError("BosPortalPresentation의 참조연결 확인", this);
            enabled = false;
            return;
        }

        // 연출 관리자가 혹시나 나중에 활성화 됬을 때를 대비
        if (run.Phase == DungeonRunController.RunPhase.AwaitingPortalPresentation)
        {
            HandleRequest();
        }
    }

    // 포탈 요청을 한번 실행
    void HandleRequest()
    {
        if (requested)
            return;

        requested = true;
        StartCoroutine(PresentPortal());
    }

    // 카드 선택 종료와 안전한 위치를 확인 한 뒤 게임을 일시 정지
    // 포탈은 비배율 시간을 재생하고 연출 완료 후 일시 정지 해제
    IEnumerator PresentPortal()
    {
        yield return null;

        Vector3 pos = Vector3.zero;

        while(true)
        {
            if (playerHealth == null || playerHealth.IsDead)
                yield break;

            bool cardBusy = cards.IsSelecting || cards.PendingSelections > 0;

            if(Time.timeScale > 0f && !cardBusy && TryFindPortalPosition(out pos))
            {
                break;
            }

            // 앞에 장애물이 있다면 강제로 배치하지않도록 게임을 일시정지 x
            // 플레이어가 이동하며 포탈이 안전한곳에 생성되도록 위치를 다시 찾습니다
            yield return new WaitForSecondsRealtime(0.25f);
        }

        Vector3 facing = playerHealth.transform.position - pos;
        facing.y = 0f;

        if (facing.sqrMagnitude < 0.001f)
            facing = -playerHealth.transform.forward;

        AcquirePause();

        // 포탈 생성
        portal = Instantiate(portalPrefab, pos, Quaternion.LookRotation(facing));

        portal.OnEntered += HandleEntered;

        // 기존의 게임 입자는 멈추되 포탈 등장 입자만 계속 재생
        SetPortalUnscaled(true);
        announcement.alpha = 1f;

        float elapsed = 0f;

        while(elapsed < openingSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / openingSeconds));

            portal.SetOpening(progress);
            yield return null;
        }

        portal.SetOpening(1f);

        // 등장 이후 카드 선택 등 일반 일시정지와 함께 정지
        SetPortalUnscaled(false);

        run.NotifyPortalReady();
        portal.Arm(playerHealth);
        ReleasePause();

        yield return new WaitForSeconds(messageHoldSeconds);

        elapsed = 0f;
        const float fadeSeconds = 0.4f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.deltaTime;
            announcement.alpha = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
            yield return null;
        }
        announcement.alpha = 0f;
    }

    // 플레이어 전방 2~3m앞에서 이동가능한 포탈 생성용 공간을 찾는 메소드
    bool TryFindPortalPosition(out Vector3 pos)
    {
        pos = Vector3.zero;

        Transform player = playerHealth.transform;

        if(!NavMesh.SamplePosition(player.position, out NavMeshHit start, 1f, NavMesh.AllAreas))
        {
            return false;
        }

        Vector3 forward = player.forward;
        forward.y = 0f;
        forward.Normalize();

        float[] angles = { 0f, -25f, -50f, 50f };
        float[] distances = { 2.5f, 2f, 3f };

        foreach ( float angle in angles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;

            foreach (float dis in distances)
            {
                Vector3 candidate = player.position + dir * dis;

                if(!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.65f, NavMesh.AllAreas))
                {
                    continue;
                }

                if (Mathf.Abs(hit.position.y - start.position.y) > 0.7f)
                    continue;

                if(NavMesh.Raycast(start.position, hit.position, out _, NavMesh.AllAreas))
                {
                    continue;
                }

                Quaternion rot = Quaternion.LookRotation(-dir);

                // 바닥에서 조금 띄워진 박스로 포탈 체크
                bool blocked = Physics.CheckBox(hit.position + Vector3.up * 1.05f, new Vector3(0.85f, 1f, 0.3f),
                                                                        rot, obstacleMask, QueryTriggerInteraction.Ignore);

                if (blocked)
                    continue;

                pos = hit.position;
                return true;
            }
        }
        return false;
    }

    // 현재 활성된 게임 액션과 카멜 상태를 잠금
    void AcquirePause()
    {
        if (ownsPause)
            return;

        ownsPause = true;
        previousTimeScale = Time.timeScale;
        previousCameraEnabled = cameraController.enabled;

        restoreActions.Clear();

        var map = playerInput.currentActionMap;

        if(map != null)
        {
            foreach ( var action in map.actions)
            {
                if(action.enabled)
                    restoreActions.Add(action);
            }
            map.Disable();
        }
        cameraController.enabled = false;
        Time.timeScale = 0f;
    }


    void ReleasePause()
    {
        if (!ownsPause)
            return;

        ownsPause = false;
        Time.timeScale = previousTimeScale;

        if (cameraController != null)
            cameraController.enabled = previousCameraEnabled;

        foreach (var action in restoreActions)
            action.Enable();

        restoreActions.Clear();
    }

    // 포탈 입자만 일시정지 중 재생할지 설정하는 메소드
    void SetPortalUnscaled(bool value)
    {
        if (portal == null)
            return;

        foreach (var ps in portal.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.useUnscaledTime = value;
        }
    }

    // 실제 포탈에 진입할 때 진행 관리자에게 전달
    void HandleEntered()
    {
        run.NotifyPortaEntered();
    }

    // 해당 1라운드 씬이 종료 시 이벤트와 연출을 정리
    private void OnDisable()
    {
        if (run != null)
            run.OnPortalRequested -= HandleRequest;

        StopAllCoroutines();
        ReleasePause();

        if (announcement != null)
            announcement.alpha = 0f;

        if(portal != null)
        {
            portal.OnEntered -= HandleEntered; ;
            Destroy(portal.gameObject);
        }

        requested = false;
    }

    // 동적으로 생성된 공용 HUD의 포탈 안내를 연결하고 초기 숨김 상태를 설정
    public void BindAnnouncement(CanvasGroup target)
    {
        announcement = target;
        if (announcement == null) return;
        announcement.alpha = 0f;
        announcement.interactable = false;
        announcement.blocksRaycasts = false;
    }
}
