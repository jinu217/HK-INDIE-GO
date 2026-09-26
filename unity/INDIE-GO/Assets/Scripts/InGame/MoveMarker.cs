using TMPro;
using UnityEngine;
using YutArena.Common;

namespace YutArena.InGame
{
    public enum MoveMarkerRole
    {
        PieceSelect,  // 움직일 수 있는 말 위에 뜨는 화살표. 말을 따라다닌다. 클릭 = 그 말 선택.
        Destination   // 갈 수 있는 보드 칸. ▼ 화살표 + (말 있으면) 윤곽선. 칸을 클릭하면 그 결과로 이동.
    }

    /// <summary>
    /// 이동 선택 마커 (두 역할 겸용).
    /// - PieceSelect: 말 위 파란 ▼, 말 따라다님.
    /// - Destination: 도착 칸에 ▼(결과 이름). "화살표"가 아니라 "칸"을 클릭해서 이동한다
    ///   (콜라이더는 칸 바닥부터 화살표까지 이어지는 넓은 기둥). 칸에 말이 있거나 마우스가 올라오면
    ///   그 칸의 "실제 보드 타일"을 하이라이트(색 틴트)한다 - 별도 네모 오브젝트를 만들지 않음.
    /// 클릭/호버 판정은 DebugPieceView 와 동일하게 카메라 레이 + Collider.Raycast. 프리팹 불필요(코드 생성).
    /// </summary>
    public sealed class MoveMarker : MonoBehaviour
    {
        public MoveMarkerRole Role { get; private set; }
        public int PieceId { get; private set; }
        public YutResult Result { get; private set; }

        private static Sprite sharedArrowSprite;

        // PieceSelect 전용
        private Transform followTarget;
        private float followHeight;             // 말 원점에서 ▼ 촉까지 고정 높이 (모델 측정 안 함 → 전부 동일)
        private MoveDestinationSelector screenClamper;

        // Destination 전용 — 카메라를 향한 네모 테두리로 도착 칸 강조 (타일 오브젝트는 안 건드림)
        private bool hasPieceOnTile;
        private bool isHovered;
        private bool highlightOn;
        private Color outlineBaseColor = new Color(1f, 0.85f, 0.35f, 1f);
        private Color outlineHoverColor = new Color(1f, 0.95f, 0.55f, 1f);
        private Transform outlineRing;         // 칸 Anchor 자식으로 붙는 빛나는 네모 테두리
        private SpriteRenderer outlineRingSr;
        private Vector3 frameBaseScale = Vector3.one;   // 타일 크기에 맞춘 프레임 기준 스케일
        private float outlineAngleDeg;                  // 평면(로컬 Z) 내 회전
        private bool outlineIsDiagonal;                 // 대각선(지름길) 칸인지 → Inspector 전용 각도 적용
        public bool OutlineIsDiagonal => outlineIsDiagonal;
        private static Sprite sharedFrameSprite;

        private Transform arrow;                 // ▼ (+글자)
        private SpriteRenderer arrowRenderer;
        private TextMeshPro label;
        private float baseScale = 1f;
        private float arrowLocalY;

        // ── 생성 ─────────────────────────────────────────────

        public void ConfigureAsPieceSelect(
            int pieceId, Transform follow, float height,
            Color color, float scale, MoveDestinationSelector clamper)
        {
            Role = MoveMarkerRole.PieceSelect;
            PieceId = pieceId;
            Result = YutResult.None;
            followTarget = follow;
            followHeight = height;               // 고정 높이 → 모든 말 화살표 동일
            screenClamper = clamper;
            baseScale = Mathf.Max(0.01f, scale);
            arrowLocalY = 0f;
            gameObject.name = $"MoveMarker_PieceSelect_p{pieceId}";

            EnsureArrow(color, showLabel: false);
            // 파란 ▼ 는 ▼ 주변만 작게 잡는다 (세로 기둥 X → 앞 화살표가 뒤 화살표 안 가림)
            EnsureBoxCollider(new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.55f, 0.5f));
            transform.localScale = Vector3.one;      // 루트는 월드 스케일 유지(콜라이더=월드 단위)
            arrow.localScale = Vector3.one * baseScale;
            arrow.localPosition = Vector3.zero;      // 루트(=정수리 위)에 고정
            if (followTarget != null)
                transform.position = CurrentFollowPosition();
        }

