namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>O modelo para manutenção, ativo ou desativado.</summary>
public sealed record ObterModeloFormularioQuery(Guid Id) : IQuery<ModeloFormularioDto?>;

/// <summary>
/// Lista de manutenção dos modelos, paginada, com filtros opcionais: o tipo de processo (traz os
/// modelos que servem a ele, inclusive os que servem a todos), a finalidade (token canônico) e o
/// estado ativo.
/// </summary>
public sealed record ListarModelosFormularioQuery(
    Guid? AfterId, int Limit, PaginationDirection Direction, string? TipoProcessoCodigo, string? Finalidade, bool? Ativo)
    : IQuery<ListarModelosFormularioResult>;

public sealed record ListarModelosFormularioResult(
    IReadOnlyList<ModeloFormularioDto> Items, Guid? AnteriorAfterId, Guid? ProximoAfterId);

public static class ObterModeloFormularioQueryHandler
{
    public static async Task<ModeloFormularioDto?> Handle(
        ObterModeloFormularioQuery query, IModeloFormularioRepository repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ModeloFormulario? modelo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return modelo?.ToDto();
    }
}

public static class ListarModelosFormularioQueryHandler
{
    private static readonly ListarModelosFormularioResult PaginaVazia = new([], null, null);

    public static async Task<ListarModelosFormularioResult> Handle(
        ListarModelosFormularioQuery query, IModeloFormularioRepository repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        FinalidadeFormulario? finalidade = FinalidadeDoFiltro(query.Finalidade);
        return finalidade == FinalidadeFormulario.Nenhuma || TipoDeProcessoNaoGravavel(query.TipoProcessoCodigo)
            ? PaginaVazia
            : await ListarPaginaAsync(query, finalidade, repository, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ListarModelosFormularioResult> ListarPaginaAsync(
        ListarModelosFormularioQuery query, FinalidadeFormulario? finalidade, IModeloFormularioRepository repository, CancellationToken cancellationToken)
    {
        (IReadOnlyList<ModeloFormulario> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(
                query.AfterId, query.Limit, query.Direction,
                string.IsNullOrWhiteSpace(query.TipoProcessoCodigo) ? null : query.TipoProcessoCodigo.Trim(), finalidade, query.Ativo,
                cancellationToken)
            .ConfigureAwait(false);
        return new ListarModelosFormularioResult([.. itens.Select(static m => m.ToDto())], anterior, proximo);
    }

    // Finalidade desconhecida não filtra por nada que exista: a página é vazia, em vez de ignorar
    // o filtro e listar tudo.
    private static FinalidadeFormulario? FinalidadeDoFiltro(string? token) =>
        token is null ? null : EstruturaFormulario.FinalidadeDoToken(token);

    // Tipo de processo que o banco não grava não é de nenhum modelo: a página é vazia.
    private static bool TipoDeProcessoNaoGravavel(string? tipo) => tipo is not null && !TextoGravavel.EhGravavel(tipo);
}
