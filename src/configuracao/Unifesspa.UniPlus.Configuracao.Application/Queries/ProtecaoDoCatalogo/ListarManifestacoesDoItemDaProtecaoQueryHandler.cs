namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>O histórico das manifestações do item, contra o ciclo em que o item está.</summary>
public static class ListarManifestacoesDoItemDaProtecaoQueryHandler
{
    public static async Task<IReadOnlyList<ManifestacaoDaProtecaoDto>?> Handle(
        ListarManifestacoesDoItemDaProtecaoQuery query,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        IManifestacaoDaProtecaoRepository manifestacoes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(manifestacoes);

        AprovacaoDaProtecao? circuito = await CircuitoAsync(query, tipos, fatos, cancellationToken).ConfigureAwait(false);
        if (circuito is null)
        {
            return null;
        }

        ItemDaProtecao item = new(query.Cadastro == CadastroDaProtecaoDoCatalogo.Fato ? CadastroDoCatalogo.Fato : CadastroDoCatalogo.TipoDocumento, query.Id);
        IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> eventos = await manifestacoes.ListarDoItemAsync(item, cancellationToken).ConfigureAwait(false);
        return HistoricoDasManifestacoes.De(eventos, circuito.Ciclo).ToDto();
    }

    // O circuito do item vivo, de onde vem o ciclo corrente; nulo quando o item não existe ou foi removido.
    private static async Task<AprovacaoDaProtecao?> CircuitoAsync(
        ListarManifestacoesDoItemDaProtecaoQuery query, ITipoDocumentoRepository tipos, IFatoCandidatoRepository fatos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fatos);

        return query.Cadastro switch
        {
            CadastroDaProtecaoDoCatalogo.TipoDocumento => (await tipos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false))?.Aprovacao,
            CadastroDaProtecaoDoCatalogo.Fato => (await fatos.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false))?.Aprovacao,
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Cadastro, "Cadastro desconhecido."),
        };
    }
}