        /// <param name="tileWorldPos">도착 칸의 월드 좌표(리프트 전).</param>
        /// <param name="arrowHeightLocalY">칸 원점 기준 ▼ 높이. 말 없으면 칸 살짝 위, 말 있으면 정수리 살짝 위(+UI 회피).</param>
        /// <param name="pieceOnTile">이 칸에 다른 말이 서 있는지 → 윤곽선 상시 표시.</param>
        /// <param name="tileAnchor">이 칸의 보드 타일 Transform. 윤곽선을 여기 자식으로 붙여 보드 기울기를 그대로 물려받는다. null 이면 윤곽선 생략.</param>
        /// <param name="diagonalTile">대각선(지름길) 칸인지. Inspector 에서 별도 각도로 실시간 조절하기 위한 표식.</param>
        /// <param name="tileOutlineSize">테두리 한 변 길이. 보드 칸에 맞춰 Inspector 에서 조절.</param>
        /// <param name="angleDeg">테두리 평면 내 회전(도). 대각선 칸이면 45 근처.</param>
        /// <param name="borderThickness01">테두리 굵기(한 변 대비 비율).</param>
        /// <param name="cornerRadius01">모서리 둥글기(한 변 대비 비율).</param>
        public void ConfigureAsDestination(
            YutResult result, int pieceId, Vector3 tileWorldPos,
            float arrowHeightLocalY, bool pieceOnTile, Transform tileAnchor,
            bool diagonalTile,
            float tileOutlineSize, float angleDeg, float borderThickness01, float cornerRadius01,
            Color arrowColor, Color outlineColorBase, Color outlineColorHover, float scale)
        {
            outlineIsDiagonal = diagonalTile;
            Role = MoveMarkerRole.Destination;
            PieceId = pieceId;
            Result = result;
            followTarget = null;
            screenClamper = null;
            hasPieceOnTile = pieceOnTile;
            isHovered = false;
            outlineBaseColor = outlineColorBase;
            outlineHoverColor = outlineColorHover;
            baseScale = Mathf.Max(0.01f, scale);
            arrowLocalY = Mathf.Max(0.05f, arrowHeightLocalY);
            gameObject.name = $"MoveMarker_Dest_{result}_p{pieceId}";

            EnsureArrow(arrowColor, showLabel: true);
            label.text = GetDisplayName(result);
            EnsureOutline(tileAnchor, Mathf.Max(0.1f, tileOutlineSize), angleDeg, borderThickness01, cornerRadius01);   // 축 기준 + 평면 내 Z 회전
            // 칸 바닥 ~ ▼ 까지 잡는 콜라이더. 폭을 칸보다 좁게(0.9) 잡아 옆 칸 마커와 안 겹치게.
            float tall = arrowLocalY + 0.3f;
            EnsureBoxCollider(new Vector3(0f, tall * 0.5f, 0f), new Vector3(0.9f, tall + 0.3f, 0.9f));

            transform.localScale = Vector3.one;
            arrow.localScale = Vector3.one * baseScale;
            transform.position = tileWorldPos;
            arrow.localPosition = new Vector3(0f, arrowLocalY, 0f);
            RefreshOutline();
        }

        // ── 비주얼 ───────────────────────────────────────────

