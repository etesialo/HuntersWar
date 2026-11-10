using UnityEngine;

namespace Archer
{
    /// <summary>발사체 하나가 들고 다니는 공격 정보.</summary>
    public struct ShotData
    {
        public float damage;
        public float critChance;
        public float critMult;
        public int pierce;
        public int ricochet;
        public int wallBounce;
        public bool fire;
        public bool poison;
        public bool frost;
        public bool lightning;
        public bool wind;
        public bool rock;
        public bool fromPlayer;

        /// <summary>속성이 하나라도 붙었는지</summary>
        public bool AnyElement => fire || frost || poison || lightning || wind || rock;
    }

    public static class DamageColors
    {
        public static readonly Color Normal = Color.white;
        public static readonly Color Crit = new(1f, 0.85f, 0.2f);
        public static readonly Color Fire = new(1f, 0.5f, 0.15f);
        public static readonly Color Poison = new(0.55f, 0.95f, 0.3f);
        public static readonly Color Lightning = new(0.75f, 0.9f, 1f);
        public static readonly Color Hurt = new(1f, 0.3f, 0.3f);
        public static readonly Color Heal = new(0.4f, 1f, 0.45f);
    }

    public static class Combat
    {
        static readonly RaycastHit[] hits = new RaycastHit[16];

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>a와 b 사이에 벽이 없는지</summary>
        public static bool HasLineOfSight(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0.6f;
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(a, d / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (hits[i].collider.GetComponent<Wall>() != null) return false;
            return true;
        }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
