namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CondicoesAtendimento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarCondicoesAtendimentoQueryHandler
{
    public static async Task<ListarCondicoesAtendimentoResult> Handle(
        ListarCondicoesAtendimentoQuery query,
        ICondicaoAtendimentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<CondicaoAtendimentoEspecializado> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        CondicaoAtendimentoDto[] items = [.. itens.Select(c => c.ToDto())];
        return new ListarCondicoesAtendimentoResult(items, anteriorAfterId, proximoAfterId);
    }
}
