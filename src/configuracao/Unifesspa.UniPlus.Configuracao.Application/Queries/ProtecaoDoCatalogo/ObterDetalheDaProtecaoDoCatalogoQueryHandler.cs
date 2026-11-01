namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterDetalheDaProtecaoDoCatalogoQueryHandler
{
    public static async Task<DetalheDaProtecaoDoCatalogoDto?> Handle(
        ObterDetalheDaProtecaoDoCatalogoQuery query,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        IAtoDaProtecaoDoCatalogoRepository atos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(atos);

        switch (query.Cadastro)
        {
            case CadastroDaProtecaoDoCatalogo.TipoDocumento:
                TipoDocumento? tipo = await tipos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
                return tipo?.ToDetalheDaProtecao(await atos.ListarDoItemAsync(CadastroDoCatalogo.TipoDocumento, query.Id, cancellationToken).ConfigureAwait(false));
            case CadastroDaProtecaoDoCatalogo.Fato:
                FatoCandidato? fato = await fatos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
                return fato?.ToDetalheDaProtecao(await atos.ListarDoItemAsync(CadastroDoCatalogo.Fato, query.Id, cancellationToken).ConfigureAwait(false));
            default:
                throw new ArgumentOutOfRangeException(nameof(query), query.Cadastro, "Cadastro desconhecido.");
        }
    }
}
