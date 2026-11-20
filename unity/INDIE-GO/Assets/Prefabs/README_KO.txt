TERRACE BALANCED — 첫 모델 기반 / 깃발 없는 수정본

사용할 파일
  Terrace_Balanced_NoFlags.obj
  Terrace_Balanced_NoFlags.mtl
  textures/floor_basecolor.png
  textures/walkway_marble.png

OBJ 하나만 옮기지 말고 MTL과 textures 폴더를 같은 상대 경로로 함께 옮겨 주세요.
Blender 편집 파일, 렌더, source 폴더는 Unity에 함께 임포트하지 않아도 됩니다.

수정 내용
  바닥 외곽: 가로 19.5 × 세로 15 = 1.3:1
  직전 버전: 22 × 15. 가로를 2.5 줄였습니다.
  첫 버전: 14 × 15. 좌우를 각각 2.75 확장했습니다.
  중앙 보드 가이드: 10 × 10 정사각형, 첫 모델의 좌표·UV·텍스처 그대로.
  중앙 바닥 높이: OBJ Y = 0.004. 표면에 돌출된 문양이나 장애물이 없습니다.
  바닥 두께: 0.19. 난간·기둥 높이는 첫 모델과 같습니다.

배치 수정
  난간을 바깥으로 옮길 때 꽃, 화분, 덩굴, 기둥도 함께 이동했습니다.
  장식은 통로 중앙이 아니라 실제 난간선에 붙도록 정리했습니다.
  앞쪽 큰 화분 두 개는 전면 난간의 바깥 코너 가까이 재배치했습니다.
  화분과 기둥의 받침끼리 겹치지 않도록 확인했습니다.
  첫 모델의 꽃·화분·기둥·중앙 석조 문장은 유지했습니다.
  꽃/덩굴 장식 12묶음, 화분 6개, 기둥 8개가 있습니다.
  천 깃발, 깃대, 깃발용 지지 구조물은 포함하지 않습니다.
  중앙의 파란 방패는 첫 모델의 석조/에나멜 문장입니다. 천 깃발이 아닙니다.

비율 판단 기준
  사용자가 제시한 약 1.3:1을 바닥 외곽의 실제 길이에 적용했습니다.
  첫 모델과 같은 추정 게임 카메라에서 보드 가이드의 화면 위치를 유지했습니다.
  1672 × 941 기준, 화면 좌우 200픽셀 폭 / y=160 이하 영역을 5픽셀 간격으로
  검사했을 때 바닥으로 덮이는 비율은 100%였습니다.
  이는 동봉된 추정 카메라에 대한 검사입니다. 사용자 Unity 카메라의 FOV,
  위치, 화면비가 다르면 측면 배경이 보이는 범위도 달라집니다.
  사진만으로 실제 카메라 정보나 잘린 외곽 치수를 완전히 복원한 것은 아닙니다.

Unity 배치
  기존 메인 맵을 숨기거나 교체하고 새 OBJ를 배치하세요. 두 맵을 겹치지 마세요.
  첫 모델과 동일한 Import Scale과 루트 Transform을 사용하세요.
  중앙 보드 기준점은 그대로이므로 보드판 자체를 가로로 늘릴 필요가 없습니다.
  OBJ는 X 좌우, Y 높이입니다. Blender (x,y,z)는 OBJ (x,z,-y)로 내보냈습니다.
  같은 임포트 설정/부모 좌표계에서 기존 보드 배치를 유지하는 용도입니다.
  간단한 바닥 충돌체가 필요하면 BoxCollider를 별도로 추가할 수 있습니다.
  루트 기준 크기 (19.5, 0.19, 15), 중심 (0, -0.095, 0.5)입니다.
  장식의 복잡한 MeshCollider는 이 패키지에 자동으로 설정하지 않았습니다.

