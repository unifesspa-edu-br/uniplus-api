namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Os valores de <c>tipo</c> que a listagem de Unidades aceita, sem repetição e na ordem pedida.
/// </summary>
internal static class TiposDaListagem
{
    /// <summary>
    /// Recusa o primeiro valor, na ordem pedida, que não é um tipo de Unidade; sem valores, a
    /// listagem não filtra por tipo. Só o número entra na mensagem: o cliente não manda texto que volte.
    /// </summary>
    internal static Result<IReadOnlyList<TipoUnidade>> Resolver(IReadOnlyList<int>? valores)
    {
        int[] pedidos = [.. (valores ?? []).Distinct()];
        int? invalido = pedidos
            .Where(valor => !Enum.IsDefined((TipoUnidade)valor))
            .Select(valor => (int?)valor)
            .FirstOrDefault();

        if (invalido is { } recusado)
        {
            return Result<IReadOnlyList<TipoUnidade>>.Failure(new DomainError(
                UnidadeErrorCodes.FiltroDeTipoInvalido,
                $"O valor de tipo '{recusado}' não corresponde a um tipo de unidade válido."));
        }

        return Result<IReadOnlyList<TipoUnidade>>.Success([.. pedidos.Select(valor => (TipoUnidade)valor)]);
    }
}
