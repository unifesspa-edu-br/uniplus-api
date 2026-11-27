namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ListarAtosDoItemDaProtecaoQueryHandler
{
    public static async Task<Result<IReadOnlyList<AtoDaProtecaoDto>>> Handle(
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

        return await ExisteAsync(query, tipos, fatos, cancellationToken).ConfigureAwait(false)
            ? await ListarAsync(query, atos, cancellationToken).ConfigureAwait(false)
            : Result<IReadOnlyList<AtoDaProtecaoDto>>.Failure(ItemDaProtecaoNaoEncontrado.Do(query.Cadastro));
    }

    private static async Task<Result<IReadOnlyList<AtoDaProtecaoDto>>> ListarAsync(
        ListarAtosDoItemDaProtecaoQuery query,
        IAtoDaProtecaoDoCatalogoRepository atos,
        CancellationToken cancellationToken)
    {
        CadastroDoCatalogo cadastro = query.Cadastro == CadastroDaProtecaoDoCatalogo.Fato
            ? CadastroDoCatalogo.Fato
            : CadastroDoCatalogo.TipoDocumento;

        IReadOnlyList<AtoDaProtecaoDoCatalogo> registrados = await atos
            .ListarDoItemAsync(cadastro, query.Id, cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<AtoDaProtecaoDto>>.Success([.. registrados.Select(static a => a.ToDto())]);
    }

    private static async Task<bool> ExisteAsync(
        ListarAtosDoItemDaProtecaoQuery query,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        CancellationToken cancellationToken) =>
        query.Cadastro switch
        {
            CadastroDaProtecaoDoCatalogo.TipoDocumento =>
                await tipos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false) is not null,
            CadastroDaProtecaoDoCatalogo.Fato =>
                await fatos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false) is not null,
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Cadastro, "Cadastro desconhecido."),
        };
}
