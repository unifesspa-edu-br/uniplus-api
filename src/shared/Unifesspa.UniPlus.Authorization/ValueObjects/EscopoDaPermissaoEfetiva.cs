namespace Unifesspa.UniPlus.Authorization.ValueObjects;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Onde uma permissão efetiva vale (ADR-0078, ADR-0084): a unidade ou a instituição, mais processo, chamada e tipo de
/// resource opcionais. É imutável; a regra de qual combinação cada origem aceita é da fábrica de
/// <see cref="EffectiveGrant"/> daquela origem.
/// </summary>
public sealed record EscopoDaPermissaoEfetiva
{
    /// <summary>Escopo da instituição: a permissão alcança só os resources sem unidade proprietária.</summary>
    public static EscopoDaPermissaoEfetiva Instituicao { get; } = new(null, true, null, null, null);

    /// <summary>Sem escopo algum, como o da titularidade, que não é concessão.</summary>
    public static EscopoDaPermissaoEfetiva Nenhum { get; } = new(null, false, null, null, null);

    /// <summary>Unidade organizacional do escopo; nula quando o escopo é a instituição ou não há unidade.</summary>
    public Guid? UnidadeId { get; }

    /// <summary>O escopo é a instituição.</summary>
    public bool Institucional { get; }

    /// <summary>Processo seletivo do escopo. Opcional.</summary>
    public Guid? ProcessoId { get; }

    /// <summary>Chamada do escopo. Opcional.</summary>
    public Guid? ChamadaId { get; }

    /// <summary>Restrição da permissão a um tipo de resource específico, como foi informada. Opcional.</summary>
    public string? ResourceTipoRestricao { get; }

    /// <summary>
    /// Há ao menos uma restrição: unidade, processo, chamada ou tipo de resource. O tipo em branco não restringe, e a
    /// instituição também não.
    /// </summary>
    public bool TemRestricao =>
        UnidadeId is not null
        || ProcessoId is not null
        || ChamadaId is not null
        || !string.IsNullOrWhiteSpace(ResourceTipoRestricao);

    private EscopoDaPermissaoEfetiva(
        Guid? unidadeId, bool institucional, Guid? processoId, Guid? chamadaId, string? resourceTipoRestricao)
    {
        UnidadeId = unidadeId;
        Institucional = institucional;
        ProcessoId = processoId;
        ChamadaId = chamadaId;
        ResourceTipoRestricao = resourceTipoRestricao;
    }

    /// <summary>
    /// Constrói o escopo. Recusa identificador informado como <see cref="Guid.Empty"/> e tipo de resource fora do
    /// kebab-case; o tipo em branco é guardado como veio, sem normalização.
    /// </summary>
    public static Result<EscopoDaPermissaoEfetiva> Criar(
        Guid? unidadeId, Guid? processoId, Guid? chamadaId, string? resourceTipoRestricao, bool institucional)
    {
        DomainError? recusa = Recusa(unidadeId, processoId, chamadaId, resourceTipoRestricao);

        return recusa is null
            ? Result<EscopoDaPermissaoEfetiva>.Success(
                new EscopoDaPermissaoEfetiva(unidadeId, institucional, processoId, chamadaId, resourceTipoRestricao))
            : Result<EscopoDaPermissaoEfetiva>.Failure(recusa);
    }

    /// <summary>O escopo de uma unidade. Recusa <see cref="Guid.Empty"/>.</summary>
    public static Result<EscopoDaPermissaoEfetiva> DaUnidade(Guid unidadeId) =>
        Criar(unidadeId, null, null, null, institucional: false);

    private static DomainError? Recusa(Guid? unidadeId, Guid? processoId, Guid? chamadaId, string? resourceTipoRestricao)
    {
        if (unidadeId == Guid.Empty || processoId == Guid.Empty || chamadaId == Guid.Empty)
        {
            return new DomainError(
                AuthorizationErrorCodes.EffectiveGrantEscopoInvalido,
                "Escopo informado não pode ser Guid.Empty — use um identificador real ou nulo.");
        }

        return !string.IsNullOrWhiteSpace(resourceTipoRestricao) && !ResourceTipos.SegueAConvencao(resourceTipoRestricao)
            ? new DomainError(
                AuthorizationErrorCodes.EffectiveGrantResourceTipoRestricaoInvalido,
                "A restrição por tipo de resource deve estar em kebab-case, em minúsculas, sem acento (ex.: tipo-documento).")
            : null;
    }
}
