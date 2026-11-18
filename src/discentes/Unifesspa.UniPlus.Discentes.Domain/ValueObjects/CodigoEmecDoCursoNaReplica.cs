namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código e-MEC do curso como a origem o entrega: opcional e dentro do que a réplica
/// comporta. Ausente é <see langword="null"/>; presente, mesmo vazio, é guardado como veio.
/// </summary>
public sealed record CodigoEmecDoCursoNaReplica
{
    public string Valor { get; }

    private CodigoEmecDoCursoNaReplica(string valor) => Valor = valor;

    public static Result<CodigoEmecDoCursoNaReplica?> Criar(string? texto)
    {
        DomainError? erro = TextoDaReplica.RecusarSeExceder(
            texto,
            LimitesDaReplica.CodigoEmecDoCurso,
            DiscentesErrorCodes.Curso.CodigoEmecLongo,
            "Código e-MEC excede o que a réplica comporta.");

        return erro is null
            ? Result<CodigoEmecDoCursoNaReplica?>.Success(texto is null ? null : new CodigoEmecDoCursoNaReplica(texto))
            : Result<CodigoEmecDoCursoNaReplica?>.Failure(erro);
    }
}
