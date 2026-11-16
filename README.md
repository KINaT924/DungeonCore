# DungeonCore (던전코어)

제한 시간 동안 던전의 코어를 지키며 성장하고, 포탈 너머의 보스 **몽마퀸**을 쓰러뜨리는 3인칭 슈팅 액션 게임입니다.
Unity 6로 8주 동안 1인 개발한 포트폴리오 프로젝트입니다.

**▶ 웹에서 플레이:** https://<GitHub 아이디>.github.io/<저장소 이름>/
> **PC 전용** — 키보드와 마우스로 조작합니다. 모바일·태블릿은 지원하지 않습니다.
> 크롬·엣지 최신 버전, 전체화면 플레이를 권장합니다. 첫 접속은 데이터 다운로드로 시간이 걸릴 수 있습니다.

<!-- 스크린샷/플레이 영상: docs/images/ 에 넣고 아래 경로를 바꾸세요 -->
<!-- ![일반전](docs/images/ingame.png) ![보스전](docs/images/boss.png) -->

---

## 게임 흐름

```
타이틀 → 로딩 → 일반전(120초) → 보스 포탈 → 로딩 → 보스전(몽마퀸) → 클리어 / 패배
                                                         └ 추가 도전: 적 체력·공격력 1.1배로 다시 시작
```

- **일반전:** 몰려오는 좀비·스켈레톤을 처치하고 코어를 부숴 경험치를 모읍니다. 레벨업마다 성장 카드 3장 중 하나를 고릅니다.
- **보스전:** 일반전에서 얻은 성장(카드·경험치·체력)을 그대로 이어받아 몽마퀸과 싸웁니다. 파이어볼과 낙석 패턴을 피하며 공격합니다.
- **결과:** 클리어하면 난이도를 올려 추가 도전, 패배하면 처음부터 다시 시작합니다.

## 조작

| 키 | 동작 |
|---|---|
| W A S D | 이동 |
| 마우스 | 시점 회전 |
| 마우스 우클릭 | 조준 (숄더 뷰) |
| 마우스 좌클릭 | 사격 |
| 1 / 2 / 3 | 권총 / 라이플 / 샷건 |
| R | 현재 무기의 스킬 |
| Left Shift | 회피 |
| Esc | 마우스 잠금 해제 (게임 화면 클릭 시 다시 잠김) |

## 주요 구현

| 기능 | 내용 | 관련 코드 |
|---|---|---|
| 몬스터 AI (FSM) | 추적·공격·피격·사망을 상태로 나누고, 상태 전환 시 진입/종료 처리를 분리 | `02.Scripts/Enemy/EnemyController.cs` |
| 오브젝트 풀링 | 몬스터 리젠과 경험치 알갱이를 재사용해 생성·파괴 비용과 GC 부담 감소 | `EnemySpawner.cs`, `ExperiencePool.cs` |
| 데이터 분리 (ScriptableObject) | 성장 카드 9종·무기 3종·미니맵 레이아웃을 에셋으로 분리해 코드 수정 없이 밸런스 조정 | `02.Scripts/Growth/`, `05.Data/` |
| 거너 3무기 + 무기별 스킬 | 권총(코어 파괴탄)·라이플(오버드라이브 연사)·샷건(집중 산탄), 무기별 탄창·쿨타임 | `PlayerGunController.cs`, `GunnerWeaponSkillController.cs` |
| 게임 흐름 / 씬 전환 | 로딩 씬 경유 비동기 로딩, 목적지 전달, 결과 화면 중복 방지 | `02.Scripts/Game/`, `LoadingSceneController.cs` |
| 성장 상태 이월 | 씬 참조 없는 스냅샷으로 카드·경험치·체력을 보스전으로 전달 (레벨업 보상 재지급 없이 복원) | `PlayerGrowthSnapshot.cs`, `PlayerRunState.cs` |
| 보스 AI | 애니메이션 상태 진입/종료 이벤트와 연동한 시전·발사, 낙석 착탄 예약 | `BossQueenController.cs`, `BossQueenAnimationDriver.cs` |
| 위협 경고 UI | 화면 밖 적 방향 원호, 낙석 범위 진입 시 조기 경고 | `ThreatWarningUI.cs`, `ThreatArcGraphic.cs` |
| 에디터 자동화 도구 | 맵·UI·FX 구성 도구, 백업/복원과 중복 실행 방지 포함 | `Assets/Editor/` |
| 사운드 설정 | AudioMixer(Master/BGM/SFX) 노출 파라미터 + dB 변환, 설정값 저장 후 모든 씬에 자동 적용 | `AudioVolumeSettings.cs`, `VolumeSettingsPanel.cs` |
| WebGL 대응 | 브라우저 마우스 잠금 정책 대응(클릭 재잠금), 압축 해제 폴백, 웹 전용 텍스처 크기 제한, 커스텀 웹 템플릿, 웹에서 종료 버튼 숨김 | `PlayerCameraController.cs`, `TitleSceneController.cs`, `Assets/WebGLTemplates/` |

## 기술 스택

Unity 6 (6000.3) · C# · URP · Input System · Cinemachine 3 · AI Navigation(NavMesh) · TextMesh Pro · WebGL

## 폴더 구조

```
Assets/
├ 01.Scenes      타이틀 / 로딩 / 인게임 / 보스 씬
├ 02.Scripts     게임 코드 (Player, Enemy, Game, Growth, UI, Combat, Core, Input)
├ 03.Art         애니메이션, 모델, 프리팹, FX, UI, 폰트, 머티리얼
├ 04.Audio       BGM / SFX / Mixer
├ 05.Data        성장 카드·무기·미니맵 데이터(ScriptableObject)
├ 06.Settings    URP·입력 설정
├ 99.Assets      외부 에셋 (일부는 라이선스상 저장소 미포함)
└ Editor         제작·검증용 에디터 도구
```

## 프로젝트 열기

- Unity **6000.3.8f1**로 엽니다.
- Unity Asset Store 유료·무료 에셋(Synty POLYGON Dungeon, PROTOFACTOR Ultimate Animation Collection, Kevin Iglesias Human Animations, Explosive LLC RPG Character Pack)은 **라이선스상 저장소에 포함하지 않았습니다.** 해당 에셋 없이 열면 맵·애니메이션 일부가 비어 보입니다.
- 완성된 결과는 위의 **웹 플레이 링크**에서 확인할 수 있습니다.

## Credits

사용한 외부 에셋·폰트·음원과 라이선스는 [CREDITS.md](CREDITS.md)에 정리했습니다.

Title music: "Horror Game Menu" by Eric Matyas — www.soundimage.org
