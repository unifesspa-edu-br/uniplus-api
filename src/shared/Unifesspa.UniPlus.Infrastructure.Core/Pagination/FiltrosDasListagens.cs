namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Collections.Concurrent;

using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

/// <summary>
/// Entrega os filtros declarados de cada ação paginada, lidos do mesmo ApiExplorer que gera o contrato
/// publicado, uma vez por ação. Ação paginada fora do contrato é erro de montagem, como a ausência do
/// <see cref="FromCursorAttribute"/>.
/// </summary>
internal sealed class FiltrosDasListagens(IApiDescriptionGroupCollectionProvider descricoes)
{
    private readonly ConcurrentDictionary<string, FiltrosDeclaradosDaListagem> _porAcao = new(StringComparer.Ordinal);

    internal FiltrosDeclaradosDaListagem Da(ActionDescriptor acao)
    {
        ArgumentNullException.ThrowIfNull(acao);

        return _porAcao.GetOrAdd(acao.Id, _ => FiltrosDeclaradosDaListagem.De(DescricaoDa(acao)));
    }

    private ApiDescription DescricaoDa(ActionDescriptor acao) => descricoes.ApiDescriptionGroups.Items
        .SelectMany(static grupo => grupo.Items)
        .FirstOrDefault(descricao => descricao.ActionDescriptor.Id == acao.Id)
        ?? throw new InvalidOperationException(
            $"A ação paginada {acao.DisplayName} precisa constar do contrato publicado (ApiExplorer) para que o link de navegação repita só os filtros que ela declara.");
}
