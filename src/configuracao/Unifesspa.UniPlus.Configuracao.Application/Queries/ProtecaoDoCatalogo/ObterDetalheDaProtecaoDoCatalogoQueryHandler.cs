namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterDetalheDaProtecaoDoCatalogoQueryHandler
{
    public static async Task<DetalheDaProtecaoDoCatalogoDto?> Handle(
        ObterDetalheDaProtecaoDoCatalogoQuery query,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fatos);

        return query.Cadastro switch
        {
            CadastroDaProtecaoDoCatalogo.TipoDocumento =>
                (await tipos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false))?.ToDetalheDaProtecao(),
            CadastroDaProtecaoDoCatalogo.Fato =>
                (await fatos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false))?.ToDetalheDaProtecao(),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Cadastro, "Cadastro desconhecido."),
        };
    }
}
