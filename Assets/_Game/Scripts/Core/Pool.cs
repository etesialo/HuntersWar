using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    /// <summary>프리팹 단위 간단한 오브젝트 풀.</summary>
    public static class Pool
    {
        static readonly Dictionary<GameObject, Stack<GameObject>> free = new();
        static readonly Dictionary<GameObject, GameObject> prefabOf = new();

        public static void Reset()
        {
            free.Clear();
            prefabOf.Clear();
        }

        public static T Spawn<T>(T prefab, Vector3 pos, Quaternion rot) where T : Component
        {
            var key = prefab.gameObject;
            if (!free.TryGetValue(key, out var stack))
            {
                stack = new Stack<GameObject>();
                free[key] = stack;
            }

            GameObject go = null;
            while (stack.Count > 0 && go == null) go = stack.Pop();

            if (go == null)
            {
                go = Object.Instantiate(key, pos, rot);
                prefabOf[go] = key;
            }
            else
            {
                go.transform.SetPositionAndRotation(pos, rot);
                go.SetActive(true);
            }
            return go.GetComponent<T>();
        }

        public static void Despawn(GameObject go)
        {
            if (go == null || !go.activeSelf) return;
            if (prefabOf.TryGetValue(go, out var key))
            {
                go.SetActive(false);
                free[key].Push(go);
            }
            else
            {
                Object.Destroy(go);
            }
        }
    }
}
