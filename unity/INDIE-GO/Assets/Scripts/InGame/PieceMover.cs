using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//n번말 m칸 이동 정보를 받은 뒤 실제 이동하는 스크립트를 호출하는 스크립트
//해당 스크립트는 piece의 위치 정보 수정, 업기, 잡기 판단 등을 하는 스크립트임
//실제 말들의 움직임은 이 스크립트가 호출하는 실제 움직임 담당(애니메이션 포함) 스크립트가 수행
//아래 코드는 2026-07-30부로 전면 수정 필요


public class PieceMover : MonoBehaviour
{
    //Piece Move Root
    public List<Transform> pathPoints = new List<Transform>();

    public float moveSpeed = 3f;

    //Current Piece Index
    public int currentIndex = 0;

    //Is Moving Piece
    private bool isMoving = false;

    //Function Piece Move
    public void MovePiece(int moveCount)
    {
        if (!isMoving)
        {
            Debug.Log("Call Funtion Moving Log");
            StartCoroutine(MoveRoutine(moveCount));
        }
    }

    IEnumerator MoveRoutine(int moveCount)
    {
        isMoving = true;

        for (int i = 0; i < moveCount; i++)
        {
            // 마지막 칸을 넘지 않도록
            if (currentIndex >= pathPoints.Count - 1)
                break;

            currentIndex++;

            Vector3 targetPos = pathPoints[currentIndex].position;

            while (Vector3.Distance(transform.position, targetPos) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPos,
                    moveSpeed * Time.deltaTime);

                yield return null;
            }

            transform.position = targetPos;

            // 한 칸 이동 후 잠깐 멈춤
            yield return new WaitForSeconds(0.15f);
        }

        isMoving = false;
    }
}