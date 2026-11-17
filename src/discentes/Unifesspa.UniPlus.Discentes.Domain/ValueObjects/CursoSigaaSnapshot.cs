namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Dados do curso do vínculo, espelhados do SIGAA (snapshot, não referência viva —
/// ver ADR-0061). A autoridade sobre esses valores é o sistema de origem; aqui só
/// se garante integridade estrutural.
/// </summary>
public sealed record CursoSigaaSnapshot
{
    public int Id { get; }
    public string Nome { get; }
    public string? CodigoEmec { get; }
    public int UnidadeId { get; }
    public string UnidadeNome { get; }

    private CursoSigaaSnapshot(int id, string nome, string? codigoEmec, int unidadeId, string unidadeNome)
    {
        Id = id;
        Nome = nome;
        CodigoEmec = codigoEmec;
        UnidadeId = unidadeId;
        UnidadeNome = unidadeNome;
    }

    public static Result<CursoSigaaSnapshot> Criar(int id, string? nome, string? codigoEmec, int unidadeId, string? unidadeNome)
    {
        CursoSigaaSnapshot curso = new(id, nome ?? string.Empty, codigoEmec, unidadeId, unidadeNome ?? string.Empty);

        DomainError? erro = curso.ValidarObrigatorios() ?? curso.ValidarTamanhos();

        return erro is null
            ? Result<CursoSigaaSnapshot>.Success(curso)
            : Result<CursoSigaaSnapshot>.Failure(erro);
    }

    private DomainError? ValidarObrigatorios()
    {
        if (Id <= 0)
            return new DomainError("Curso.IdInvalido", "Id do curso deve ser positivo.");

        if (string.IsNullOrWhiteSpace(Nome))
            return new DomainError("Curso.NomeVazio", "Nome do curso é obrigatório.");

        if (UnidadeId <= 0)
            return new DomainError("Curso.UnidadeIdInvalido", "Id da unidade deve ser positivo.");

        if (string.IsNullOrWhiteSpace(UnidadeNome))
            return new DomainError("Curso.UnidadeNomeVazio", "Nome da unidade é obrigatório.");

        return null;
    }

    private DomainError? ValidarTamanhos()
    {
        if (Nome.Length > LimitesDaReplica.NomeDoCurso)
            return new DomainError(
                DiscentesErrorCodes.Curso.NomeLongo, "Nome do curso excede o que a réplica comporta.");

        if (CodigoEmec is { Length: > LimitesDaReplica.CodigoEmecDoCurso })
            return new DomainError(
                DiscentesErrorCodes.Curso.CodigoEmecLongo, "Código e-MEC excede o que a réplica comporta.");

        if (UnidadeNome.Length > LimitesDaReplica.NomeDaUnidade)
            return new DomainError(
                DiscentesErrorCodes.Curso.UnidadeNomeLongo, "Nome da unidade excede o que a réplica comporta.");

        return null;
    }
}
