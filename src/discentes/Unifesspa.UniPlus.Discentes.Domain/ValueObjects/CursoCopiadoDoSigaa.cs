namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Dados do curso do vínculo copiados do SIGAA, conforme a ADR-0061.
/// O sistema de origem é responsável por esses valores; aqui se confere sua integridade.
/// </summary>
public sealed record CursoCopiadoDoSigaa
{
    public IdDoCursoNoSigaa Id { get; }
    public NomeDoCurso Nome { get; }
    public CodigoEmecDoCursoNaReplica? CodigoEmec { get; }
    public UnidadeNoSigaa Unidade { get; }

    private CursoCopiadoDoSigaa(
        IdDoCursoNoSigaa id,
        NomeDoCurso nome,
        CodigoEmecDoCursoNaReplica? codigoEmec,
        UnidadeNoSigaa unidade)
    {
        Id = id;
        Nome = nome;
        CodigoEmec = codigoEmec;
        Unidade = unidade;
    }

    public static Result<CursoCopiadoDoSigaa> Criar(int id, string? nome, string? codigoEmec, int unidadeId, string? unidadeNome)
    {
        Result<IdDoCursoNoSigaa> idDoCurso = IdDoCursoNoSigaa.Criar(id);
        Result<IdDaUnidadeNoSigaa> idDaUnidade = IdDaUnidadeNoSigaa.Criar(unidadeId);
        DomainError? erro = idDoCurso.Error ?? NomeDoCurso.RecusarSeVazio(nome)
            ?? idDaUnidade.Error ?? NomeDaUnidade.RecusarSeVazio(unidadeNome);
        if (erro is not null)
            return Result<CursoCopiadoDoSigaa>.Failure(erro);

        return NomeDoCurso.Criar(nome).Encadear(nomeDoCurso =>
            CodigoEmecDoCursoNaReplica.Criar(codigoEmec).Encadear(emec =>
                NomeDaUnidade.Criar(unidadeNome).Compor(nomeDaUnidade =>
                    new CursoCopiadoDoSigaa(idDoCurso.Value!, nomeDoCurso, emec, new UnidadeNoSigaa(idDaUnidade.Value!, nomeDaUnidade)))));
    }
}
