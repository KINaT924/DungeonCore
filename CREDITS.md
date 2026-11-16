# Credits & Third-Party Assets

DungeonCore는 Unity 6(6000.3) 기반 개인 포트폴리오 프로젝트입니다. 게임 코드, 제작 도구(Editor 스크립트), 맵 구성, UI·FX 구성은 직접 작성했습니다.
아래 외부 에셋은 각 라이선스에 따라 사용했습니다.

## 저장소 포함 여부 요약

| 구분 | 저장소 포함 | 이유 |
|---|---|---|
| CC0 에셋 (KayKit, Quaternius) | 포함 가능 | 퍼블릭 도메인(CC0) |
| Unity Asset Store 에셋 | **미포함** (.gitignore) | Standard Unity Asset Store EULA는 원본 에셋 재배포를 허용하지 않음 |
| Synty 에셋 | **미포함** (.gitignore) | Synty 라이선스상 소스 파일 외부 공유 불가 |
| 폰트 (여기어때 잘난체) | 포함 가능 | 라이선스 전문 또는 출처 표기와 함께 재배포 허용 |

미포함 에셋은 아래 표의 링크에서 직접 받아 `Assets/99.Assets/` 아래 같은 경로에 임포트하면 프로젝트를 열 수 있습니다.
WebGL 빌드(플레이 링크)에는 모든 에셋이 게임 데이터로 포함되어 있습니다.

## 3D 모델 · 애니메이션

| 에셋 | 제작자 | 라이선스 | 프로젝트 경로 | 저장소 |
|---|---|---|---|---|
| POLYGON Dungeon Pack (+ Polygon Generic) | Synty Studios | Unity Asset Store 구매 · Standard Unity Asset Store EULA | `Assets/99.Assets/Synty` | 미포함 |
| Ultimate Animation Collection (2Handed Gun / Assault Rifle / Shotgun Animset) | PROTOFACTOR, INC | Standard Unity Asset Store EULA | `Assets/99.Assets/PROTOFACTOR` | 미포함 |
| Human Animations | Kevin Iglesias | Unity Asset Store 구매 · Standard Unity Asset Store EULA | `Assets/99.Assets/Kevin Iglesias` | 미포함 |
| RPG Character Mecanim Animation Pack FREE | Explosive LLC | Standard Unity Asset Store EULA | `Assets/99.Assets/ExplosiveLLC` | 미포함 |
| KayKit – Character Pack: Adventurers | Kay Lousberg | CC0 1.0 | `Assets/99.Assets/kaykit_character_pack_adventures` | 포함 |
| KayKit – Character Animations | Kay Lousberg | CC0 1.0 | `Assets/99.Assets/kaykit_Anim` | 포함 |
| Zombie Apocalypse Kit | Quaternius | CC0 1.0 | `Assets/99.Assets/Zombie Apocalypse Kit - March 2024` | 포함 |
| 보스 '몽마퀸' 3D 모델 | 직접 제작 (ChatGPT 컨셉 이미지 → Tripo AI 3D 생성 → Unity 리깅·애니메이션 연결) | Tripo 유료 요금제로 생성 (생성물 사용 권한 보유) | `Assets/03.Art/Models/BossQueen` | 포함 |

## 폰트

| 폰트 | 제작자 | 라이선스 | 경로 |
|---|---|---|---|
| 여기어때 잘난체 (Jalnan) | ㈜여기어때컴퍼니 (2026년 8월 다운로드) | 무료 사용, 출처 표기 권장, 폰트 단독 판매·개작 재배포 금지 | `Assets/03.Art/Fonts/Jalnan.ttf`, `DungeonUI SDF.asset` |
| DC SDF | 인터넷에 무료 공개된 폰트로 제작한 TMP 폰트 에셋 | 공개 무료 폰트 | `Assets/03.Art/Fonts/DC SDF.asset` |
| Liberation Sans | Red Hat (TextMesh Pro 기본 포함) | SIL OFL 1.1 | `Assets/TextMesh Pro` |

이 프로젝트에는 ㈜여기어때컴퍼니가 제공한 여기어때 잘난체가 적용되어 있습니다.

## 오디오

### 배경음악 (BGM)

| 사용 위치 | 곡 | 제작자 | 출처 | 라이선스 |
|---|---|---|---|---|
| 타이틀 (`04.Audio/BGM/Title.mp3`) | Horror Game Menu | Eric Matyas | Soundimage.org | 무료 사용, **출처 표기 필수** |
| 일반전 (`04.Audio/BGM/InGame.wav`) | Determined Pursuit | Emma_MA | OpenGameArt.org | CC0 1.0 |
| 보스전 (`04.Audio/BGM/Boss.wav`) | Epic Boss Battle | Juhani Junkala | OpenGameArt.org | CC0 1.0 |

Title music: "Horror Game Menu" by Eric Matyas — www.soundimage.org

### 효과음

| 파일 | 출처 |
|---|---|
| `04.Audio/SFX/gunshot_19.wav`, `GemBreak.ogg`, `SkeletonDead.wav`, `ZombieDead.wav` | AI 생성 및 무료 공개 음원 |
| `03.Art/Prefabs/Boss/Battle/QueenClear.wav` | 보스 전투 구성 도구로 생성 |
| `04.Audio/BGM/BGM.ogg` (이전 BGM, 현재 미사용) | AI 생성 및 무료 공개 음원 |

## 이미지 (직접 제작 / AI 생성)

| 대상 | 제작 방식 |
|---|---|
| 타이틀·로딩·결과 배경, 성장 카드 아이콘·프레임, 스킬 아이콘, HUD 아트 | ChatGPT 이미지 생성 후 직접 편집 |
| 보스 컨셉 아트 | ChatGPT 이미지 생성 |
| 포탈·보스 아레나·전투 FX 메시/머티리얼 | 직접 제작 (Unity Editor 스크립트로 생성) |

## 엔진 · 패키지

Unity 6 (6000.3.8f1), Universal Render Pipeline 17.3, Cinemachine 3.1, Input System 1.18, AI Navigation 2.0, TextMesh Pro, Build Report Tool (개발용, 빌드 미포함).
