namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão da proteção de dados sobre um item com os seus fundamentos: o ato (aprovação, ajuste recomendado ou
/// reprovação), a classificação recomendada no ajuste e a justificativa.
/// </summary>
/// <param name="Ato">Aprovação, ajuste recomendado ou reprovação.</param>
/// <param name="ClassificacaoRecomendada">A classificação que o ajuste recomenda; nula nas demais decisões.</param>
/// <param name="Justificativa">
/// Obrigatória na reprovação; na aprovação e no ajuste, opcional, salvo diante de manifestação desfavorável vigente.
/// </param>
public sealed record DecisaoFundamentada(
    AtoDaProtecao Ato, ClassificacaoDeProtecao? ClassificacaoRecomendada, Fundamentacao? Justificativa)
{
    /// <summary>
    /// Aprovação ou ajuste sem justificativa: a decisão que a manifestação desfavorável vigente não admite.
    /// </summary>
    public bool FaltaJustificativaDianteDeManifestacaoDesfavoravel =>
        (Ato is AtoDaProtecao.Aprovacao or AtoDaProtecao.AjusteRecomendado) && Justificativa is null;

    /// <summary>
    /// Confere a regra da justificativa contra o circuito do item, antes de qualquer transição (UNI-REQ-0374): aprovar
    /// ou recomendar ajuste em item em solicitação com manifestação desfavorável vigente exige a justificativa, e a
    /// falta dela é recusada no campo <c>justificativa</c>. Fora de solicitação não confere: a transição do circuito
    /// recusa a decisão com o código próprio.
    /// </summary>
    public Result ConferirDiante(AprovacaoDaProtecao circuito)
    {
        ArgumentNullException.ThrowIfNull(circuito);

        bool recusada = circuito.Situacao == SituacaoDaAprovacao.Solicitada
            && circuito.ManifestacaoDesfavoravelVigente
            && FaltaJustificativaDianteDeManifestacaoDesfavoravel;

        return recusada
            ? Result.ValidationFailure([new FieldError("justificativa", new DomainError(
                ProtecaoDoCatalogoErrorCodes.JustificativaExigidaPelaManifestacao,
                "Há manifestação técnica desfavorável vigente: aprovar ou recomendar ajuste exige a justificativa."))])
            : Result.Success();
    }
}
