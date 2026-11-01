namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarAtosDoItemDaProtecaoQueryHandler
{
    public static async Task<IReadOnlyList<AtoDaProtecaoDto>?> Handle(
        ListarAtosDoItemDaProtecaoQuery query,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        IAtoDaProtecaoDoCatalogoRepository atos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(atos);

        (bool existe, CadastroDoCatalogo cadastro) = query.Cadastro switch
        {
            CadastroDaProtecaoDoCatalogo.TipoDocumento =>
                (await tipos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false) is not null, CadastroDoCatalogo.TipoDocumento),
            CadastroDaProtecaoDoCatalogo.Fato =>
                (await fatos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false) is not null, CadastroDoCatalogo.Fato),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Cadastro, "Cadastro desconhecido."),
        };

        if (!existe)
        {
            return null;
        }

        IReadOnlyList<AtoDaProtecaoDoCatalogo> registrados = await atos
            .ListarDoItemAsync(cadastro, query.Id, cancellationToken)
            .ConfigureAwait(false);

        return [.. registrados.Select(static a => a.ToDto())];
    }
}