색상/텍스처가 흰색으로 보일 때
  Reference_Floor에는 textures/floor_basecolor.png를 연결하세요.
  Carved_Ivory_Marble도 같은 이미지의 UV 일부를 사용합니다.
  Walkway_Ivory_Marble에는 textures/walkway_marble.png를 연결하세요.
  각 머티리얼의 Base Color 곱셈 색상은 흰색으로 놓으세요.
  PNG는 색상용(sRGB)입니다. 중앙 바닥을 Normal Map으로 임포트하지 마세요.
  프로젝트의 렌더 파이프라인과 맞는 Lit/Standard 셰이더를 사용하세요.

선택 사항: 머티리얼/Prefab 생성 보조 스크립트
  Unity/Editor/TerraceBalancedMaterials.cs를 Assets 아래의 Editor 폴더에 복사합니다.
  OBJ 폴더에 material_manifest.json과 textures 폴더도 함께 둡니다.
  Project 창에서 Terrace_Balanced_NoFlags.obj를 선택한 후
  Tools > Terrace > Create materials and prefab 메뉴를 실행합니다.
  현재 파이프라인에 맞는 머티리얼과 새 Prefab을 생성합니다.
  기존 씬과 기존 머티리얼은 변경하지 않습니다. 새 Prefab을 씬에 배치하세요.
  이 스크립트는 제공된 OBJ에만 적용되며 Unity Editor에서 실행 검증하지 않았습니다.
  참고 API: https://docs.unity3d.com/ScriptReference/PrefabUtility.SaveAsPrefabAsset.html

기존 별도 깃발
  깃발은 이번 OBJ에 넣지 않았습니다.
  첫 모델에 맞춘 좌/우 메인 깃발은 맵 루트 좌표계 X 방향으로 각각 -2.75 / +2.75
  이동하면 새 측면 난간 위치와 맞출 수 있습니다. 깃발을 가로로 늘리지 마세요.
  별도 파일 flag_placement_reference.json에 원래 앵커와 수정 앵커를 기록했습니다.
  임포터 축 변환이나 부모 Transform이 있으면 해당 좌표계에서 적용하세요.

미리보기
  renders/01_game_camera.png : 첫 모델과 같은 카메라, 기존 2D 배경 합성
  renders/02_top_view.png    : 수직 탑뷰. 1.3:1 외곽과 중앙 정사각형 확인
  renders/03_front_view.png  : 수평 정면뷰. 낮은 바닥과 난간 높이 확인
  renders/04_full_overview.png : 전체 장식 배치를 확인하는 사선뷰
  위 이미지는 실제 내보낸 모델의 Blender 렌더입니다. Unity 캡처는 아닙니다.
  게임 시점에서 화면 밖으로 나간 꽃도 탑뷰/전체뷰 및 OBJ에는 남아 있습니다.

검증과 편집
  OBJ를 새 Blender 씬에 재임포트하여 그룹, 축, UV, 텍스처 경로, 바닥 크기와
  평탄함을 확인했습니다. 결과는 validation.json과 layout_validation.json에 있습니다.
  꽃잎/덩굴 끝은 외곽 바닥에서 약 4cm 정도 자연스럽게 돌출할 수 있습니다.
  전체 모델 약 61만 삼각형입니다. 모바일용 저사양 LOD는 포함하지 않았습니다.
  Terrace_Balanced.blend는 편집 가능한 장식 단위와 카메라를 유지합니다.
  source/Base_Approved.blend는 첫 모델을 기준으로 다시 제작하기 위한 원본입니다.
  source/build_balanced_terrace.py, finalize_balanced_terrace.py, verify_balanced_terrace.py를
  Blender 5.3의 background 모드에서 순서대로 실행하면 모델과 검증을 재생성합니다.
  중앙 텍스처는 첫 모델에서 승인된 재구성 이미지를 그대로 보존했습니다.
  처음 첨부한 2D 원본 사진의 픽셀을 직접 추출한 텍스처는 아닙니다.
