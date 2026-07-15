namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Contracts;

public static class ObterFatoCandidatoPorCodigoQueryHandler
{
    public static async Task<FatoCandidatoView?> Handle(
        ObterFatoCandidatoPorCodigoQuery query,
        IFatoCandidatoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        return await reader
            .ObterPorCodigoAsync(query.Codigo, cancellationToken)
            .ConfigureAwait(false);
    }
}
