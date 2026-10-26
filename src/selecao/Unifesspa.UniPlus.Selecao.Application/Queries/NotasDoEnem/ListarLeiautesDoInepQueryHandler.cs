namespace Unifesspa.UniPlus.Selecao.Application.Queries.NotasDoEnem;

using Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// Projeta o catálogo de leiautes, que vive no código: não há repositório. Catálogo pequeno e fixo,
/// por isso a lista não é paginada.
/// </summary>
public static class ListarLeiautesDoInepQueryHandler
{
    public static IReadOnlyList<LeiauteDoInepDto> Handle(ListarLeiautesDoInepQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        TipoDeLeiauteDoInep[] tipos = query.Tipo is { } tipo ? [tipo] : [TipoDeLeiauteDoInep.Envio, TipoDeLeiauteDoInep.Retorno];

        return [.. tipos
            .SelectMany(CatalogoDeLeiautesDoInep.Padrao.Ativos)
            .Select(LeiauteDoInepDto.De)];
    }
}
