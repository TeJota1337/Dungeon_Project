using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Popula as 10 linhas do CanvasScore com o ranking. Cada coluna (Nome/Pontos/InimigosDerrotados/
// DanoCausado) é um container com 10 filhos Text (TMP) na mesma ordem visual (posição 1 a 10) -
// esse script lê os filhos automaticamente, então não precisa arrastar os 40 textos um por um.
public class LeaderboardUI : MonoBehaviour
{
    [Header("Colunas (arraste o container 'Nome', 'Pontos' etc. do CanvasScore)")]
    public Transform nomeColumn;
    public Transform pontosColumn;
    public Transform inimigosColumn;
    public Transform danoColumn;

    private TextMeshProUGUI[] nomeTexts;
    private TextMeshProUGUI[] pontosTexts;
    private TextMeshProUGUI[] inimigosTexts;
    private TextMeshProUGUI[] danoTexts;

    void Awake()
    {
        nomeTexts = GetColumnTexts(nomeColumn);
        pontosTexts = GetColumnTexts(pontosColumn);
        inimigosTexts = GetColumnTexts(inimigosColumn);
        danoTexts = GetColumnTexts(danoColumn);
    }

    // O placar fica visível no mundo o jogo inteiro (não só no game over),
    // então já mostra o ranking salvo assim que a cena carrega.
    void Start()
    {
        Refresh(Leaderboard.Load());
    }

    static TextMeshProUGUI[] GetColumnTexts(Transform column)
    {
        return column != null ? column.GetComponentsInChildren<TextMeshProUGUI>(true) : new TextMeshProUGUI[0];
    }

    // Chamado pelo botão de reset no CanvasScore via OnClick() - apaga o ranking salvo
    // (PlayerPrefs) e já atualiza as colunas na hora, sem precisar recarregar a cena.
    public void ResetLeaderboard()
    {
        Leaderboard.Clear();
        Refresh(Leaderboard.Load());
    }

    // entries já deve vir ordenado (maior Pontos primeiro) - ex.: Leaderboard.Load()/AddEntry().
    public void Refresh(List<LeaderboardEntry> entries)
    {
        int rowCount = nomeTexts.Length;

        for (int i = 0; i < rowCount; i++)
        {
            LeaderboardEntry entry = i < entries.Count ? entries[i] : null;
            SetRow(i, entry);
        }
    }

    void SetRow(int i, LeaderboardEntry entry)
    {
        if (i < nomeTexts.Length) nomeTexts[i].text = entry != null ? entry.nome : "";
        if (i < pontosTexts.Length) pontosTexts[i].text = entry != null ? entry.pontos.ToString() : "";
        if (i < inimigosTexts.Length) inimigosTexts[i].text = entry != null ? entry.inimigosDerrotados.ToString() : "";
        if (i < danoTexts.Length) danoTexts[i].text = entry != null ? entry.danoCausado.ToString() : "";
    }
}
