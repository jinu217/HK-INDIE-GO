ROYAL BOARD TILES — 일반 / 모서리 / 중앙 3종 OBJ

1. 먼저 사용할 파일

Tile_Normal.obj
  금색 테두리와 밝은 아이보리 대리석으로 된 일반 팔각 타일 1개.
  크기 약 X 1.125 × Y 0.108 × Z 1.125.

Tile_Corner.obj
  안쪽 한 귀퉁이가 잘린 모서리 타일 1개.
  파란 에나멜, 원형 금색 테두리, 금색 꽃잎 6장, 중앙 파란 보석,
  세 모서리의 식물형 장식을 실제 입체 메쉬로 제작했습니다.
  크기 X 1.62 × Y 0.214 × Z 1.62.

Tile_Center.obj
  중앙 원형 문양과 네 방향 돌출부를 가진 중앙 타일 1개.
  파란 에나멜과 금색 꽃잎, 파란 보석, 돌출부의 금색 장식을 포함합니다.
  크기 X 2.5 × Y 0.23 × Z 2.5.

세 단독 파일 모두 원점은 바닥 중앙 (0,0,0)입니다. Y가 위쪽입니다.
각각 독립적으로 배치·이동·회전할 수 있습니다.
모서리 타일은 한 개를 복제한 뒤 90도 단위로 회전하여 네 모서리에 사용합니다.

2. 전체 배치와 가이드

Board_Layout_NoGuides.obj
  일반 24개 + 모서리 4개 + 중앙 1개, 총 29개가 배치되어 있습니다.
  K01~K04, N01~N24, C01의 개별 오브젝트 이름으로 구분되어 있습니다.
  바닥이나 맵 자체는 포함하지 않습니다.

Board_Layout_WithGuides.obj
  위와 동일한 29개 타일 + 분리된 가이드 오브젝트입니다.
  외곽 10 × 10 경계, 타일 외곽선, 중심선, 연결 경로, 타일 번호를 포함합니다.
  가이드 오브젝트를 숨기거나 지우면 게임용 배치본으로 사용할 수 있습니다.

Board_Placement_Guide.obj
  가이드만 있는 파일입니다. 기존 맵 위에 올린 뒤 단독 타일 3종을 복제하여
  번호와 외곽선에 맞춰 배치할 때 사용합니다.

중요: OBJ에는 별도의 계층/피벗 메타데이터가 없습니다. 전체 배치 OBJ는
오브젝트가 분리되어 있어도 임포터에 따라 개별 회전축이 보드 원점에 놓일 수 있습니다.
각 타일을 자기 중심으로 회전하고 싶다면 단독 OBJ 3종을 복제하거나,
아래 Unity 조립 도구를 사용하세요. 이동만 하는 경우에는 전체 배치본도 쓸 수 있습니다.

3. 기존 맵과 맞추기

이 패키지는 Terrace_Balanced의 중앙 10 × 10 가이드와 같은 단위를 사용합니다.
보드 배치의 원점은 맵의 중앙입니다. 전체 보드의 바깥 폭/깊이는 약 9.32 × 9.32입니다.
맵의 바닥 높이는 Y=0.004, 배치된 타일의 바닥은 Y=0.008입니다.
따라서 전체 배치 OBJ를 맵과 같은 루트 Transform에 놓으면 맞도록 제작되어 있습니다.
맵 전체를 스케일 변경했다면 보드도 동일한 부모와 동일한 전체 배율을 사용하세요.
중앙 타일만 따로 배치한다면 위치 (0,0.008,0)입니다.

모서리 기준: 단독 Corner의 잘린 귀퉁이는 로컬 +X,+Z 방향입니다.
후면 왼쪽 K01: 위치 (-3.85,0.008,-3.85), Y 회전   0도
후면 오른쪽 K02: 위치 (3.85,0.008,-3.85), Y 회전 -90도
전면 오른쪽 K03: 위치 (3.85,0.008,3.85), Y 회전 180도
전면 왼쪽 K04: 위치 (-3.85,0.008,3.85), Y 회전  90도
전체 29개 위치는 placement.csv 또는 board_layout.json을 참고하세요.

4. Unity에서 개별 피벗을 유지한 29개 타일 조립

일반적인 OBJ 사용은 .obj, 같은 이름의 .mtl, textures 폴더만 함께 복사하면 됩니다.
아래 도구를 사용할 때는 board_layout.json과 material_manifest.json도 같은 폴더에 둡니다.

  1) 타일 OBJ 3개와 Board_Placement_Guide.obj를 Unity Assets에 임포트합니다.
     MTL, textures, 위 JSON 파일도 경로를 유지하여 함께 복사합니다.
  2) Unity/Editor/RoyalBoardAssembler.cs를 Assets 아래 Editor 폴더에 복사합니다.
  3) Project 창에서 제공된 OBJ 중 하나를 선택합니다.
  4) Tools > Royal Board > Create editable board prefabs를 실행합니다.

