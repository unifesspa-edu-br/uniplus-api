namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Contracts;

public static class ListarFatosCandidatoQueryHandler
{
    public static async Task<ListarFatosCandidatoResult> Handle(
        ListarFatosCandidatoQuery query,
        IFatoCandidatoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        IReadOnlyList<FatoCandidatoView> itens = await reader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ListarFatosCandidatoResult(itens);
    }
}
