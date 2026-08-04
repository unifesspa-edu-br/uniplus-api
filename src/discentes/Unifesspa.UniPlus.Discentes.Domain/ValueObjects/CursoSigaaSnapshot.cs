namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

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
        if (id <= 0)
            return Result<CursoSigaaSnapshot>.Failure(new DomainError("Curso.IdInvalido", "Id do curso deve ser positivo."));

        if (string.IsNullOrWhiteSpace(nome))
            return Result<CursoSigaaSnapshot>.Failure(new DomainError("Curso.NomeVazio", "Nome do curso é obrigatório."));

        if (unidadeId <= 0)
            return Result<CursoSigaaSnapshot>.Failure(new DomainError("Curso.UnidadeIdInvalido", "Id da unidade deve ser positivo."));

        if (string.IsNullOrWhiteSpace(unidadeNome))
            return Result<CursoSigaaSnapshot>.Failure(new DomainError("Curso.UnidadeNomeVazio", "Nome da unidade é obrigatório."));

        return Result<CursoSigaaSnapshot>.Success(new CursoSigaaSnapshot(id, nome, codigoEmec, unidadeId, unidadeNome));
    }
}
