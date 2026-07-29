using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
//각 말들의 객체 생성 스크립트
//각 말들의 정보를 여기서 참조, 수정하여 다른 스크립트에서 각 말들의 정보 참조 가능


namespace YutArena.InGame
{
    public class PieceManager : MonoBehaviour
    {
        public static PieceManager Instance { get; private set; }

        [SerializeField]
        private int piecesPerPlayer = 4;

        [SerializeField]
        private PlayerSlot[] activePlayers =
        {
            PlayerSlot.Player1,
            PlayerSlot.Player2,
            PlayerSlot.Player3,
            PlayerSlot.Player4
        };

        private Dictionary<int, PieceData> pieces = new();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            CreatePieces();
        }

        private void CreatePieces()
        {
            pieces.Clear();

            int pieceId = 0;

            foreach (PlayerSlot player in activePlayers)
            {
                for (int i = 0; i < piecesPerPlayer; i++)
                {
                    pieces.Add(pieceId, new PieceData(pieceId, player));
                    pieceId++;
                }
            }
        }

        public PieceData GetPiece(int pieceId)
        {
            return pieces[pieceId];
        }
    }
}