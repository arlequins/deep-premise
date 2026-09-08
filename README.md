# Unseen Order

개발 저장소: **Deep Premise**

하나의 살아 있는 세계를 제한된 위치에서 오래 관찰하고, 중요한 순간에는 직접 개입하는 자율 시뮬레이션 게임입니다.

## 현재 상태

**Windows 개발 환경으로 이동하기 위한 미완성 중간 저장입니다. 실행 가능한 프로토타입이나 릴리스가 아닙니다.**

- 엔진: Godot **4.7.2 stable**, GDScript, 네이티브 2D.
- 목표: Windows x64 독립 실행형 클라이언트. 웹뷰와 Electron을 사용하지 않습니다.
- 시뮬레이션, 로컬 저장, 도시 지도, 로컬 자동화 연결의 초기 코드를 작성했습니다.
- `game/main.tscn`이 참조하는 `game/main.gd`는 아직 작성하지 않았습니다. 현재 프로젝트를 실행하면 게임 화면이 열리지 않습니다.
- Godot 버전의 컴파일·동작·Windows 내보내기 검증은 아직 하지 않았습니다.
- 별도 게임 소개 사이트와 AWS 리소스는 만들지 않습니다. 향후 소개는 기존 Beat에 통합합니다.

## Windows에서 이어가기

1. 이 저장소의 `feature/prototype-v1` 브랜치를 내려받습니다.
2. Python 3를 설치한 뒤 저장소 폴더에서 다음 명령을 실행합니다.

```powershell
py -3 tools/setup_engine.py --templates
```

도구는 공식 배포 파일의 고정 SHA-256을 확인하고 엔진과 Windows 템플릿을 저장소 안의 `.tools`에 준비합니다. 엔진 경로는 `.tools/engine-path.txt`에 기록됩니다. Python은 개발 준비 도구이며 최종 게임의 실행 의존성으로 계획하지 않습니다.

```powershell
$engine = (Get-Content .tools/engine-path.txt -Raw).Trim()
& $engine --editor --path .
```

**이 명령은 미완성 프로젝트를 편집기로 여는 용도입니다.** 구현과 검증을 마친 뒤 Windows 빌드를 준비합니다.

다음 작업자는 먼저 [개발 인계 문서](docs/HANDOFF.md)를 읽어 주세요. 세계의 숨은 설정은 사용자에게 설명하지 않습니다.

## 라이선스

게임 코드와 자체 콘텐츠의 사용 권한은 별도로 부여하지 않았습니다. 포함된 Godot 엔진 라이선스와 Noto Sans KR 글꼴 라이선스는 각각 `game/assets/GODOT-LICENSE.txt`, `game/assets/OFL.txt`에 있습니다.
