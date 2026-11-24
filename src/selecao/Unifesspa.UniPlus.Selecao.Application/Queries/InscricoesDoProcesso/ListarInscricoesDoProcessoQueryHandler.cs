namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.ListagemDeInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Wolverine.Attributes;

/// <summary>
/// A listagem de quem opera o processo, projetada pela permissão da classe de cada campo (ADR-0153). O acesso ao
/// processo já foi decidido na borda; aqui a decisão de cada classe de dado é uma só, sobre o processo, e dela saem os
/// registros: um por inscrição exibida, gravados numa só mensagem com a decisão de classe mais alta exercida. A página
/// só sai depois de gravados.
/// </summary>
public static class ListarInscricoesDoProcessoQueryHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c>: a consulta não grava no banco, e o registro do acesso é publicado de forma síncrona no
    /// Kafka. A transação ambiente do Wolverine seguraria a conexão aberta até o broker confirmar.
    /// </summary>
    [NonTransactional]
    public static async Task<ListarInscricoesDoProcessoResult> Handle(
        ListarInscricoesDoProcessoQuery query, ProjetorDaListagemDeInscricoes projetor,
        IProcessoSeletivoRepository processoRepository, IInscricaoRepository inscricaoRepository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(projetor);
        ArgumentNullException.ThrowIfNull(processoRepository);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        UnidadeENomeDoProcesso? processo = await processoRepository.ObterUnidadeENomeAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return processo is null ? new([], null, null)
            : await PaginaProjetadaAsync(query, processo, projetor, inscricaoRepository, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ListarInscricoesDoProcessoResult> PaginaProjetadaAsync(
        ListarInscricoesDoProcessoQuery query, UnidadeENomeDoProcesso processo, ProjetorDaListagemDeInscricoes projetor,
        IInscricaoRepository inscricoesRepository, CancellationToken cancellationToken)
    {
        ResourceContext resource = AcessoAoProcesso.ResourceDaInscricao(processo.UnidadeId, query.ProcessoSeletivoId, inscricaoId: null);
        ProjecaoDaListagemDeInscricoes projecao = await projetor.DecidirAsync(query.ProcessoSeletivoId, resource, cancellationToken).ConfigureAwait(false);
        (IReadOnlyList<ResumoDaInscricaoDoProcesso> inscricoes, Guid? anterior, Guid? proximo) = await inscricoesRepository
            .ListarEnviadasDoProcessoAsync(query.ProcessoSeletivoId, query.AfterId, query.Limit, query.Direction,
                projecao.SaiONome, projecao.SaiOCpf, cancellationToken).ConfigureAwait(false);
        await projetor.RegistrarCadaInscricaoAsync(projecao, resource, inscricoes, cancellationToken).ConfigureAwait(false);
        ProcessoDaInscricaoDto daListagem = new(query.ProcessoSeletivoId, processo.Nome);
        return new([.. inscricoes.Select(i => projecao.Projetar(i, daListagem))], anterior, proximo);
    }
}
