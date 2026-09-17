namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Handler da vitrine pública: pagina candidatos por urgência, resolve a versão publicamente
/// visível de cada um e projeta o item de lista do envelope congelado.
/// </summary>
/// <remarks>
/// <b>O tamanho da página pode vir menor que o pedido.</b> A ordenação e a paginação acontecem no
/// banco, sobre estado publicado; a visibilidade exige ato normativo registrado, que vive em outro
/// módulo e nenhuma consulta daqui pode afirmar. Um candidato sem ato em nenhuma versão da linhagem
/// é descartado depois de a página ser formada.
/// <para>
/// Isso não repete nem omite item: a âncora de continuação é a do último candidato CONSIDERADO, não
/// a do último devolvido, então o percurso avança sobre a mesma ordem independentemente do descarte.
/// Quem navega deve seguir a âncora, nunca concluir fim de coleção por página vazia.
/// </para>
/// <para>
/// O descarte é raro por construção: só alcança certame entre a publicação e o dreno da mensagem de
/// registro, ou cuja publicação teve o registro recusado — estado que alguém reconcilia.
/// </para>
/// </remarks>
public static class ListarCertamesPublicadosQueryHandler
{
    public static async Task<ListarCertamesPublicadosResult> Handle(
        ListarCertamesPublicadosQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IAtoRegistradoReader atoRegistradoReader,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(atoRegistradoReader);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        (IReadOnlyList<CandidatoDaVitrine> candidatos, DateTimeOffset instante, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
            await processoSeletivoRepository
                .ListarVitrineAsync(
                    query.Instante, query.Situacao, query.AfterSortKey, query.AfterId, query.Limit, query.Direction,
                    cancellationToken)
                .ConfigureAwait(false);

        if (candidatos.Count == 0)
        {
            return new ListarCertamesPublicadosResult([], anterior, proximo);
        }

        IReadOnlyDictionary<Guid, IReadOnlyList<LinhagemDeVersao>> linhagens = await processoSeletivoRepository
            .ObterLinhagensVigentesAsync([.. candidatos.Select(static c => c.ProcessoSeletivoId)], instante, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlySet<Guid> registrados = await atoRegistradoReader
            .FiltrarRegistradosAsync(
                [.. linhagens.Values.SelectMany(static degraus => degraus).Select(static d => d.AtoCriadorId)],
                cancellationToken)
            .ConfigureAwait(false);

        // Um degrau por candidato: o mais novo cujo ato existe. Candidato sem nenhum fica de fora.
        List<Guid> atosEleitos = [];
        foreach (CandidatoDaVitrine candidato in candidatos)
        {
            if (!linhagens.TryGetValue(candidato.ProcessoSeletivoId, out IReadOnlyList<LinhagemDeVersao>? degraus))
            {
                continue;
            }

            foreach (LinhagemDeVersao degrau in degraus)
            {
                if (registrados.Contains(degrau.AtoCriadorId))
                {
                    atosEleitos.Add(degrau.AtoCriadorId);
                    break;
                }
            }
        }

        IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
            .ObterVersoesPorAtoCriadorAsync(atosEleitos, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, VersaoConfiguracao> porProcesso = versoes.ToDictionary(static v => v.ProcessoSeletivoId);

        // A ordem da página é a do banco: o dicionário resolve conteúdo, nunca posição.
        List<CertameNaVitrineDto> itens = [];
        foreach (CandidatoDaVitrine candidato in candidatos)
        {
            if (!porProcesso.TryGetValue(candidato.ProcessoSeletivoId, out VersaoConfiguracao? versao)
                || !registroCodecs.SabeLer(versao.SchemaVersion))
            {
                continue;
            }

            if (ProjecaoDoCertamePublicado.TentarLerDocumento(versao.ConfiguracaoCongelada) is not { } envelope)
            {
                continue;
            }

            if (ProjecaoDaVitrine.Projetar(candidato, instante, envelope) is { } item)
            {
                itens.Add(item);
            }
        }

        return new ListarCertamesPublicadosResult(itens, anterior, proximo);
    }
}