생성물은 새 Generated_RoyalBoard 폴더에 들어갑니다.
  - Tile_Normal.prefab / Tile_Corner.prefab / Tile_Center.prefab
  - RoyalBoard_Editable.prefab: 29개 타일 각각 독립 피벗, 가이드 없음
  - RoyalBoard_Editable_WithGuides.prefab: 동일한 타일과 가이드
  - 현재 렌더 파이프라인에 맞춘 머티리얼

가이드는 EditorOnly 태그로 구분됩니다. 편집 화면에서 숨길 수도 있습니다.
이 도구는 기존 씬에 배치하지 않으며 기존 맵/머티리얼을 변경하지 않습니다.
생성된 Prefab을 원하는 씬으로 직접 드래그하세요.
Unity Editor에서의 컴파일/실행 검증은 하지 못했습니다. OBJ 검증은 아래와 같습니다.

5. 재질과 색감

일반 타일 상판: textures/tile_ivory_albedo.png (색상용 sRGB 이미지)
금색, 파란 에나멜, 보석: 별도 재질 슬롯이며 MTL에 색상이 들어 있습니다.
사진의 밝은 아이보리, 따뜻한 금색, 짙은 왕실 파랑을 기준으로 제작했습니다.
금속의 반사와 최종 명도는 Unity의 조명, 환경 반사, 노출, 톤 매핑에 따라 달라집니다.
위 조립 도구는 Built-in Standard / URP Lit / HDRP Lit 재질 연결을 도와줍니다.
실제 프로젝트의 밝기까지 사진과 자동으로 일치시키는 기능은 아닙니다.
필요한 원색, 금속도, 거칠기 값은 material_manifest.json에 있습니다.

6. 실제 모델 확인 이미지

renders/01_board_on_map.png : 기존 맵에 실제 모델을 놓은 게임 시점
renders/02_board_top.png : 수직 탑뷰
renders/03_layout_with_guidelines.png : 가이드 포함 배치
renders/04_placement_guide.png : 번호/외곽선만 있는 가이드
renders/05_normal_tile.png : 일반 타일 단독
renders/05_corner_tile.png : 모서리 타일 단독
renders/05_center_tile.png : 중앙 타일 단독

모두 실제 3D 모델의 Blender 렌더입니다. 이전 ImageGen 합성 이미지가 아닙니다.
맵과 2D 배경은 기존 파일을 사용한 미리보기이며 이번 OBJ에는 포함하지 않았습니다.
단독 타일 PNG는 투명 배경이라 뷰어에 따라 검은색 바탕으로 보일 수 있습니다.

7. 검증 및 편집

6개 OBJ를 새 Blender 씬에 다시 임포트하여 오브젝트 수, UV, 이미지 경로,
삼각형 유효성, 단독 타일의 바닥 원점과 전체 배치를 확인했습니다.
단독 타일 3종을 JSON 좌표대로 변환했을 때 전체 배치본의 형상과 일치하는지도 확인했습니다.
29개 타일은 10 × 10 가이드 안에 들어가며 서로의 바닥 영역이 겹치지 않습니다.
자세한 결과는 validation.json에 있습니다.

Royal_Board_Editable.blend에는 독립 배치된 29개 타일과 숨겨진 원형 타일 3종,
분리된 가이드, 조명과 카메라가 들어 있습니다.
Templates 컬렉션을 표시하면 단독 원형을 편집할 수 있습니다.
같은 종류의 타일은 메쉬 데이터를 공유하므로 원형 수정이 복제본에 반영됩니다.
source/build_royal_board.py는 형상/재질/배치를 재생성하는 Blender 스크립트입니다.
source/render_royal_board.py의 맵 합성 렌더는 이전 Terrace_Balanced 폴더가 필요합니다.

참고 사진을 바탕으로 재구성한 모델입니다. 사진에서 일관되지 않은 원근/간격은
실제 3D에서 가능한 대칭 배치로 정리했으므로 사진의 모든 픽셀이 동일하지는 않습니다.
주요 디자인, 타일 종류와 수량, 전체 경로 구조를 유지하는 것이 제작 기준입니다.

Unity API 참고
https://docs.unity3d.com/ScriptReference/PrefabUtility.InstantiatePrefab.html
https://docs.unity3d.com/ScriptReference/PrefabUtility.SaveAsPrefabAsset.html
