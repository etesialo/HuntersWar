using UnityEngine;

namespace Archer
{
    [CreateAssetMenu(menuName = "Archer/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("프리팹")]
        public PlayerController player;
        public Projectile arrow;
        public Projectile enemyBullet;
        public Pickup expGem;
        public Pickup coin;
        public Angel angel;

        [Header("적 프리팹")]
        public Enemy slime;
        public Enemy bat;
        public Enemy archer;
        public Enemy charger;
        public Enemy turret;
        public Enemy miniBoss;
        public Enemy boss;

        [Header("방 프리팹 (순서대로 진행, 비면 배치도로 생성)")]
        public RoomRoot[] rooms;
        [Tooltip("깨지 않은 방의 출구를 막는 연기 장막 (방을 깨면 걷힘)")]
        public ExitBarrier exitBarrier;

        [Header("방 테마 (비면 기본 도형)")]
        public RoomTheme theme;

        [Header("바닥 메시 (맥스 Room_plan, 13.5x21m, 18x28 그리드). 비면 사각 판")]
        public GameObject floorModel;
        [Tooltip("외곽 테두리 통메시 (맥스 Roomwall_01). 있으면 벽 큐브는 판정만 남고 안 보인다")]
        public GameObject wallModel;

        [Header("테스트")]
        [Tooltip("켜면 첫 방에서 계속 진행: 적을 다 잡으면 방을 넘어가지 않고 다음 방의 적 구성이 같은 방에 웨이브로 나온다 (적 세기도 웨이브마다 오른다). 레벨업/스킬 선택은 그대로.")]
        public bool testLoopRoom = true;
        [Tooltip("유저 체력 무한 (맞는 연출은 그대로)")]
        public bool testGodMode = true;

        [Header("연출")]
        [Tooltip("적 공격 예고선 (반투명 페더 줄). 비면 telegraph 로 그린다")]
        public Material aimLine;
        [Tooltip("화염 구체 스킬의 불덩이 연출 (비면 orb 재질 구체)")]
        public GameObject fireOrbFx;
        [Tooltip("적이 나타날 때 발밑 소환진 (비면 없음)")]
        public HitSpark enemySpawnFx;
        [Tooltip("빙결된 몬스터를 감싸는 얼음덩어리")]
        public IceBlock iceBlock;
        [Tooltip("빙결이 풀릴 때 얼음이 깨지는 연출")]
        public HitSpark iceShatter;
        [Tooltip("중독된 몬스터 몸 주변 연출 (독 기포/독기/독방울)")]
        public GameObject poisonedFx;
        [Tooltip("번개 화살이 주변 적에게 튈 때 번개 줄기 재질 (비면 Sprites/Default 로 자동 생성)")]
        public Material lightningMat;
        [Tooltip("몬스터 몸통 박치기에 플레이어가 맞았을 때")]
        public HitSpark playerContactHit;

        [Header("분위기")]
        [Tooltip("방에 들어갈 때 얹는 분위기 묶음 (안개/낙엽/구름 그림자/햇살/촛불 빛무리)")]
        public Ambience ambience;

        [Header("소리")]
        public SoundBank sounds;

        [Header("장애물")]
        [Tooltip("배치도 '#' 칸에 자동으로 바위 장애물을 만든다. 끄면 새로 만들지 않고, 방 프리팹에 이미 들어간 자동 장애물(Obstacles/Obstacle)도 게임에서 꺼진다. 장애물은 배경에 직접 넣고 장애물 영역으로 마킹한다.")]
        public bool autoObstacles = false;

        [Header("바닥 구멍 (물)")]
        public Material holeWall;
        public Material holeWater;
        public Material holeMask;

        [Header("머티리얼")]
        public Material floor;
        public Material wall;
        public Material obstacle;
        public Material water;
        public Material door;
        public Material portal;
        public Material telegraph;
        public Material orb;

        [Header("밸런스")]
        [Tooltip("방이 하나 넘어갈 때마다 적 체력 증가율")]
        public float hpScalePerRoom = 0.15f;
        [Tooltip("방이 하나 넘어갈 때마다 적 공격력 증가율")]
        public float dmgScalePerRoom = 0.06f;

        /// <summary>방 레이아웃 문자 → 적 프리팹</summary>
        public Enemy GetEnemy(char c) => c switch
        {
            'S' => slime,
            'B' => bat,
            'A' => archer,
            'C' => charger,
            'T' => turret,
            'M' => miniBoss,
            'X' => boss,
            _ => null,
        };
    }
}
