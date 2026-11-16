using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "DungeonCore/Gunner Weapon Data")]
public class GunnerWeaponData : ScriptableObject
{
    /// <summary>
    /// 무기 한 종류의 기본 전투 설정을 보관하는 데이터형 스크립트
    /// PlayerGunController에서 선택한 무기의 설정을 읽고 발사에 사용
    /// </summary>

    [Header("무기 구분")]       // 기존 무기 전환에서 사용하는 enum을 가져옵니다
    [SerializeField] GunnerWeaponType weaponType;
    public GunnerWeaponType WeaponType => weaponType;


    [Header("발사 방식")]       // true면 좌클릭 유지 중 반복 발사 실행
    [SerializeField] bool automatic;
    public bool Automatic => automatic;


    [Header("기본 전투 수치")]        
    [SerializeField, Min(0f)] float damage = 20f;               // Ray 한발의 피해량, 샷건은 명중한 산탄만큼 피해량 적용
    public float Damage => damage;

    [FormerlySerializedAs("frieInterval")]
    [SerializeField, Min(0.01f)] float fireInterval = 0.3f;     // 성공한 발사 이후 다음 발사까지 기다리는 기본 시간
    public float FireInterval => fireInterval;

    [SerializeField, Min(0.1f)] float range = 50f;              // 총구에서 피해 판정을 검사할 최대 거리
    public float Range => range;


    [Header("탄창과 재장전")]
    [SerializeField, Min(1)] int magazineSize = 12;             // 강화가 적용되기 전의 최대 탄창
    public int MagazineSize => magazineSize;
    [SerializeField, Min(0.01f)] float reloadDur = 1.5f;        // 공통 재장전 기본 소요 시간
    public float ReloadDuration => reloadDur;


    [Header("산탄 설정")]
    [SerializeField, Min(1)] int pelletCount = 1;               // 한 번 발사할 때 검사할 Ray 개수
    public int PelletCount => pelletCount;
    [SerializeField, Range(0f, 45f)] float spreadAngle;         // 중앙 발사 방향에서 퍼질 수 있는 산탄 최대 각도
    public float SpreadAngle => spreadAngle;

}
