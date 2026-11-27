namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ListarObrigatoriedadesLegaisQuery"/>.
/// Paginação keyset bidirecional por <c>Id</c> (ADR-0026 + ADR-0089 + Guid v7).
/// A mecânica de keyset (probe <c>n+1</c>, reversão, flags sem COUNT) vive no
/// repositório via <c>CursorKeyset</c>; o handler resolve o dia civil da vigência, quando a consulta
/// a pede, e projeta em DTO.
/// </summary>
public static class ListarObrigatoriedadesLegaisQueryHandler
{
    public static async Task<Result<ListarObrigatoriedadesLegaisResult>> Handle(
        ListarObrigatoriedadesLegaisQuery query,
        IObrigatoriedadeLegalRepository repository,
        ITipoProcessoReader tipos,
        IResolvedorFusoInstitucional fuso,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fuso);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<DateOnly?> vigentesEm = DiaDaVigencia(query, fuso, timeProvider);

        return vigentesEm.IsFailure
            ? Result<ListarObrigatoriedadesLegaisResult>.Failure(vigentesEm.Error!)
            : Result<ListarObrigatoriedadesLegaisResult>.Success(
                await ListarAsync(query, vigentesEm.Value, repository, tipos, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<ListarObrigatoriedadesLegaisResult> ListarAsync(
        ListarObrigatoriedadesLegaisQuery query,
        DateOnly? vigentesEm,
        IObrigatoriedadeLegalRepository repository,
        ITipoProcessoReader tipos,
        CancellationToken cancellationToken)
    {
        (IReadOnlyList<ObrigatoriedadeLegal> itens, Guid? anteriorAfterId, Guid? proximoAfterId) =
            await repository.ListarPaginadoAsync(
                query.AfterId, query.Limit, query.Direction,
                query.TipoProcessoCodigo, query.Categoria, vigentesEm,
                cancellationToken).ConfigureAwait(false);
        string[] codigos = [.. itens.Select(item => item.TipoProcessoCodigo)
            .Where(codigo => codigo != TodosOsTiposDeProcesso.Codigo).Distinct()];
        IReadOnlyDictionary<string, string> nomes = await tipos.ObterNomesPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false);
        return new ListarObrigatoriedadesLegaisResult(
            [.. itens.Select(item => ObrigatoriedadeLegalMapping.ToDto(item, nomes))], anteriorAfterId, proximoAfterId);
    }

    /// <summary>
    /// O dia que filtra a listagem: hoje no fuso institucional, ou nenhum quando a consulta pede todas as regras,
    /// caso em que o fuso nem é consultado.
    /// </summary>
    private static Result<DateOnly?> DiaDaVigencia(
        ListarObrigatoriedadesLegaisQuery query, IResolvedorFusoInstitucional fuso, TimeProvider timeProvider)
    {
        if (!query.Vigentes)
        {
            return Result<DateOnly?>.Success(null);
        }

        Result<DateOnly> hoje = fuso.DiaCivilDe(timeProvider.GetUtcNow());
        return hoje.IsFailure
            ? Result<DateOnly?>.Failure(hoje.Error!)
            : Result<DateOnly?>.Success(hoje.Value);
    }
}
