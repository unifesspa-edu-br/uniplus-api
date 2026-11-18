namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O campo "vínculo" que o SIGAA associa à situação acadêmica, na nomenclatura da origem:
/// opcional e dentro do que a réplica comporta. Ausente é <see langword="null"/>.
/// </summary>
public sealed record QualificadorDaSituacao
{
    public string Valor { get; }

    private QualificadorDaSituacao(string valor) => Valor = valor;

    public static Result<QualificadorDaSituacao?> Criar(string? texto)
    {
        DomainError? erro = TextoDaReplica.RecusarSeExceder(
            texto,
            LimitesDaReplica.VinculoDaSituacao,
            DiscentesErrorCodes.SituacaoAcademica.VinculoLongo,
            "Qualificador da situação excede o que a réplica comporta.");

        return erro is null
            ? Result<QualificadorDaSituacao?>.Success(texto is null ? null : new QualificadorDaSituacao(texto))
            : Result<QualificadorDaSituacao?>.Failure(erro);
    }
}
