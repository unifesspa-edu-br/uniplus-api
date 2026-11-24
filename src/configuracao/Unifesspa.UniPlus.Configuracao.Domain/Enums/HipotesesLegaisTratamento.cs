namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Regras de compatibilidade da hipótese legal com a classificação de proteção.</summary>
public static class HipotesesLegaisTratamento
{
    // Só o art. 7º prevê execução de contrato, interesse legítimo e proteção do crédito; só o
    // art. 11 prevê a prevenção à fraude. As demais hipóteses existem nos dois artigos.
    private static readonly HashSet<HipoteseLegalTratamento> SoDoArtigo7 =
    [
        HipoteseLegalTratamento.ExecucaoContrato,
        HipoteseLegalTratamento.InteresseLegitimo,
        HipoteseLegalTratamento.ProtecaoDoCredito,
    ];

    private static readonly HashSet<HipoteseLegalTratamento> SoDoArtigo11 = [HipoteseLegalTratamento.PrevencaoAFraude];

    /// <summary>
    /// Indica se a hipótese cabe na classificação: as do art. 11 da LGPD para o dado sensível e
    /// as do art. 7º para os demais.
    /// </summary>
    public static bool AdmiteClassificacao(HipoteseLegalTratamento hipotese, ClassificacaoDeProtecao classificacao) =>
        classificacao == ClassificacaoDeProtecao.Sensivel
            ? !SoDoArtigo7.Contains(hipotese)
            : !SoDoArtigo11.Contains(hipotese);
}
