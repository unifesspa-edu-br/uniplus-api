namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>O detalhe do item com os atos registrados e as manifestações vigentes do ciclo corrente.</summary>
public static class ObterDetalheDaProtecaoDoCatalogoQueryHandler
{
    public static async Task<DetalheDaProtecaoDoCatalogoDto?> Handle(
        ObterDetalheDaProtecaoDoCatalogoQuery query,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        IAtoDaProtecaoDoCatalogoRepository atos,
        IManifestacaoDaProtecaoRepository manifestacoes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fatos);

        return query.Cadastro switch
        {
            CadastroDaProtecaoDoCatalogo.TipoDocumento => await DoTipoAsync(
                await tipos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false), atos, manifestacoes, cancellationToken).ConfigureAwait(false),
            CadastroDaProtecaoDoCatalogo.Fato => await DoFatoAsync(
                await fatos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false), atos, manifestacoes, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Cadastro, "Cadastro desconhecido."),
        };
    }

    private static async Task<DetalheDaProtecaoDoCatalogoDto?> DoTipoAsync(
        TipoDocumento? tipo, IAtoDaProtecaoDoCatalogoRepository atos, IManifestacaoDaProtecaoRepository manifestacoes, CancellationToken cancellationToken)
    {
        if (tipo is null)
        {
            return null;
        }

        ItemDaProtecao item = new(CadastroDoCatalogo.TipoDocumento, tipo.Id);
        (IReadOnlyList<AtoDaProtecaoDoCatalogo> registrados, ManifestacoesDoCiclo vigentes) =
            await LerRegistrosAsync(item, tipo.Aprovacao.Ciclo, atos, manifestacoes, cancellationToken).ConfigureAwait(false);
        return tipo.ToDetalheDaProtecao(registrados, vigentes);
    }

    private static async Task<DetalheDaProtecaoDoCatalogoDto?> DoFatoAsync(
        FatoCandidato? fato, IAtoDaProtecaoDoCatalogoRepository atos, IManifestacaoDaProtecaoRepository manifestacoes, CancellationToken cancellationToken)
    {
        if (fato is null)
        {
            return null;
        }

        ItemDaProtecao item = new(CadastroDoCatalogo.Fato, fato.Id);
        (IReadOnlyList<AtoDaProtecaoDoCatalogo> registrados, ManifestacoesDoCiclo vigentes) =
            await LerRegistrosAsync(item, fato.Aprovacao.Ciclo, atos, manifestacoes, cancellationToken).ConfigureAwait(false);
        return fato.ToDetalheDaProtecao(registrados, vigentes);
    }

    // Os dois registros só acrescentados do item: os atos, de todos os ciclos, e as manifestações do ciclo corrente.
    private static async Task<(IReadOnlyList<AtoDaProtecaoDoCatalogo> Atos, ManifestacoesDoCiclo Vigentes)> LerRegistrosAsync(
        ItemDaProtecao item,
        CicloDaAprovacao ciclo,
        IAtoDaProtecaoDoCatalogoRepository atos,
        IManifestacaoDaProtecaoRepository manifestacoes,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AtoDaProtecaoDoCatalogo> registrados = await atos.ListarDoItemAsync(item.Cadastro, item.ItemId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> eventos = await manifestacoes.ListarDoCicloAsync(item, ciclo, cancellationToken).ConfigureAwait(false);
        return (registrados, ManifestacoesDoCiclo.De(item, ciclo, eventos));
    }
}
