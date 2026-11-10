using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Archer.EditorTools
{
    /// <summary>
    /// 어느 씬을 열어두든 플레이 버튼을 누르면 InGame 씬으로 시작한다. 플레이를 멈추면 원래 열던 씬으로 돌아온다.
    /// 메뉴 "Archer/플레이는 항상 InGame 씬에서" 로 켜고 끈다 (기본 켬).
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromInGame
    {
        const string ScenePath = "Assets/_Game/Scenes/InGame.unity";
        const string Menu = "Archer/플레이는 항상 InGame 씬에서";
        const string PrefKey = "Archer.PlayFromInGame";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static PlayFromInGame()
        {
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            var scene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
            EditorSceneManager.playModeStartScene = scene;
            if (Enabled && scene == null)
                Debug.LogWarning($"[Archer] {ScenePath} 를 찾을 수 없어서 현재 씬으로 플레이해요.");
        }

        [MenuItem(Menu, false, 20)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(Menu, true)]
        static bool ToggleValidate()
        {
            UnityEditor.Menu.SetChecked(Menu, Enabled);
            return true;
        }
    }
}
