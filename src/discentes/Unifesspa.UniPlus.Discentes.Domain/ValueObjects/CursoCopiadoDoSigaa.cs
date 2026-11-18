namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Dados do curso do vínculo copiados do SIGAA, conforme a ADR-0061.
/// O sistema de origem é responsável por esses valores; aqui se confere sua integridade.
/// </summary>
public sealed record CursoCopiadoDoSigaa
{
    public int Id { get; }
    public string Nome { get; }
    public string? CodigoEmec { get; }
    public int UnidadeId { get; }
    public string UnidadeNome { get; }

    private CursoCopiadoDoSigaa(int id, string nome, string? codigoEmec, int unidadeId, string unidadeNome)
    {
        Id = id;
        Nome = nome;
        CodigoEmec = codigoEmec;
        UnidadeId = unidadeId;
        UnidadeNome = unidadeNome;
    }

    public static Result<CursoCopiadoDoSigaa> Criar(int id, string? nome, string? codigoEmec, int unidadeId, string? unidadeNome)
    {
        CursoCopiadoDoSigaa curso = new(id, nome ?? string.Empty, codigoEmec, unidadeId, unidadeNome ?? string.Empty);

        DomainError? erro = curso.ValidarObrigatorios() ?? curso.ValidarTamanhos();

        return erro is null
            ? Result<CursoCopiadoDoSigaa>.Success(curso)
            : Result<CursoCopiadoDoSigaa>.Failure(erro);
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
