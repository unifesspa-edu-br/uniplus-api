namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
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

        if (descricao.Length > LimitesDaReplica.DescricaoDaSituacao)
            return Result<SituacaoAcademicaSnapshot>.Failure(new DomainError(
                DiscentesErrorCodes.SituacaoAcademica.DescricaoLonga, "Descrição da situação excede o que a réplica comporta."));

        if (vinculo is { Length: > LimitesDaReplica.VinculoDaSituacao })
            return Result<SituacaoAcademicaSnapshot>.Failure(new DomainError(
                DiscentesErrorCodes.SituacaoAcademica.VinculoLongo, "Qualificador da situação excede o que a réplica comporta."));

        return Result<SituacaoAcademicaSnapshot>.Success(new SituacaoAcademicaSnapshot(id, descricao, vinculo));
    }
}
