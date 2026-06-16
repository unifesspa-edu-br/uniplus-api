namespace Unifesspa.UniPlus.Authorization.ValueObjects;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Snapshot de contrato de um escopo de auditoria vigente, consumido pela
/// decisão de autorização (ADR-0078) — <b>não</b> é a entidade persistida. A
/// hierarquia institucional não herda permissão; o acesso de auditoria é
/// explícito e vinculado a uma unidade opcional, com validade própria.
/// </summary>
public sealed record EscopoAuditoriaVigente
{
    /// <summary>Identificador do escopo de auditoria.</summary>
    public Guid EscopoId { get; }

    /// <summary>Unidade à qual o escopo se aplica. Opcional (escopo global quando nulo).</summary>
    public Guid? UnidadeId { get; }

    /// <summary>Validade do escopo.</summary>
    public DateTimeOffset ValidoAte { get; }

    private EscopoAuditoriaVigente(Guid escopoId, Guid? unidadeId, DateTimeOffset validoAte)
    {
        EscopoId = escopoId;
        UnidadeId = unidadeId;
        ValidoAte = validoAte;
    }

    /// <summary>
    /// Constrói um <see cref="EscopoAuditoriaVigente"/> validado. Rejeita
    /// escopo vazio (<see cref="Guid.Empty"/>).
    /// </summary>
    public static Result<EscopoAuditoriaVigente> From(Guid escopoId, DateTimeOffset validoAte, Guid? unidadeId = null)
    {
        if (escopoId == Guid.Empty)
        {
            return Result<EscopoAuditoriaVigente>.Failure(new DomainError(
                AuthorizationErrorCodes.EscopoAuditoriaEscopoObrigatorio,
                "Identificador do escopo de auditoria é obrigatório."));
        }

        return Result<EscopoAuditoriaVigente>.Success(new EscopoAuditoriaVigente(escopoId, unidadeId, validoAte));
    }
}
