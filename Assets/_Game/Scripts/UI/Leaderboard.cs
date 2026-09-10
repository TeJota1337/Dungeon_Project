using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LeaderboardEntry
{
    public string nome;
    public int pontos;
    public int inimigosDerrotados;
    public int danoCausado;
}

[System.Serializable]
class LeaderboardData
{
    public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
}

// Ranking persistente (PlayerPrefs, via JSON) das 10 melhores runs, por Pontos (maior primeiro).
public static class Leaderboard
{
    const string PrefsKey = "Leaderboard";
    const int MaxEntries = 10;

    public static List<LeaderboardEntry> Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) return new List<LeaderboardEntry>();

        LeaderboardData data = JsonUtility.FromJson<LeaderboardData>(json);
        return data != null && data.entries != null ? data.entries : new List<LeaderboardEntry>();
    }

    // Adiciona uma entrada, reordena por Pontos (desc) e descarta tudo depois da 10ª posição.
    public static List<LeaderboardEntry> AddEntry(string nome, int pontos, int inimigosDerrotados, int danoCausado)
    {
        List<LeaderboardEntry> entries = Load();

        entries.Add(new LeaderboardEntry
        {
            nome = nome,
            pontos = pontos,
            inimigosDerrotados = inimigosDerrotados,
            danoCausado = danoCausado
        });

        entries.Sort((a, b) => b.pontos.CompareTo(a.pontos));

        if (entries.Count > MaxEntries)
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

        Save(entries);
        return entries;
    }

    static void Save(List<LeaderboardEntry> entries)
    {
        string json = JsonUtility.ToJson(new LeaderboardData { entries = entries });
        PlayerPrefs.SetString(PrefsKey, json);
        PlayerPrefs.Save();
    }

    // Apaga o ranking salvo (botão de reset no CanvasScore, ver LeaderboardUI.ResetLeaderboard).
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }
}
