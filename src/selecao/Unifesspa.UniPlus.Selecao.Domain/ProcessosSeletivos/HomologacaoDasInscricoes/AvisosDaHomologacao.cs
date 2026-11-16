namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a regra de homologação das inscrições pede que o administrador confirme antes de publicar, sem
/// nunca impedir a publicação.
/// </summary>
internal static class AvisosDaHomologacao
{
    /// <summary>
    /// Um aviso por fase cuja regra não tem condição alguma, na ordem recebida. Com nota importada de exame
    /// não há aviso: a regra vazia já é recusada pela conferência da confirmação do INEP.
    /// </summary>
    internal static IReadOnlyList<AvisoConformidade> Da(
        IEnumerable<(string CodigoDaFase, RegraDeHomologacaoDasInscricoes Regra)> regrasPorFase,
        bool usaNotaImportadaDeExame)
    {
        ArgumentNullException.ThrowIfNull(regrasPorFase);

        return usaNotaImportadaDeExame
            ? []
            : [.. regrasPorFase
                .Where(static fase => fase.Regra.HomologaTodaInscricaoEnviada)
                .Select(static fase => AvisoDaFase(fase.CodigoDaFase))];
    }

    private static AvisoConformidade AvisoDaFase(string codigoDaFase) => new(
        CodigosDeAvisoDeConformidade.HomologacaoDasInscricoesSemCondicao,
        DimensaoConformidade.Cronograma,
        $"A fase '{codigoDaFase}' homologa toda inscrição enviada, porque a regra de homologação não tem condição. Confirme que é isso que o edital prevê.");
}
