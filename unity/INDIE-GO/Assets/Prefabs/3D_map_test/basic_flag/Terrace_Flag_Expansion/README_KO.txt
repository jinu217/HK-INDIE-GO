궁전 테라스 — 깃발 및 장식 확장팩

이전 Terrace_Detailed 모델에는 깃발과 깃대가 없었습니다.
이번 파일에는 사진을 참고해 만든 깃발 5개와 설치 부품, 중앙 장식을 추가했습니다.
기존 맵의 크기·원점·바닥 높이를 기준으로 배치한 실제 3D 메시입니다.

1. 가장 간단하게 사용하는 파일

Terrace_With_Flags.obj
  기존 맵과 추가 오브젝트를 모두 합친 배치 완료본입니다.
  이 파일과 같은 이름의 .mtl 및 textures 폴더를 함께 유지하세요.
  이전 맵 대신 이 통합본을 사용하면 됩니다.

Terrace_Additions_Placed.obj
  추가한 깃발·깃대·중앙 금색 장식만 포함합니다.
  이전 Terrace_Detailed.obj와 같은 원점, 축, 크기로 내보냈습니다.
  기존 맵과 동일한 부모 아래에 두고 Position / Rotation / Scale 및
  모델 임포트 스케일을 기존 맵과 같게 설정하세요.
  통합본과 이 추가본을 동시에 배치하면 깃발이 중복됩니다.

Single_*.obj
  5개의 깃발을 각각 다른 곳에 배치할 수 있는 독립 에셋입니다.
  바닥 설치형은 받침 바닥 중심, 중앙 벽걸이형은 벽의 부착 지점이 원점입니다.
  배치 원본 좌표는 placement_manifest.json에 기록했습니다.
  JSON의 좌표는 OBJ의 Y-up 기준입니다. 엔진의 축 변환 설정에 따라
  표시 좌표가 달라질 수 있으므로 원래 위치 복원에는 Placed 파일이 간단합니다.

Terrace_With_Flags.blend
  개별 천, 깃대, 고리, 장식과 제작용 모디파이어를 보존한 편집 원본입니다.
  텍스처를 내부에 포함했으며, 별도 PNG도 textures 폴더에 제공합니다.
  게임 카메라, 전체 시점, 정탑뷰, 앞·뒤 확대 카메라를 포함합니다.

2. 이번에 추가한 것

- 좌우 메인 청색 깃발 2개: 서로 다른 주름, 좌우 방향에 맞춘 지지대.
- 뒤쪽 청록색·청색 보조 깃발 2개: 난간과 화분 뒤에 배치.
- 중앙 벽걸이 청색 깃발 1개: 기존 중앙 석조 장식 앞에 설치.
- 금색 창끝 장식, 깃대, 가로봉, 고리, 고정 탭, 끈과 술.
- 대리석 받침, 금색 테두리 및 중앙의 휘어진 금색 장식 4개.

중앙의 기존 단단한 청색 방패판은 통합본에서 천 깃발로 교체했습니다.
추가본만 쓰면 이전 방패판은 그 뒤에 남습니다. 사진에 더 가까운 교체 상태는
통합본에 반영되어 있습니다. 원래 난간·화분·꽃·덩굴·바닥은 유지했습니다.
배경 도시는 2D 이미지이며 OBJ 메시에는 들어 있지 않습니다.

3. 형태와 배치 기준

중앙 보드용 평면: 10 × 10, OBJ 기준 높이 Y=0.004.
맵 받침 두께: 0.19. 기존 단층 구조를 유지했습니다.
메인 깃발 천: 너비 1.12, 내려오는 길이 2.90.
보조 깃발 천: 너비 0.76, 내려오는 길이 2.25.
중앙 깃발 천: 너비 0.82, 내려오는 길이 1.88.
천 두께: 0.006. 앞면·뒷면·옆면을 가진 메시입니다.

천은 실제 굴곡이 있는 메시이며 끝부분이 뾰족한 형태입니다.
가장자리의 가느다란 금색 마감도 주름을 따라 움직이는 형상으로 만들었습니다.
게임 카메라에서 보이는 부분을 우선하면서 옆면과 뒷면도 닫힌 형태로 제작했습니다.
사진 밖으로 잘린 부분과 가려진 설치 구조는 기존 맵 비례에 맞춰 재구성했습니다.
따라서 원본 사진을 측량한 1:1 복원은 아닙니다.

4. 텍스처와 Unity 재질