        private void EnsureArrow(Color color, bool showLabel)
        {
            if (arrow == null)
            {
                var arrowObject = new GameObject("Arrow");
                arrowObject.transform.SetParent(transform, false);
                arrow = arrowObject.transform;
                arrowRenderer = arrowObject.AddComponent<SpriteRenderer>();
                arrowRenderer.sprite = GetArrowSprite();
                arrowRenderer.sortingOrder = 50;
            }
            arrowRenderer.color = color;

            if (showLabel && label == null)
            {
                var labelObject = new GameObject("Label");
                labelObject.transform.SetParent(arrow, false);
                labelObject.transform.localPosition = new Vector3(0f, 1.35f, -0.05f); // ▼ 바로 위
                label = labelObject.AddComponent<TextMeshPro>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 2.6f;
                label.fontStyle = FontStyles.Bold;
                label.color = new Color(0.12f, 0.08f, 0.02f);
                label.raycastTarget = false;
                label.rectTransform.sizeDelta = new Vector2(4f, 2f);
                label.sortingOrder = 60;
            }
            if (label != null)
                label.gameObject.SetActive(showLabel);
        }

        private void EnsureBoxCollider(Vector3 center, Vector3 size)
        {
            var col = GetComponent<BoxCollider>();
            if (col == null)
                col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = center;
            col.size = size;
        }

        // 도착 "칸"에 윤곽선을 준다. 칸 Anchor 의 자식으로 붙여 보드 기울기/스케일을 그대로 물려받고
        // (= 원래 크기·정렬), 평면 안에서 Z 회전만 준다. 대각선 칸은 angleDeg 를 45 근처로.
        private void EnsureOutline(
            Transform tileVisual,
            float tileSize, float angleDeg, float thickness01, float radius01)
        {
            if (outlineRing != null) Destroy(outlineRing.gameObject);
            if (tileVisual == null)
            {
                Debug.Log($"[MoveMarker] {Result} 테두리 생략: 칸 Transform 없음");
                return;
            }

            frameBaseScale = Vector3.one * Mathf.Max(0.2f, tileSize);

            var frameObject = new GameObject("MoveMarkerTileOutline");
            frameObject.transform.SetParent(tileVisual, false);   // 칸 Anchor 자식 (원래대로)
            frameObject.transform.localPosition = Vector3.zero;
            frameObject.transform.localScale = frameBaseScale;

            outlineAngleDeg = angleDeg;
            outlineRing = frameObject.transform;
            ApplyOutlineRotation();
            outlineRingSr = frameObject.AddComponent<SpriteRenderer>();
            outlineRingSr.sprite = GetFrameSprite(thickness01, radius01);
            outlineRingSr.color = OpaqueRingColor(outlineBaseColor);
            // 꼭지점/분기점 칸은 3D로 솟아 있어서 보드 평면 스프라이트가 그 지오메트리에 가림.
            // 깊이 테스트를 무시하는 머티리얼로 항상 위에 그린다.
            Material overlayMat = GetOverlaySpriteMaterial();
            if (overlayMat != null) outlineRingSr.sharedMaterial = overlayMat;
            if (arrowRenderer != null)
                outlineRingSr.sortingLayerID = arrowRenderer.sortingLayerID;
            outlineRingSr.sortingOrder = 45;
            frameObject.SetActive(false);

            Debug.Log($"[MoveMarker] {Result} 테두리 생성: 칸='{tileVisual.name}' size={frameBaseScale.x:0.00} angle={angleDeg:0} order=45");
        }

        // 평면 내 미세 회전 보정을 실시간으로 반영 (Inspector 슬라이더로 각도 맞추기).
        public void SetOutlineAngle(float deg)
        {
            if (Role != MoveMarkerRole.Destination || outlineRing == null) return;
            if (Mathf.Approximately(outlineAngleDeg, deg)) return;
            outlineAngleDeg = deg;
            ApplyOutlineRotation();
        }

        private void ApplyOutlineRotation()
        {
            if (outlineRing == null) return;
            // 칸 Anchor 자식이므로 로컬 Z 회전 = 보드 평면 안에서의 회전.
            outlineRing.localRotation = Quaternion.Euler(0f, 0f, outlineAngleDeg);
        }

