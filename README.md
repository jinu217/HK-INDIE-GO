# HK-INDIE-GO

한경대학교 2026 창업 동아리

---

# 개발 환경

| 항목 | 버전 |
|------|------|
| Unity | **6000.3.18f1** |
| IDE | **Visual Studio 2026** |
| Git | **GitHub Desktop** |

---

# 폴더 구조

```text
HK-INDIE-GO/
├── .git/
├── .gitignore
├── README.md
├── unity/
│   └── INDIE-GO/
├── builds/
│   └── .gitkeep
└── steam/
```

- **unity** : Unity 프로젝트
- **builds** : 빌드 결과물 저장 폴더 (Git에서는 폴더만 관리)
- **steam** : Steam SDK 및 Steam 관련 파일

---

# 브랜치 규칙

- `main` 브랜치는 **절대 직접 작업하거나 Commit / Push 하지 않습니다.**
- 모든 개발은 **`dev` 브랜치에서 새로운 브랜치를 생성**하여 진행합니다.
- 브랜치 이름은 반드시 **`dev/...`** 형식을 사용합니다.

### 예시

```text
dev/player
dev/ui
dev/game-manager
dev/tutorial
dev/bugfix
```

---

# GitHub Desktop 사용 규칙

1. 작업 전 **Current Branch**가 `main`이 아닌지 확인합니다.
2. **Current Branch**를 `dev`로 변경합니다.
3. **Fetch origin → Pull origin**을 실행하여 최신 내용을 받아옵니다.
4. `dev`에서 **New Branch**를 생성하여 작업합니다.
5. 작업이 끝나면 **Commit → Push origin**을 진행합니다.
6. GitHub에서 **Pull Request**를 생성하여 **dev 브랜치로 Merge**합니다.
7. Merge가 완료된 작업 브랜치는 삭제합니다. (권장)

> **main 브랜치에는 직접 Commit하거나 Push하지 않습니다.**

---

# Unity 프로젝트 위치

Unity Hub에서는 아래 폴더를 프로젝트로 등록합니다.

```text
unity/
└── INDIE-GO/
```

---

# Git 관리 규칙

### Git에 포함되는 폴더

```text
Assets/
Packages/
ProjectSettings/
steam/
```

### Git에서 제외되는 폴더

```text
Library/
Temp/
Logs/
UserSettings/
builds/
```

> `builds` 폴더는 Git에서 관리하지만, **빌드 결과물은 관리하지 않습니다.**
> (`.gitkeep` 파일만 포함됩니다.)

---

# Scripts 폴더 안내

`Assets/Scripts/` 내부 폴더는 프로젝트 진행에 따라 자유롭게 생성 및 수정할 수 있습니다.

예시

```text
Scripts/
├── Managers/
├── Player/
├── UI/
├── Core/
└── Utility/
```

위 예시는 참고용이며, 프로젝트 규모에 따라 필요한 폴더를 자유롭게 추가하여 사용합니다.

단, **역할이 명확하도록 폴더를 구성**해 주세요.

---

# 커밋 메시지 규칙

아래 형식을 권장합니다.

```text
feat: 새로운 기능 추가
fix: 버그 수정
refactor: 코드 리팩토링
docs: 문서 수정
style: 코드 스타일 수정
chore: 프로젝트 설정 변경
```

### 예시

```text
feat: 플레이어 이동 구현
fix: 윷 던지기 버그 수정
refactor: GameManager 구조 개선
docs: README 수정
```

---

# 기타

- 프로젝트 폴더 구조는 팀원과 상의 없이 변경하지 않습니다.
- `Assets` 내부 폴더 구조는 프로젝트 진행에 따라 자유롭게 추가할 수 있지만, 기존 구조 변경은 팀원과 상의 후 진행합니다.
- `.gitignore`는 임의로 수정하지 않습니다.
- 새로운 Unity Package 또는 외부 라이브러리 추가 전에는 팀원과 먼저 공유합니다.
- 빌드 결과물은 `builds/` 폴더에 저장하며 Git에는 포함하지 않습니다.
- Steam 관련 파일은 `steam/` 폴더에서 관리합니다.
- 개발 중 문제가 발생하거나 프로젝트 설정이 변경되는 경우 반드시 팀원에게 공유합니다.