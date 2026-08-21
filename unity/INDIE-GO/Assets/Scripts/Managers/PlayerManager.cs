using System;
using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;

/// <summary>
/// 미리 배치된 PlayerSlot들을 활성화하고, 현재 게임에 참가 중인 플레이어를 관리한다.
/// playerSlots 배열에는 PlayerSlot_1부터 순서대로 PlayerController를 등록해야 한다.
/// </summary>
public sealed class PlayerManager : MonoBehaviour
{
    [Header("Scene Slots")]
    [SerializeField] private PlayerController[] playerSlots;

    [Header("Classic Mode")]
    [Min(1)]
    [SerializeField] private int piecesPerPlayer = 4;

    private readonly List<PlayerController> activePlayers = new List<PlayerController>();

    public IReadOnlyList<PlayerController> ActivePlayers => activePlayers;
    public int MaxPlayerCount => playerSlots == null ? 0 : playerSlots.Length;

    /// <summary>
    /// 캐릭터 선택 완료 후 호출한다. playerNames를 주지 않으면 "Player 1" 등의 기본 이름을 사용한다.
    /// </summary>
    public void SetupPlayers(int playerCount, IReadOnlyList<string> playerNames = null)
    {
        SetupPlayers(playerCount, piecesPerPlayer, playerNames);
    }

    /// <summary>
    /// Creates the active player slots from the settings delivered by the lobby.
    /// </summary>
    public void SetupPlayers(GameStartSettings settings, IReadOnlyList<string> playerNames = null)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        SetupPlayers(settings.playerCount, settings.pieceCountPerPlayer, playerNames);
    }

    private void SetupPlayers(int playerCount, int pieceCountPerPlayer, IReadOnlyList<string> playerNames)
    {
        ValidateSetupRequest(playerCount, pieceCountPerPlayer, playerNames);
        activePlayers.Clear();

        for (int slotIndex = 0; slotIndex < playerSlots.Length; slotIndex++)
        {
            PlayerController slot = playerSlots[slotIndex];
            slot.ResetPlayer();
            slot.gameObject.SetActive(false);
        }

        for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
        {
            int playerId = playerIndex + 1;
            string playerName = playerNames == null ? null : playerNames[playerIndex];
            PlayerController player = playerSlots[playerIndex];

            player.gameObject.SetActive(true);
            player.Initialize(playerId, playerName, pieceCountPerPlayer);
            activePlayers.Add(player);
        }
    }

    public bool TryGetPlayer(int playerId, out PlayerController player)
    {
        int index = playerId - 1;
        if (index >= 0 && index < activePlayers.Count)
        {
            player = activePlayers[index];
            return true;
        }

        player = null;
        return false;
    }

    private void ValidateSetupRequest(int playerCount, int pieceCountPerPlayer, IReadOnlyList<string> playerNames)
    {
        if (playerSlots == null || playerSlots.Length == 0)
            throw new InvalidOperationException("PlayerManager에 PlayerSlot이 등록되지 않았습니다.");
        if (pieceCountPerPlayer <= 0)
            throw new InvalidOperationException("말 수는 1 이상이어야 합니다.");
        if (playerCount < 1 || playerCount > playerSlots.Length)
            throw new ArgumentOutOfRangeException(nameof(playerCount),
                $"플레이어 수는 1명부터 {playerSlots.Length}명까지 가능합니다.");
        if (playerNames != null && playerNames.Count != playerCount)
            throw new ArgumentException("playerNames의 개수는 playerCount와 같아야 합니다.", nameof(playerNames));

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] == null)
                throw new InvalidOperationException($"playerSlots[{i}]가 비어 있습니다.");
        }
    }
}
