namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public static class ListarFatosCandidatoQueryHandler
{
    public static async Task<ListarFatosCandidatoResult> Handle(
        ListarFatosCandidatoQuery query,
        IFatoCandidatoDoCatalogoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        IReadOnlyList<FatoDoCatalogoDto> itens = await reader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ListarFatosCandidatoResult(itens);
    }
}
