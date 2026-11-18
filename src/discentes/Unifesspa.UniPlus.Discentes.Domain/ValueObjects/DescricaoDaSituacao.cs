namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Descrição da situação acadêmica como o SIGAA a entrega: obrigatória e dentro do que a
/// réplica comporta.
/// </summary>
public sealed record DescricaoDaSituacao
{
    public string Valor { get; }

    private DescricaoDaSituacao(string valor) => Valor = valor;

    public static Result<DescricaoDaSituacao> Criar(string? texto)
    {
        DomainError? erro = TextoDaReplica.RecusarSeVazio(
                texto, DiscentesErrorCodes.SituacaoAcademica.DescricaoVazia, "Descrição da situação é obrigatória.")
            ?? TextoDaReplica.RecusarSeExceder(
                texto,
                LimitesDaReplica.DescricaoDaSituacao,
                DiscentesErrorCodes.SituacaoAcademica.DescricaoLonga,
                "Descrição da situação excede o que a réplica comporta.");

        return erro is null
            ? Result<DescricaoDaSituacao>.Success(new DescricaoDaSituacao(texto!))
            : Result<DescricaoDaSituacao>.Failure(erro);
    }
}
