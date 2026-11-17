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
        SituacaoAcademicaSnapshot situacao = new(id, descricao ?? string.Empty, vinculo);

        DomainError? erro = situacao.ValidarObrigatorios() ?? situacao.ValidarTamanhos();

        return erro is null
            ? Result<SituacaoAcademicaSnapshot>.Success(situacao)
            : Result<SituacaoAcademicaSnapshot>.Failure(erro);
    }

    private DomainError? ValidarObrigatorios()
    {
        if (Id <= 0)
            return new DomainError("SituacaoAcademica.IdInvalido", "Id da situação deve ser positivo.");

        if (string.IsNullOrWhiteSpace(Descricao))
            return new DomainError("SituacaoAcademica.DescricaoVazia", "Descrição da situação é obrigatória.");

        return null;
    }

    private DomainError? ValidarTamanhos()
    {
        if (Descricao.Length > LimitesDaReplica.DescricaoDaSituacao)
            return new DomainError(
                DiscentesErrorCodes.SituacaoAcademica.DescricaoLonga, "Descrição da situação excede o que a réplica comporta.");

        if (Vinculo is { Length: > LimitesDaReplica.VinculoDaSituacao })
            return new DomainError(
                DiscentesErrorCodes.SituacaoAcademica.VinculoLongo, "Qualificador da situação excede o que a réplica comporta.");

        return null;
    }
}