        private static Color OpaqueRingColor(Color c)
        {
            return new Color(c.r, c.g, c.b, 1f);
        }

        // 깊이 테스트 무시(항상 위에 그림) 스프라이트 머티리얼. 솟아있는 꼭지점 칸 지오메트리에 안 가리게.
        private static Material overlaySpriteMaterial;
        private static Material GetOverlaySpriteMaterial()
        {
            if (overlaySpriteMaterial != null) return overlaySpriteMaterial;
            Shader shader = Shader.Find("UI/Default");
            if (shader == null) return null;   // 못 찾으면 기본 스프라이트 머티리얼 사용
            overlaySpriteMaterial = new Material(shader) { name = "MoveMarkerOutlineOverlay" };
            overlaySpriteMaterial.SetInt(
                "unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            return overlaySpriteMaterial;
        }

        public void SetHovered(bool hovered)
        {
            if (Role != MoveMarkerRole.Destination || isHovered == hovered) return;
            isHovered = hovered;
            RefreshOutline();
        }

        private void RefreshOutline()
        {
            bool show = hasPieceOnTile || isHovered;
            if (show == highlightOn) return;
            highlightOn = show;
            Debug.Log($"[MoveMarker] {Result} 강조 {(show ? "ON" : "OFF")} (hover={isHovered}, ring={(outlineRing != null)})");
            if (outlineRing != null) outlineRing.gameObject.SetActive(show);
        }

        private void OnDestroy()
        {
            if (outlineRing != null) Destroy(outlineRing.gameObject);
        }

        private void Update()
        {
            // PieceSelect 화살표는 말을 따라다닌다(정수리 위 유지). Destination 은 고정.
            if (Role == MoveMarkerRole.PieceSelect && followTarget != null)
                transform.position = CurrentFollowPosition();

            // 도착 칸 강조: 크기는 칸에 딱 맞게 항상 고정. 피드백은 색 밝기 + 알파 펄스만(삐져나옴 방지).
            if (Role == MoveMarkerRole.Destination && highlightOn && outlineRingSr != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);   // 0..1
                Color glow = isHovered ? outlineHoverColor : outlineBaseColor;

                if (outlineRing.localScale != frameBaseScale)
                    outlineRing.localScale = frameBaseScale;

                Color rc = OpaqueRingColor(glow);
                rc.a = Mathf.Lerp(isHovered ? 0.85f : 0.6f, 1f, pulse);
                outlineRingSr.color = rc;
            }
        }

        private Vector3 CurrentFollowPosition()
        {
            // 말 위치 + 고정 높이 → 모든 말 화살표가 같은 높이(정수리 위).
            Vector3 p = followTarget.position + Vector3.up * followHeight;
            return screenClamper != null ? screenClamper.ClampToScreenSafeArea(p) : p;
        }

        // ── 클릭/호버 찾기 (역할별) ──────────────────────────

