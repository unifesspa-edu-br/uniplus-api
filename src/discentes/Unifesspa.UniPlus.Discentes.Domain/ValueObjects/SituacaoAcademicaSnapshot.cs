namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Situação acadêmica do discente no SIGAA. <see cref="Vinculo"/> é o campo
/// "vínculo" que o SIGAA associa à situação (nomenclatura do sistema de origem),
/// não a situação do <c>VinculoDiscente</c> em si.
/// </summary>
public sealed record SituacaoAcademicaSnapshot
{
    public int Id { get; }
    public string Descricao { get; }
    public string? Vinculo { get; }

    private SituacaoAcademicaSnapshot(int id, string descricao, string? vinculo)
    {
        Id = id;
        Descricao = descricao;
        Vinculo = vinculo;
    }

    public static Result<SituacaoAcademicaSnapshot> Criar(int id, string? descricao, string? vinculo)
    {
        if (id <= 0)
            return Result<SituacaoAcademicaSnapshot>.Failure(new DomainError("SituacaoAcademica.IdInvalido", "Id da situação deve ser positivo."));

        if (string.IsNullOrWhiteSpace(descricao))
            return Result<SituacaoAcademicaSnapshot>.Failure(new DomainError("SituacaoAcademica.DescricaoVazia", "Descrição da situação é obrigatória."));

        return Result<SituacaoAcademicaSnapshot>.Success(new SituacaoAcademicaSnapshot(id, descricao, vinculo));
    }
}
