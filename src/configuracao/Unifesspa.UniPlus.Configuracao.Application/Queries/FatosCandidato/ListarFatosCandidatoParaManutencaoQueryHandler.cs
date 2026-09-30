namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarFatosCandidatoParaManutencaoQueryHandler
{
    public static async Task<ListarFatosCandidatoParaManutencaoResult> Handle(
        ListarFatosCandidatoParaManutencaoQuery query,
        IFatoCandidatoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        // Origem desconhecida não filtra por nada que exista: a página é vazia, em vez de ignorar o
        // filtro e listar tudo.
        OrigemFato? origem = null;
        if (query.Origem is not null)
        {
            if (!OrigensFato.TryAnalisar(query.Origem, out OrigemFato analisada))
            {
                return new ListarFatosCandidatoParaManutencaoResult([], null, null);
            }

            origem = analisada;
        }

        (IReadOnlyList<FatoCandidato> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, origem, query.Ativo, cancellationToken)
            .ConfigureAwait(false);
        FatoCandidatoDto[] items = [.. itens.Select(static f => f.ToDto())];
        return new ListarFatosCandidatoParaManutencaoResult(items, anterior, proximo);
    }
}
