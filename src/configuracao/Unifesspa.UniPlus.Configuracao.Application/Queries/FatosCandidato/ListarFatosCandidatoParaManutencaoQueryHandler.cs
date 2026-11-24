namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public static class ListarFatosCandidatoParaManutencaoQueryHandler
{
    public static async Task<ListarFatosCandidatoParaManutencaoResult> Handle(
        ListarFatosCandidatoParaManutencaoQuery query,
        IFatoCandidatoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        if (!TentarAnalisarOrigem(query.Origem, out OrigemFato? origem))
        {
            return new ListarFatosCandidatoParaManutencaoResult([], null, null);
        }

        (IReadOnlyList<FatoCandidato> itens, Guid? anterior, Guid? proximo) = await repository.ListarPaginadoAsync(
            query.AfterId, query.Limit, query.Direction, origem, query.Ativo, cancellationToken).ConfigureAwait(false);
        return new ListarFatosCandidatoParaManutencaoResult([.. itens.Select(static f => f.ToDto())], anterior, proximo);
    }

    // Origem desconhecida não filtra por nada que exista: a página é vazia, em vez de ignorar o filtro e listar tudo.
    private static bool TentarAnalisarOrigem(string? texto, out OrigemFato? origem)
    {
        origem = null;
        if (texto is null)
        {
            return true;
        }

        bool conhecida = CatalogoDe<OrigemFato>.TentarObter(texto, out OrigemFato analisada);
        origem = conhecida ? analisada : null;
        return conhecida;
    }
}
