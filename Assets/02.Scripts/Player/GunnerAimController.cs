using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;


[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class GunnerAimController : MonoBehaviour
{
    /// <summary>
    /// 우클릭 조준 기능 상태와 화면 표시 전용 카메라 관리 스크립트
    /// </summary>

    [Header("레퍼런스")]
    [SerializeField] PlayerGunController gun;
    [SerializeField] PlayerDodgeController dodge;
    [SerializeField] Health health;
    [SerializeField] Animator animator;
    [SerializeField] Camera referenceCamera;

    [Header("표시 전용 참조")]
    [SerializeField] Camera viewCamera;
    [SerializeField] Transform shoulderAnchor;

    [Header("화면 전환")]
    [SerializeField, Range(20f, 70f)] float aimFieldOfView = 42f;
    [SerializeField, Min(0.01f)] float blendSeconds = 0.18f;

    [Header("표시 카메라의 벽 관통 방지")]
    [SerializeField] LayerMask camaraCollisionMask = 65;
    [SerializeField, Min(0.01f)] float cameraRadius = 0.15f;

    float blend;
    bool routing;
    bool originalCameraEnabled;
    string originalTag, viewTag;

    // 입력을 기다리지 않고 현재 우클릭고 행동 상태를 즉시 계산하여 코드상 실행 순서와 무관하게 조건을 사용한 계산을 실행
    // 회피, 피격, 장착, 재장전, 사망, 카드 선택 중에는 조준 불가
    public bool IsAiming => isActiveAndEnabled
        && gun != null && gun.enabled && !gun.IsReloadAnimActive
        && health != null && !health.IsDead
        && dodge != null && !dodge.IsDodging
        && animator != null && !animator.GetBool("FullBodyAction")
        && Time.timeScale > 0f
        && Cursor.lockState == CursorLockMode.Locked
        && Mouse.current != null && Mouse.current.rightButton.isPressed;


    private void Awake()
    {
        if (gun == null)
            gun = GetComponent<PlayerGunController>();
        if(dodge ==null)
            dodge = GetComponent<PlayerDodgeController>();
        if (health == null)
            health = GetComponent<Health>();
    }

    // 판정 카메라는 렌더링만 끈체 표시 카메라로 출력
    // Camare.main을 사용하는 다른 컴포넌트가 표시 카메라를 찾도록태그도 임시로 교체
    private void OnEnable()
    {
        if(referenceCamera == null || viewCamera == null || shoulderAnchor == null || 
            referenceCamera == viewCamera || gun == null)
        {
            Debug.LogError("GunnerAimController의 참조 연결값을 확읺세요", this);
            enabled = false;
            return;
        }
        originalCameraEnabled = referenceCamera.enabled;
        originalTag = referenceCamera.tag;
        viewTag = viewCamera.tag;
        referenceCamera.enabled = false;
        viewCamera.tag = "MainCamera";
        viewCamera.transform.SetPositionAndRotation(referenceCamera.transform.position, referenceCamera.transform.rotation);
        viewCamera.fieldOfView = referenceCamera.fieldOfView;
        viewCamera.enabled = true;
        blend = 0f;
        routing = true;
    }

    // 카메라 컨트롤러, 시네머신이 기준 시점을 계산한 뒤 표시 시점만 갱신
    // 비조준 상태에서는 기존 시점을 그대로 출력하고 조준 중에는 어깨 위치로 시점을 변경하여 확대
    // 일시정지중에는 시점을 그대로 유지
    private void LateUpdate()
    {
        if (!routing || Time.timeScale <= 0f)
            return;
        blend = Mathf.MoveTowards(blend, IsAiming ? 1f : 0f, Time.deltaTime / blendSeconds);
        float amount = Mathf.SmoothStep(0f, 1f, blend);
        Vector3 basePosition = referenceCamera.transform.position;
        Quaternion baseRotation = referenceCamera.transform.rotation;
        Vector3 aimPoint = gun.GetAimPoint();
        Vector3 shoulderPosition = GetSafeShoulderPosition();

        if (Vector3.Dot(aimPoint - shoulderPosition, referenceCamera.transform.forward) < 0.25f)
            shoulderPosition = basePosition;

        Vector3 position = Vector3.Lerp(basePosition, shoulderPosition, amount);
        Quaternion rotation = baseRotation;
        if (amount > 0.0001f && (aimPoint - position).sqrMagnitude > 0.0001f)
            rotation = Quaternion.LookRotation(aimPoint - position, referenceCamera.transform.up);

        viewCamera.transform.SetPositionAndRotation(position, rotation);
        viewCamera.fieldOfView = Mathf.Lerp(referenceCamera.fieldOfView, aimFieldOfView, amount);
    }

    // 플레이어의 카메라 기준점에서 어깨 위치까지 벽 충돌을 검사
    // 표시 카메라 위치만 제한하며 총알Ray,사거리,목표점에는 영향을 주지않습니다
    Vector3 GetSafeShoulderPosition()
    {
        Vector3 origin = shoulderAnchor.parent.position;
        Vector3 offset = shoulderAnchor.position - origin;
        float distance = offset.magnitude;
        if (distance > .001f && Physics.SphereCast(origin, cameraRadius, offset / distance,
            out RaycastHit hit, distance, camaraCollisionMask, QueryTriggerInteraction.Ignore))
            return origin + offset.normalized * Mathf.Max(0f, hit.distance - .03f);
        return shoulderAnchor.position;
    }

    // 컴포넌트를 끄거나 플레이를 종료할 때 원래 카메라 출력과 태그로 복원
    void OnDisable()
    {
        if (!routing) 
            return;
        if (viewCamera != null)
        {
            viewCamera.enabled = false;
            viewCamera.tag = viewTag;
        }

        if (referenceCamera != null)
        {
            referenceCamera.tag = originalTag;
            referenceCamera.enabled = originalCameraEnabled;
        }
        routing = false;
    }
}
