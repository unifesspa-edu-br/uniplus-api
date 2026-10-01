namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>O modelo para manutenção, ativo ou desativado.</summary>
public sealed record ObterModeloFormularioQuery(Guid Id) : IQuery<ModeloFormularioView?>;

/// <summary>
/// Lista de manutenção dos modelos, paginada, com filtros opcionais: o tipo de processo (traz os
/// modelos que servem a ele, inclusive os que servem a todos), a finalidade (token canônico) e o
/// estado ativo.
/// </summary>
public sealed record ListarModelosFormularioQuery(
    Guid? AfterId, int Limit, PaginationDirection Direction, string? TipoProcessoCodigo, string? Finalidade, bool? Ativo)
    : IQuery<ListarModelosFormularioResult>;

public sealed record ListarModelosFormularioResult(
    IReadOnlyList<ModeloFormularioView> Items, Guid? AnteriorAfterId, Guid? ProximoAfterId);

public static class ObterModeloFormularioQueryHandler
{
    public static async Task<ModeloFormularioView?> Handle(
        ObterModeloFormularioQuery query, IModeloFormularioRepository repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ModeloFormulario? modelo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return modelo?.ToView();
    }
}

public static class ListarModelosFormularioQueryHandler
{
    public static async Task<ListarModelosFormularioResult> Handle(
        ListarModelosFormularioQuery query, IModeloFormularioRepository repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        // Finalidade desconhecida não filtra por nada que exista: a página é vazia, em vez de ignorar
        // o filtro e listar tudo.
        FinalidadeFormulario? finalidade = null;
        if (query.Finalidade is not null)
        {
            finalidade = EstruturaFormulario.FinalidadeDoToken(query.Finalidade);
            if (finalidade == FinalidadeFormulario.Nenhuma)
            {
                return new ListarModelosFormularioResult([], null, null);
            }
        }

        // Tipo de processo que o banco não grava não é de nenhum modelo: a página é vazia.
        if (query.TipoProcessoCodigo is { } tipo && !ModeloFormulario.EhGravavel(tipo))
        {
            return new ListarModelosFormularioResult([], null, null);
        }

        (IReadOnlyList<ModeloFormulario> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(
                query.AfterId, query.Limit, query.Direction,
                string.IsNullOrWhiteSpace(query.TipoProcessoCodigo) ? null : query.TipoProcessoCodigo.Trim(), finalidade, query.Ativo,
                cancellationToken)
            .ConfigureAwait(false);
        return new ListarModelosFormularioResult([.. itens.Select(static m => m.ToView())], anterior, proximo);
    }
}
