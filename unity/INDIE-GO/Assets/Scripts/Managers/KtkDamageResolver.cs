using System.Collections.Generic;
using System.Linq;
using YutArena.Common;

namespace YutArena.Managers
{
    // 킬 더 킹 칼 종류 (기획 4-1, 4-4)
    public enum KtkSword
    {
        Normal,   // [일반 검] 데미지 +1
        Twin,     // [쌍검] 데미지 +2
        Vampire,  // [흡혈 검] 데미지 +1, 데미지가 실제로 들어가면 HP 1 회복
        Demon     // [마검] 대상 즉사, 방패 효과 무시 (서든데스·[영혼 주입]에서만 나옴)
    }

    // 킬 더 킹 방패 종류 = 탈출하면 플레이어(팀)가 얻는 [방패 효과] 종류 (기획 4-1, 5)
    public enum KtkShield
    {
        Normal,   // [일반 방패] 데미지를 막음
        Thorn,    // [가시 방패] 데미지를 막고, 공격한 상대에게 데미지 1
        Mirror    // [반사의 거울] 데미지는 들어오고, 받은 데미지 전부를 공격한 상대에게 돌려줌
    }

    // ===================================================================
    // 킬 더 킹 [탈출 처리 순서] 계산 (기획 10번)
    // 1. 탈출 묶음의 방패 -> 내 [방패 효과]로 추가
    // 2. 데미지 = 말 수 + 칼
    // 3. [마검]이면 대상 즉사 (대상 방패 효과는 발동·소모 안 함) -> 7로
    // 4. 대상 [방패 효과] 가장 최근 것 1개 소모 (일반 = 막음 / 가시 = 막고 1 반격 / 반사 = 들어오고 전부 돌려줌)
    // 5. 데미지 적용 (대상 남은 HP보다 큰 만큼 버림)
    // 6. [흡혈 검] 회복 (데미지가 실제로 들어갔을 때만)
    // 7. 대상 탈락 확인 -> 킬 보상, 1팀만 남으면 승리 (8 안 함)
    // 8. 돌아온 데미지(가시/반사) 핑퐁: 받는 쪽도 방패 효과 1개 소모하며 반복,
    //    일반 방패에 막히거나 방패 효과 없이 HP에 들어가면 끝, 누구든 탈락하면 끝
    // HP·방패 효과·탈락 처리는 KillTheKingManager가 들고 있고, 여기는 순서대로 부르기만 함
    // ===================================================================
    public class KtkDamageResolver
    {
        private readonly KillTheKingManager ktk;

        public KtkDamageResolver(KillTheKingManager ktk)
        {
            this.ktk = ktk;
        }

        public static int GetSwordBonus(KtkSword sword)
        {
            return sword switch
            {
                KtkSword.Normal => 1,
                KtkSword.Twin => 2,
                KtkSword.Vampire => 1,
                _ => 0 // 마검은 데미지 대신 즉사
            };
        }

        public void ResolveEscape(TeamSlot attacker, TeamSlot target, int pieceCount,
            IReadOnlyList<KtkSword> swords, IReadOnlyList<KtkShield> shields)
        {
            // 1. 탈출하는 묶음이 가진 방패 -> 바로 내 [방패 효과]로 추가
            foreach (var shield in shields)
                ktk.AddShieldEffect(attacker, shield);

            // 2. 데미지 계산
            int damage = pieceCount + swords.Sum(s => GetSwordBonus(s));
            ktk.Log($"[킬더킹] {attacker} 탈출 → 대상 {target} | 말 {pieceCount}" +
                (swords.Count > 0 ? ", 칼 " + string.Join("+", swords) : "") + $" = 데미지 {damage}");

            // 3. [마검]: 대상 즉사. 대상 방패 효과는 발동·소모 안 함, 4~6 건너뜀
            if (swords.Contains(KtkSword.Demon))
            {
                ktk.Log($"[킬더킹] 마검! {target} 즉사 (방패 효과 무시)");
                ktk.ApplyDamage(attacker, target, ktk.GetHp(target));
                ktk.KnockOutIfDead(target, attacker);
                return; // 방패 효과가 안 쓰였으니 돌아오는 데미지도 없음
            }

            // 4. 대상의 [방패 효과] 확인 (가장 최근 것 1개 소모)
            KtkShield? targetShield = ktk.PopShieldEffect(target);
            int bounceAmount = 0;
            if (targetShield == KtkShield.Normal)
            {
                ktk.Log($"[킬더킹] {target} 일반 방패로 막음");
            }
            else if (targetShield == KtkShield.Thorn)
            {
                ktk.Log($"[킬더킹] {target} 가시 방패로 막고 {attacker}에게 1 반격");
                bounceAmount = 1;
            }
            else
            {
                // 5. 데미지 적용 (반사의 거울도 데미지는 들어옴)
                int dealt = ktk.ApplyDamage(attacker, target, damage);
                if (targetShield == KtkShield.Mirror)
                {
                    ktk.Log($"[킬더킹] {target} 반사의 거울: 받은 데미지 {damage} 전부를 {attacker}에게 돌려줌");
                    bounceAmount = damage; // 남는 데미지 버리기 전 값 전부
                }
                // 6. [흡혈 검] 회복 (첫 타격에서 실제로 들어갔을 때만)
                if (dealt > 0 && swords.Contains(KtkSword.Vampire))
                    ktk.Heal(attacker, 1, "흡혈 검");
            }

            // 7. 대상 탈락 확인 -> 킬 보상, 1팀만 남으면 승리 (그러면 8단계 안 함)
            if (ktk.KnockOutIfDead(target, attacker) && ktk.IsGameOver) return;

            // 8. 돌아온 데미지 핑퐁 (대상이 7단계에서 탈락했어도 반사로 돌아가는 데미지는 처리)
            if (bounceAmount > 0)
                ResolveBounce(target, attacker, bounceAmount); // 대상이 공격자에게 돌려줌
        }

        // 돌아온 데미지를 받는 쪽도 처음 공격과 같은 방식으로 [방패 효과]를 확인하며 반복
        private void ResolveBounce(TeamSlot sender, TeamSlot receiver, int amount)
        {
            while (amount > 0 && !ktk.IsGameOver)
            {
                // 받을 쪽이 이미 탈락했으면 끝 (기획에 없는 경우, 확인 요청 중)
                if (!ktk.IsTeamActive(receiver)) return;

                KtkShield? shield = ktk.PopShieldEffect(receiver);
                int next = 0;
                if (shield == KtkShield.Normal)
                {
                    ktk.Log($"[킬더킹] 핑퐁: {receiver} 일반 방패로 막음, 끝");
                    return;
                }
                if (shield == KtkShield.Thorn)
                {
                    ktk.Log($"[킬더킹] 핑퐁: {receiver} 가시 방패로 막고 {sender}에게 1 반격");
                    next = 1;
                }
                else
                {
                    ktk.ApplyDamage(sender, receiver, amount);
                    if (shield == KtkShield.Mirror)
                    {
                        ktk.Log($"[킬더킹] 핑퐁: {receiver} 반사의 거울로 {amount} 그대로 {sender}에게 돌려줌");
                        next = amount;
                    }
                }

                // 핑퐁 중 누구든 탈락하면 그 자리에서 끝 (탈락시킨 쪽이 살아 있으면 킬 보상)
                if (ktk.KnockOutIfDead(receiver, sender)) return;
                if (next == 0) return; // 방패 효과 없이 HP에 들어감 -> 끝

                (sender, receiver) = (receiver, sender);
                amount = next;
            }
        }
    }
}
