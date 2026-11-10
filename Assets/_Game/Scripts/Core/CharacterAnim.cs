using UnityEngine;

namespace Archer
{
    /// <summary>
    /// WAF 캐릭터 모델용 애니메이션 구동기. 컨트롤러에는 idle / move / attack / die 상태만 있고
    /// 전환은 전부 코드가 CrossFade 로 한다 (에디터의 CharacterModels 가 컨트롤러를 만든다).
    /// </summary>
    public class CharacterAnim : MonoBehaviour
    {
        static readonly int IdleId = Animator.StringToHash("idle");
        static readonly int MoveId = Animator.StringToHash("move");
        static readonly int AttackId = Animator.StringToHash("attack");
        static readonly int DieId = Animator.StringToHash("die");

        [Tooltip("공격 모션을 끝까지 안 보고 이동/대기로 돌아가는 최대 시간")]
        public float attackHold = 0.45f;

        Animator anim;
        int current;
        bool moving, dead;
        float attackTimer;

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
        }

        void OnEnable()
        {
            current = 0;
        }

        public void SetMoving(bool value) => moving = value;

        /// <summary>빙결: 그 자세 그대로 멈춤</summary>
        public void SetFrozen(bool frozen)
        {
            if (anim) anim.speed = frozen ? 0f : 1f;
        }

        /// <summary>사망 모션 길이 (초). 클립 이름에 die 가 들어간 것</summary>
        public float DieLength
        {
            get
            {
                if (anim && anim.runtimeAnimatorController)
                    foreach (var c in anim.runtimeAnimatorController.animationClips)
                        if (c && c.name.Contains("die")) return c.length;
                return 0.8f;
            }
        }

        public void Attack()
        {
            if (dead || !anim) return;
            anim.CrossFadeInFixedTime(AttackId, 0.05f, 0, 0f);
            current = AttackId;
            attackTimer = attackHold;
        }

        public void Die()
        {
            if (dead || !anim) return;
            dead = true;
            anim.speed = 1f; // 얼어 있다가 죽어도 사망 모션은 움직이게
            anim.CrossFadeInFixedTime(DieId, 0.08f);
            current = DieId;
        }

        public void ResetState()
        {
            dead = false;
            attackTimer = 0f;
            current = 0;
        }

        void Update()
        {
            if (dead || !anim) return;
            if (attackTimer > 0f)
            {
                attackTimer -= Time.deltaTime;
                // 공격 중에 움직이면 바로 이동 모션으로
                if (!moving) return;
                attackTimer = 0f;
            }
            int want = moving ? MoveId : IdleId;
            if (want == current) return;
            anim.CrossFadeInFixedTime(want, 0.12f);
            current = want;
        }
    }
}
