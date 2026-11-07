using UnityEngine;
using YutArena.Common;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

namespace YutArena.UI
{
    public sealed class GameEndImageOverlay : MonoBehaviour
    {
        public void ReturnToLobby()
        {
            LoadMenuScene("LocalLobbyScene");
        }

        public void ReturnToStart()
        {
            LoadMenuScene("StartScene");
        }

        private void LoadMenuScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("이동할 씬이 빌드 목록에 없음: " + sceneName, this);
                return;
            }
            if (gameManager != null) gameManager.ReturnToLobby();
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        [Tooltip("게임 종료 이벤트 관리 객체")]
        [SerializeField] private TestGameManager gameManager;
        [Tooltip("화면 전체 덮개 이미지")]
        [SerializeField] private UnityEngine.UI.Image coverImage;
        [Tooltip("게임 종료 패널 루트")]
        [SerializeField] private GameObject gameEndPanel;
        [Tooltip("팀전 결과 영역")]
        [SerializeField] private GameObject teamPanel;
        [Tooltip("개인전 결과 영역")]
        [SerializeField] private GameObject soloPanel;
        [Tooltip("게임 모드 및 맵 요약 텍스트")]
        [SerializeField] private TMPro.TMP_Text gameInfoText;
        [SerializeField] private GameModeDatabase gameModeDatabase;
        [SerializeField] private MapDatabase mapDatabase;
        [Tooltip("승리 팀 표시 텍스트")]
        [SerializeField] private TMPro.TMP_Text winningTeamText;
        [Tooltip("참가 팀 수 표시 텍스트")]
        [SerializeField] private TMPro.TMP_Text teamCountText;
        [Tooltip("개인전 참가 인원 표시 텍스트")]
        [SerializeField] private TMPro.TMP_Text soloPlayerCountText;
        [Tooltip("개인전 우승자 번호 이미지")]
        [SerializeField] private UnityEngine.UI.Image soloWinnerImage;
        [Tooltip("개인전 우승자 번호 텍스트")]
        [SerializeField] private TMPro.TMP_Text soloWinnerText;
        [Tooltip("개인전 우승자 캐릭터 이름")]
        [SerializeField] private TMPro.TMP_Text soloWinnerCharacterName;
        [Tooltip("개인전 1위부터 4위 순서의 행")]
        [SerializeField] private GameObject[] soloRankingRows;
        [SerializeField] private UnityEngine.UI.Image[] soloRankingImages;
        [SerializeField] private TMPro.TMP_Text[] soloRankingTexts;
        [SerializeField] private TMPro.TMP_Text[] soloRankingNames;
        [Tooltip("1P부터 4P 순서의 번호 이미지")]
        [SerializeField] private UnityEngine.UI.Image[] playerNumberImages;
        [Tooltip("1P부터 4P 순서의 번호 텍스트")]
        [SerializeField] private TMPro.TMP_Text[] playerNumberTexts;
        [Tooltip("승리 팀 플레이어 번호 표시 이미지")]
        [SerializeField] private UnityEngine.UI.Image[] winningPlayerImages;
        [Tooltip("승리 팀 플레이어 번호 표시 텍스트")]
        [SerializeField] private TMPro.TMP_Text[] winningPlayerTexts;
        [Tooltip("1P부터 4P 순서의 캐릭터 이름 텍스트")]
        [SerializeField] private TMPro.TMP_Text[] playerCharacterNames;
        [Tooltip("승리 팀 플레이어별 캐릭터 이름 표시")]
        [SerializeField] private TMPro.TMP_Text[] winningCharacterNames;

        [Tooltip("rank1의 두 명, rank2의 두 명 순서")]
        [SerializeField] private UnityEngine.UI.Image[] rankingPlayerImages;
        [SerializeField] private TMPro.TMP_Text[] rankingPlayerTexts;
        [SerializeField] private TMPro.TMP_Text[] rankingCharacterNames;

        private void OnEnable()
        {
            if (gameEndPanel != null) gameEndPanel.SetActive(false);
            else if (coverImage != null) coverImage.gameObject.SetActive(false);
            if (gameManager == null) gameManager = FindFirstObjectByType<TestGameManager>();
            if (gameManager != null) gameManager.OnGameEnded += HandleGameEnded;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.OnGameEnded -= HandleGameEnded;
        }

        private void HandleGameEnded(GameResultData result)
        {
            if (winningTeamText != null)
                winningTeamText.text = result != null && result.winningTeam == TeamSlot.TeamA
                    ? "빨강 팀 승리"
                    : result != null && result.winningTeam == TeamSlot.TeamB
                        ? "파랑 팀 승리" : string.Empty;
            RefreshWinningPlayers(result);
            RefreshSoloWinner(result);
            RefreshSoloRanking(result);
            Show();
        }