banner_atlas.png
  1254 × 1254. 왼쪽은 청색, 오른쪽은 청록색 천과 금색 문양입니다.
  이미 메시 UV가 연결되어 있으므로 두 색을 따로 잘라 사용할 필요가 없습니다.
  Banner_Embroidered_Silk 재질의 Base Map / Albedo에 연결합니다.
  색상 곱은 흰색, Tiling (1,1), Offset (0,0)을 기준으로 만들었습니다.

banner_metallic.png / banner_roughness.png
  금색 자수와 천의 반사 특성을 구분하는 일반 PBR 데이터 맵입니다.
  색상 이미지가 아닌 데이터이므로 선형 값으로 취급합니다.

banner_unity_metallic_smoothness.png
  R: Metallic, A: Smoothness. Unity URP Lit의 Metallic 워크플로용입니다.
  Base Map이 아니라 Metallic Map에 연결합니다.
  Smoothness Source는 Metallic Alpha, Smoothness 배율은 1을 시작값으로 사용하세요.
  텍스처의 sRGB 옵션은 끕니다. 이 파일의 알파는 투명도가 아닙니다.
  천 재질의 Surface Type은 Opaque로 사용하세요.
  HDRP Mask Map용 채널 구성과는 다릅니다.

OBJ의 MTL에는 색상, Metallic, Roughness 연결을 기록했습니다.
엔진의 MTL 가져오기 방식에 따라 PBR 항목이 자동 연결되지 않을 수 있으므로
위 PNG를 이용해 프로젝트의 렌더 파이프라인에 맞는 재질에 연결하세요.
Blender의 천 Sheen과 미세한 절차적 직물 Bump는 MTL로 동일하게 전달되지 않습니다.
금속·천의 최종 밝기는 프로젝트 조명, 환경 반사, 노출에 따라 조정해야 합니다.

Unity 6.0 URP Lit 공식 참고:
https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/built-in/shaders-in-universalrp/reference/lit-shader

5. 렌더 미리보기

renders/01_game_camera.png       기존 맵 기준의 게임 카메라 + 2D 배경
renders/02_full_overview.png     깃대 꼭대기까지 보이는 전체 사선 시점
renders/03_top_clearance.png     정탑뷰의 배치 확인
renders/04_central_banner.png    중앙 깃발과 석조 장식 확대
renders/05_banner_front.png      메인 깃발 앞면
renders/06_banner_back.png       메인 깃발 뒷면과 지지 구조

이 미리보기는 제공하는 3D 장면을 Blender에서 렌더링한 결과입니다.
컨셉 이미지를 완성 메시처럼 표시한 것이 아닙니다.
게임 카메라 이미지는 원본 사진처럼 일부 깃대 꼭대기가 화면 밖으로 잘립니다.
전체 모델의 형태는 02_full_overview.png에서 확인할 수 있습니다.

6. 검증과 사용 범위

Blender 5.3의 OBJ 가져오기로 추가본, 통합본, 개별 깃발 5개를 다시 열어
UV·텍스처 파일·삼각형 메시·바닥 평탄성을 확인했습니다.
천의 양면이 닫혀 있는지, 천과 해당 깃대의 삼각형이 교차하는지도 확인했습니다.
보드 영역과 천의 전체 삼각형을 실제 월드 좌표 및 지정 게임 카메라의
화면 좌표에서 비교했습니다. 해당 카메라에서 천이 보드 영역을 침범하지 않습니다.
자유롭게 카메라를 돌릴 때까지 보드 가림이 없다는 뜻은 아닙니다.
상세 수치는 model_info.json, placement_manifest.json, validation.json에 있습니다.

추가본: 122,384 삼각형 / 통합본: 704,268 삼각형.
디테일을 우선한 원본 메시이며 타깃 기기의 성능 최적화까지 완료한 모델은 아닙니다.
Unity Editor에서의 실행 검증은 하지 않았습니다.
OBJ는 주름이 고정된 정적 메시입니다. 바람 애니메이션, 천 시뮬레이션,
게임 충돌체, LOD, Unity 프리팹 및 실시간 조명 설정은 포함하지 않습니다.

7. 제작 코드

source 폴더에는 이번 확장 제작·재질 맵 구성·내보내기·검증 코드를 포함했습니다.
기본 사용에는 실행할 필요가 없습니다. .blend를 바로 열어 수정할 수 있습니다.
전체를 다시 만들려면 이 폴더와 같은 상위 폴더에 이전 패키지의
Terrace_Detailed/Terrace_Detailed.blend가 필요합니다.
생성 순서: prepare_banner_materials.py → build_flag_expansion.py
→ finalize_flag_expansion.py → verify_flag_expansion.py.
Blender 스크립트는 Blender 5.3 기준, 재질 데이터 생성은 Python + Pillow + NumPy 기준입니다.
