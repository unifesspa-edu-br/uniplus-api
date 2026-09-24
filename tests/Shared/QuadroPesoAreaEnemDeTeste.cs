namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Quadro de pesos por área do ENEM para testes: a classificação baseada em ENEM com cálculo
/// local exige a resolução de Pesos por Área e o quadro congelado dela.
/// </summary>
/// <remarks>
/// Cada chamada devolve grupos novos: um grupo congelado se vincula à classificação que o
/// recebe e não pode ser reaproveitado em outra. Os pesos variam por grupo para que uma troca
/// de grupo na cópia não passe despercebida.
/// </remarks>
internal static class QuadroPesoAreaEnemDeTeste
{
    public const string Resolucao = "Res. 805/2024";

    /// <param name="inverterOrdem">
    /// Entrega grupos e áreas na ordem inversa, para provar que quem ordena é a canonicalização,
    /// e não a ordem de inserção.
    /// </param>
    public static IReadOnlyList<GrupoPesoAreaEnemCongelado> Completo(bool inverterOrdem = false)
    {
        GrupoPesoAreaEnemCongelado[] grupos =
        [
            Grupo("HUMANISTICA_I", "Humanística I", pesoLinguagens: 3.00m, inverterOrdem),
            Grupo("HUMANISTICA_II", "Humanística II", pesoLinguagens: 2.50m, inverterOrdem),
            Grupo("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas", pesoLinguagens: 1.50m, inverterOrdem),
            Grupo("TECNOLOGICA", "Tecnológica", pesoLinguagens: 1.00m, inverterOrdem),
        ];

        return inverterOrdem ? [.. Enumerable.Reverse(grupos)] : grupos;
    }

    public static GrupoPesoAreaEnemCongelado Grupo(string codigo, string rotulo, decimal pesoLinguagens = 2.00m, bool inverterAreas = false)
    {
        (string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)[] areas =
        [
            ("REDACAO", "Redação", 2.00m, 400m),
            ("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, null),
            ("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, null),
            ("LINGUAGENS", "Linguagens e suas Tecnologias", pesoLinguagens, null),
            ("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, null),
        ];

        return GrupoPesoAreaEnemCongelado.Criar(
            codigo,
            rotulo,
            "Resolução nº 805/2024/Consepe – Anexo I",
            inverterAreas ? Enumerable.Reverse(areas) : areas).Value!;
    }
}
