namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public static class ObterFatoCandidatoPorCodigoQueryHandler
{
    public static async Task<FatoDoCatalogoDto?> Handle(
        ObterFatoCandidatoPorCodigoQuery query,
        IFatoCandidatoDoCatalogoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        return await reader
            .ObterPorCodigoAsync(query.Codigo, cancellationToken)
            .ConfigureAwait(false);
    }
}
