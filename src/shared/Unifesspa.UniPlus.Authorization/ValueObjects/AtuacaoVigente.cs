namespace Unifesspa.UniPlus.Authorization.ValueObjects;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Snapshot de contrato de uma atuação institucional vigente (sessão delegada),
/// consumido pela decisão de autorização (ADR-0078) — <b>não</b> é a entidade
/// persistida. Representa o usuário operando em nome de uma unidade, com
/// validade própria.
/// </summary>
public sealed record AtuacaoVigente
{
    /// <summary>Unidade representada na atuação delegada.</summary>
    public Guid UnidadeRepresentadaId { get; }

    /// <summary>Validade da atuação.</summary>
    public DateTimeOffset ValidoAte { get; }

    private AtuacaoVigente(Guid unidadeRepresentadaId, DateTimeOffset validoAte)
    {
        UnidadeRepresentadaId = unidadeRepresentadaId;
        ValidoAte = validoAte;
    }

    /// <summary>
    /// Constrói uma <see cref="AtuacaoVigente"/> validada. Rejeita unidade
    /// representada vazia (<see cref="Guid.Empty"/>).
    /// </summary>
    public static Result<AtuacaoVigente> From(Guid unidadeRepresentadaId, DateTimeOffset validoAte)
    {
        if (unidadeRepresentadaId == Guid.Empty)
        {
            return Result<AtuacaoVigente>.Failure(new DomainError(
                AuthorizationErrorCodes.AtuacaoUnidadeObrigatoria,
                "Unidade representada é obrigatória."));
        }

        return Result<AtuacaoVigente>.Success(new AtuacaoVigente(unidadeRepresentadaId, validoAte));
    }
}