        public static bool TryFindAtScreenPosition(
            Camera cam, Vector2 screenPosition, MoveMarkerRole role, out MoveMarker found)
        {
            found = null;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(screenPosition);
            // 콜라이더에 맞은 것 중에서 "▼ 가 커서에 화면상 가장 가까운" 마커를 고른다.
            // (카메라 거리로 고르면 겹칠 때 앞쪽 칸이 항상 이겨서 윷 대신 걸만 잡힘)
            float bestScreenDistSqr = float.PositiveInfinity;
            foreach (MoveMarker c in FindObjectsByType<MoveMarker>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c.Role != role) continue;
                if (!c.TryHit(ray, cam, out _)) continue;

                Vector3 aim = c.arrow != null ? c.arrow.position : c.transform.position;
                Vector3 sp = cam.WorldToScreenPoint(aim);
                if (sp.z <= 0f) continue;
                float dSqr = ((Vector2)sp - screenPosition).sqrMagnitude;
                if (dSqr >= bestScreenDistSqr) continue;
                bestScreenDistSqr = dSqr;
                found = c;
            }
            return found != null;
        }

        private bool TryHit(Ray ray, Camera cam, out float distance)
        {
            distance = float.PositiveInfinity;
            bool hit = false;
            foreach (Collider col in GetComponentsInChildren<Collider>(false))
            {
                if (!col.enabled ||
                    !col.Raycast(ray, out RaycastHit h, cam.farClipPlane) ||
                    h.distance >= distance) continue;
                distance = h.distance;
                hit = true;
            }
            return hit;
        }

        // ── 헬퍼 ────────────────────────────────────────────

        // 아래 방향 삼각 화살표 ▼. 피벗 = 촉 끝(아래).
        private static Sprite GetArrowSprite()
        {
            if (sharedArrowSprite != null) return sharedArrowSprite;

            const int w = 48, h = 56;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var clear = new Color(1f, 1f, 1f, 0f);
            var solid = new Color(1f, 1f, 1f, 1f);
            float cx = (w - 1) * 0.5f;
            int tipRows = Mathf.RoundToInt(h * 0.5f);
            float stemHalf = w * 0.16f;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inside = y < tipRows
                    ? Mathf.Abs(x - cx) <= Mathf.Lerp(1.5f, w * 0.5f, y / (float)tipRows)
                    : Mathf.Abs(x - cx) <= stemHalf;
                tex.SetPixel(x, y, inside ? solid : clear);
            }
            tex.Apply();
            sharedArrowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
            return sharedArrowSprite;
        }

        // 속이 빈 "둥근 네모" 테두리. 굵기/둥글기는 Inspector 값(비율)으로 만든다.
        private static float cachedThickness01 = -1f;
        private static float cachedRadius01 = -1f;
        private static Sprite GetFrameSprite(float thickness01, float radius01)
        {
            if (sharedFrameSprite != null &&
                Mathf.Approximately(cachedThickness01, thickness01) &&
                Mathf.Approximately(cachedRadius01, radius01))
                return sharedFrameSprite;
            cachedThickness01 = thickness01;
            cachedRadius01 = radius01;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var clear = new Color(1f, 1f, 1f, 0f);
            var solid = new Color(1f, 1f, 1f, 1f);

            float half = size * 0.5f;
            float thickness = size * Mathf.Clamp(thickness01, 0.01f, 0.4f);
            float outerR = size * Mathf.Clamp(radius01, 0f, 0.5f);
            float innerR = Mathf.Max(0f, outerR - thickness);
            float outerHalf = half - 1f;
            float innerHalf = outerHalf - thickness;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x - (size - 1) * 0.5f;
                float py = y - (size - 1) * 0.5f;
                bool inOuter = RoundedRectSdf(px, py, outerHalf, outerHalf, outerR) <= 0f;
                bool inInner = RoundedRectSdf(px, py, innerHalf, innerHalf, innerR) <= 0f;
                tex.SetPixel(x, y, inOuter && !inInner ? solid : clear);
            }
            tex.Apply();
            sharedFrameSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return sharedFrameSprite;
        }

        // 둥근 사각형 SDF. <= 0 이면 내부.
        private static float RoundedRectSdf(float px, float py, float halfX, float halfY, float radius)
        {
            float qx = Mathf.Abs(px) - (halfX - radius);
            float qy = Mathf.Abs(py) - (halfY - radius);
            float ax = Mathf.Max(qx, 0f);
            float ay = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static string GetDisplayName(YutResult result)
        {
            switch (result)
            {
                case YutResult.Do: return "Do";
                case YutResult.Gae: return "Gae";
                case YutResult.Geol: return "Geol";
                case YutResult.Yut: return "Yut";
                case YutResult.Mo: return "Mo";
                case YutResult.BackDo: return "BackDo";
                default: return result.ToString();
            }
        }
    }
}
