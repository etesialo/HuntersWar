using UnityEngine;

namespace Archer
{
    /// <summary>기기에 남는 기록 (PlayerPrefs). 런이 끝날 때 Commit 으로 반영한다.</summary>
    public static class SaveData
    {
        const string KGold = "save.gold", KBest = "save.bestStage", KKills = "save.totalKills", KRuns = "save.runs";

        public static int Gold => PlayerPrefs.GetInt(KGold, 0);
        public static int BestStage => PlayerPrefs.GetInt(KBest, 0);
        public static int TotalKills => PlayerPrefs.GetInt(KKills, 0);
        public static int Runs => PlayerPrefs.GetInt(KRuns, 0);

        /// <summary>런 결과 반영. 최고 기록을 넘었으면 true</summary>
        public static bool Commit(int stage, int kills, int coins)
        {
            bool best = stage > BestStage;
            if (best) PlayerPrefs.SetInt(KBest, stage);
            PlayerPrefs.SetInt(KGold, Gold + coins);
            PlayerPrefs.SetInt(KKills, TotalKills + kills);
            PlayerPrefs.SetInt(KRuns, Runs + 1);
            PlayerPrefs.Save();
            return best;
        }
    }
}
