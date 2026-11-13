namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão do Representante do controlador sobre um item com os seus fundamentos: o ato (aprovação, ajuste
/// recomendado ou reprovação), a classificação recomendada no ajuste e a justificativa.
/// </summary>
/// <param name="Ato">Aprovação, ajuste recomendado ou reprovação.</param>
/// <param name="ClassificacaoRecomendada">A classificação que o ajuste recomenda; nula nas demais decisões.</param>
/// <param name="Justificativa">
/// Obrigatória na reprovação; na aprovação e no ajuste, opcional, salvo diante de manifestação desfavorável vigente.
/// </param>
public sealed record DecisaoFundamentada(
    AtoDaProtecao Ato, ClassificacaoDeProtecao? ClassificacaoRecomendada, Fundamentacao? Justificativa)
{
    private static readonly FieldError JustificativaExigida = new("justificativa", new DomainError(
        ProtecaoDoCatalogoErrorCodes.JustificativaExigidaPelaManifestacao,
        "Há manifestação técnica desfavorável vigente: aprovar ou recomendar ajuste exige a justificativa."));

    /// <summary>
    /// Aprovação ou ajuste sem justificativa: a decisão que a manifestação desfavorável vigente não admite.
    /// </summary>
    public bool FaltaJustificativaDianteDeManifestacaoDesfavoravel =>
        (Ato is AtoDaProtecao.Aprovacao or AtoDaProtecao.AjusteRecomendado) && Justificativa is null;

    /// <summary>
    /// Confere a decisão contra o item antes de qualquer transição, com as recusas num único lote (ADR-0125): aprovar ou
    /// recomendar ajuste em item em solicitação com manifestação desfavorável vigente exige a justificativa (UNI-REQ-0374),
    /// recusada no campo <c>justificativa</c>; e a classificação recomendada pelo ajuste precisa caber na
    /// <paramref name="protecao"/> do item. Fora de solicitação a regra da justificativa não confere: a transição do
    /// circuito recusa a decisão com o código próprio.
    /// </summary>
    public Result Conferir(AprovacaoDaProtecao circuito, ProtecaoDeDados protecao)
    {
        ArgumentNullException.ThrowIfNull(circuito);

        List<FieldError> erros = FaltaAJustificativaQueAManifestacaoExige(circuito) ? [JustificativaExigida] : [];
        if (Ato == AtoDaProtecao.AjusteRecomendado)
        {
            erros.AddRange(AprovacaoDaProtecao.ConferirRecomendacao(
                ClassificacaoRecomendada ?? ClassificacaoDeProtecao.Nenhuma, protecao.Classificacao, protecao.Hipotese).Errors);
        }

        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    // A regra só vale para o item em solicitação: fora dela, a transição do circuito recusa a decisão.
    private bool FaltaAJustificativaQueAManifestacaoExige(AprovacaoDaProtecao circuito) =>
        circuito.Situacao == SituacaoDaAprovacao.Solicitada
        && circuito.ManifestacaoDesfavoravelVigente
        && FaltaJustificativaDianteDeManifestacaoDesfavoravel;
}