        private void RefreshSoloWinner(GameResultData result)
        {
            int person = result != null ? (int)result.winningPlayer : 0;
            var settings = gameManager != null ? gameManager.Settings : null;
            if (settings == null) settings = GameStartSettingsHolder.Current;
            if (person == 0 && result != null && result.winningTeam != TeamSlot.None && settings != null && !settings.isTeamMode)
                for (int i = 1; i <= settings.playerCount; i++)
                    if (MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)i) == result.winningTeam)
                    {
                        person = i;
                        break;
                    }
            int index = person - 1;
            if (soloWinnerCharacterName != null)
            {
                var nameText = playerCharacterNames != null && index >= 0 && index < playerCharacterNames.Length
                    ? playerCharacterNames[index] : null;
                soloWinnerCharacterName.text = nameText != null ? nameText.text : string.Empty;
            }
            var image = playerNumberImages != null && index >= 0 && index < playerNumberImages.Length
                ? playerNumberImages[index] : null;
            var text = playerNumberTexts != null && index >= 0 && index < playerNumberTexts.Length
                ? playerNumberTexts[index] : null;
            if (soloWinnerImage != null)
            {
                soloWinnerImage.gameObject.SetActive(person > 0);
                soloWinnerImage.sprite = image != null ? image.sprite : null;
                soloWinnerImage.enabled = image != null && image.enabled;
                if (image != null) soloWinnerImage.color = image.color;
            }
            if (soloWinnerText != null)
            {
                soloWinnerText.text = text != null ? text.text : person > 0 ? person + "P" : string.Empty;
                if (text != null) soloWinnerText.color = text.color;
            }
        }

        private void RefreshSoloRanking(GameResultData result)
        {
            if (soloRankingRows == null) return;
            var settings = gameManager != null ? gameManager.Settings : null;
            if (settings == null) settings = GameStartSettingsHolder.Current;
            for (int rank = 0; rank < soloRankingRows.Length; rank++)
            {
                int person = 0;
                if (settings != null && !settings.isTeamMode && result != null && rank < settings.playerCount)
                {
                    var team = result.finalRanking != null && rank < result.finalRanking.Count
                        ? result.finalRanking[rank] : rank == 0 ? result.winningTeam : TeamSlot.None;
                    if (team != TeamSlot.None)
                        for (int i = 1; i <= settings.playerCount; i++)
                            if (MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)i) == team)
                            {
                                person = i;
                                break;
                            }
                    if (person == 0 && rank == 0) person = (int)result.winningPlayer;
                }
                if (soloRankingRows[rank] != null) soloRankingRows[rank].SetActive(person > 0);
                int index = person - 1;
                var sourceImage = playerNumberImages != null && index >= 0 && index < playerNumberImages.Length ? playerNumberImages[index] : null;
                var sourceText = playerNumberTexts != null && index >= 0 && index < playerNumberTexts.Length ? playerNumberTexts[index] : null;
                var sourceName = playerCharacterNames != null && index >= 0 && index < playerCharacterNames.Length ? playerCharacterNames[index] : null;
                if (soloRankingImages != null && rank < soloRankingImages.Length && soloRankingImages[rank] != null)
                {
                    soloRankingImages[rank].sprite = sourceImage != null ? sourceImage.sprite : null;
                    soloRankingImages[rank].enabled = sourceImage != null && sourceImage.enabled;
                    if (sourceImage != null) soloRankingImages[rank].color = sourceImage.color;
                }
                if (soloRankingTexts != null && rank < soloRankingTexts.Length && soloRankingTexts[rank] != null)
                {
                    soloRankingTexts[rank].text = sourceText != null ? sourceText.text : person > 0 ? person + "P" : string.Empty;
                    if (sourceText != null) soloRankingTexts[rank].color = sourceText.color;
                }
                if (soloRankingNames != null && rank < soloRankingNames.Length && soloRankingNames[rank] != null)
                    soloRankingNames[rank].text = sourceName != null ? sourceName.text : string.Empty;
            }
        }

        private void RefreshWinningPlayers(GameResultData result)
        {
            var settings = gameManager != null ? gameManager.Settings : null;
            if (settings == null) settings = GameStartSettingsHolder.Current;
            FillTeamPlayers(settings, result != null ? result.winningTeam : TeamSlot.None,
                winningPlayerImages, winningPlayerTexts, winningCharacterNames);
            var ranking = new System.Collections.Generic.List<TeamSlot>();
            if (result != null && result.finalRanking != null)
                foreach (var team in result.finalRanking)
                    if (team != TeamSlot.None && !ranking.Contains(team)) ranking.Add(team);
            if (ranking.Count == 0 && result != null && result.winningTeam != TeamSlot.None)
                ranking.Add(result.winningTeam);
            // 2팀 대전에서 순위 데이터가 생략된 경우 우승팀 외 참가팀을 2위로 표시
            if (ranking.Count == 1 && settings != null && settings.isTeamMode)
                for (int person = 1; person <= settings.playerCount; person++)
                {
                    var team = MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)person);
                    if (team != TeamSlot.None && !ranking.Contains(team)) ranking.Add(team);
                }
            for (int row = 0; row < 2; row++)
            {
                var images = new UnityEngine.UI.Image[2];
                var texts = new TMPro.TMP_Text[2];
                var names = new TMPro.TMP_Text[2];
                for (int i = 0; i < 2; i++)
                {
                    int index = row * 2 + i;
                    if (rankingPlayerImages != null && index < rankingPlayerImages.Length) images[i] = rankingPlayerImages[index];
                    if (rankingPlayerTexts != null && index < rankingPlayerTexts.Length) texts[i] = rankingPlayerTexts[index];
                    if (rankingCharacterNames != null && index < rankingCharacterNames.Length) names[i] = rankingCharacterNames[index];
                }
                FillTeamPlayers(settings, row < ranking.Count ? ranking[row] : TeamSlot.None, images, texts, names);
            }
        }

        private void FillTeamPlayers(GameStartSettings settings, TeamSlot team,
            UnityEngine.UI.Image[] winningPlayerImages, TMPro.TMP_Text[] winningPlayerTexts,
            TMPro.TMP_Text[] winningCharacterNames)
        {
            if (winningPlayerImages == null) return;
            int slot = 0;
            if (settings != null && settings.isTeamMode && team != TeamSlot.None)
                for (int person = 1; person <= settings.playerCount && slot < winningPlayerImages.Length; person++)
                {
                    // 공유 캐릭터 번호 대신 실제 참가자 번호 기준으로 승리 팀 구성원 조회
                    if (MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)person) != team) continue;
                    int index = person - 1;
                    var target = winningPlayerImages[slot];
                    var source = playerNumberImages != null && index < playerNumberImages.Length ? playerNumberImages[index] : null;
                    if (target != null)
                    {
                        target.gameObject.SetActive(true);
                        target.sprite = source != null ? source.sprite : null;
                        target.enabled = source != null && source.enabled;
                        if (source != null) target.color = source.color;
                    }
                    if (winningPlayerTexts != null && slot < winningPlayerTexts.Length && winningPlayerTexts[slot] != null)
                    {
                        var text = playerNumberTexts != null && index < playerNumberTexts.Length ? playerNumberTexts[index] : null;
                        winningPlayerTexts[slot].text = text != null ? text.text : person + "P";
                        if (text != null) winningPlayerTexts[slot].color = text.color;
                    }
                    if (winningCharacterNames != null && slot < winningCharacterNames.Length && winningCharacterNames[slot] != null)
                    {
                        var nameText = playerCharacterNames != null && index < playerCharacterNames.Length
                            ? playerCharacterNames[index] : null;
                        winningCharacterNames[slot].text = nameText != null ? nameText.text : string.Empty;
                    }
                    slot++;
                }
            for (; slot < winningPlayerImages.Length; slot++)
            {
                if (winningPlayerImages[slot] != null) winningPlayerImages[slot].gameObject.SetActive(false);
                if (winningCharacterNames != null && slot < winningCharacterNames.Length && winningCharacterNames[slot] != null)
                    winningCharacterNames[slot].text = string.Empty;
            }
        }

        public void Show()
        {
            var settings = gameManager != null ? gameManager.Settings : null;
            if (settings == null) settings = GameStartSettingsHolder.Current;
            bool isTeam = settings != null && settings.isTeamMode;
            if (soloPlayerCountText != null && settings != null)
                soloPlayerCountText.text = $"{settings.playerCount}명";
            if (teamCountText != null && settings != null)
            {
                var teams = new System.Collections.Generic.HashSet<TeamSlot>();
                for (int i = 1; i <= settings.playerCount; i++)
                {
                    var team = MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)i);
                    if (team != TeamSlot.None) teams.Add(team);
                }
                teamCountText.text = $"{teams.Count}개의 팀";
            }
            if (gameInfoText != null && settings != null)
            {
                var mode = gameModeDatabase != null ? gameModeDatabase.FindByMode(settings.gameMode) : null;
                string modeName = mode != null && !string.IsNullOrWhiteSpace(mode.modeName)
                    ? mode.modeName : settings.gameMode.ToString();
                var mapManager = FindFirstObjectByType<YutArena.InGame.MapManager>();
                var mapType = mapManager != null && mapManager.CurrentMap != null
                    ? mapManager.CurrentMap.mapType : settings.mapType;
                string mapName = mapType.ToString();
                if (mapDatabase != null)
                    foreach (var entry in mapDatabase.GetOrderedMaps())
                        if (entry.mapType == mapType && !string.IsNullOrWhiteSpace(entry.mapName))
                        {
                            mapName = entry.mapName;
                            break;
                        }
                gameInfoText.text = $"{modeName} / {(isTeam ? "팀전" : "개인전")} · {mapName}";
            }
            if (teamPanel != null) teamPanel.SetActive(isTeam);
            if (soloPanel != null) soloPanel.SetActive(!isTeam);
            if (coverImage != null) coverImage.gameObject.SetActive(true);
            if (gameEndPanel != null)
            {
                gameEndPanel.transform.SetAsLastSibling();
                gameEndPanel.SetActive(true);
            }
        }
    }
}

