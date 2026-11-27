namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a regra de homologação das inscrições pede que o administrador confirme antes de publicar, sem
/// nunca impedir a publicação.
/// </summary>
internal static class AvisosDaHomologacao
{
    /// <summary>
    /// Um aviso por fase cuja regra não tem condição alguma, na ordem do cronograma. A regra a que a leitura recusou alguma
    /// alternativa não é dada como vazia: não se sabe o que o autor escreveu nela.
    /// </summary>
    internal static IReadOnlyList<AvisoConformidade> Da(CronogramaDoProcesso cronograma)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        return
        [
            .. cronograma.Fases
                .Where(static fase => fase.Atos.RegraDeHomologacao is { HomologaTodaInscricaoEnviada: true } && !fase.ParteRecusadaEm(FaseDeclarada.CampoDaRegraDeHomologacao))
                .Select(static fase => AvisoDaFase(fase.Citacao)),
        ];
    }

    private static AvisoConformidade AvisoDaFase(CitacaoDaFase fase) => new(
        CodigosDeAvisoDeConformidade.HomologacaoDasInscricoesSemCondicao,
        DimensaoConformidade.Cronograma,
        $"A fase {fase} homologa toda inscrição enviada, porque a regra de homologação não tem condição. Confirme que é isso que o edital prevê.");
}
