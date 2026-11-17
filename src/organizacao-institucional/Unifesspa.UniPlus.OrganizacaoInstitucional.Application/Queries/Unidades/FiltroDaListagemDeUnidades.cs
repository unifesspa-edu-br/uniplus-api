namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// O filtro que a consulta de Unidades pede, ou a primeira recusa, na ordem busca e depois tipo.
/// </summary>
internal static class FiltroDaListagemDeUnidades
{
    internal static Result<FiltroListagemUnidades> Resolver(string? busca, IReadOnlyList<int>? tipos)
    {
        Result<string?> termo = BuscaPedida.Validar(busca);
        if (!termo.IsSuccess)
        {
            return Result<FiltroListagemUnidades>.Failure(termo.Error!);
        }

        Result<IReadOnlyList<TipoUnidade>> pedidos = TiposDaListagem.Resolver(tipos);
        return pedidos.IsSuccess
            ? Result<FiltroListagemUnidades>.Success(new FiltroListagemUnidades(TermoOuNulo(termo.Value), pedidos.Value!))
            : Result<FiltroListagemUnidades>.Failure(pedidos.Error!);
    }

    private static string? TermoOuNulo(string? termo) => string.IsNullOrWhiteSpace(termo) ? null : termo;
}
