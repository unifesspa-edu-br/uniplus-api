namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O painel de conformidade e o congelamento recebem o mesmo conjunto de corretores, lido do catálogo pelo mesmo
/// serviço: o painel não aprova a banca que a publicação recusa, nem recusa a que ela aceita.
/// </summary>
public sealed class CorretoresDasEtapasNoCongelamentoENoPainelTests
{
    private const string ItemDoMinimo = "cronograma_banca_com_o_minimo_de_integrantes";

    private static async Task<ConformidadeProcessoSeletivoDto> Painel(ProcessoSeletivo processo) =>
        (await ProcessoComCorrecaoIndependente.Servicos(processo).ConformidadeAsync(new ObterConformidadeProcessoSeletivoQuery(processo.Id))).Value!;

    [Theory(DisplayName = "O painel mostra o item do mínimo de integrantes vermelho com a banca de 2 e verde com a de 3, como a publicação")]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task Painel_BancaDaEtapa_ConcordaComAPublicacao(int integrantes, bool conforme)
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComBancaDaEtapa(integrantes);

        ConformidadeProcessoSeletivoDto painel = await Painel(processo);
        (Result publicacao, _) = await ProcessoComCorrecaoIndependente.Servicos(processo).PublicarAsync(processo.Id);

        painel.Itens.Single(item => item.Codigo == ItemDoMinimo).Ok.Should().Be(conforme);
        publicacao.IsSuccess.Should().Be(conforme, publicacao.Error?.Message);
    }

    [Theory(DisplayName = "O painel mostra o item do mínimo vermelho com a banca da fase de 2 e verde com a de 3, como a publicação")]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task Painel_BancaDaFase_ConcordaComAPublicacao(int integrantes, bool conforme)
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComBancaDaFase(integrantes);

        ConformidadeProcessoSeletivoDto painel = await Painel(processo);
        (Result publicacao, _) = await ProcessoComCorrecaoIndependente.Servicos(processo).PublicarAsync(processo.Id);

        painel.Itens.Single(item => item.Codigo == ItemDoMinimo).Ok.Should().Be(conforme);
        publicacao.IsSuccess.Should().Be(conforme, publicacao.Error?.Message);
    }

    [Fact(DisplayName = "O painel deixa vermelho o item do mínimo com a regra de consolidação não resolvida, sem lançar")]
    public async Task Painel_RegraForaDoCatalogo_DeixaOItemVermelhoSemLancar()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComBancaDaEtapa(integrantes: 5);

        ConformidadeProcessoSeletivoDto? painel = (await ProcessoComCorrecaoIndependente.Servicos(processo)
            .ComRegrasDeConsolidacao(RegrasDeConsolidacaoDeTeste.Catalogo(RegrasDeConsolidacaoDeTeste.AvaliacaoUnica))
            .ConformidadeAsync(new ObterConformidadeProcessoSeletivoQuery(processo.Id))).Value;

        painel!.Itens.Single(item => item.Codigo == ItemDoMinimo).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O painel e a publicação leem cada referência do catálogo, e o conjunto é o mesmo nos dois")]
    public async Task PainelEPublicacao_LeemACadaReferencia()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComBancaDaEtapa(integrantes: 3);
        IRegraCatalogoReader noPainel = RegrasDeConsolidacaoDeTeste.Catalogo();
        IRegraCatalogoReader naPublicacao = RegrasDeConsolidacaoDeTeste.Catalogo();

        await ProcessoComCorrecaoIndependente.Servicos(processo).ComRegrasDeConsolidacao(noPainel)
            .ConformidadeAsync(new ObterConformidadeProcessoSeletivoQuery(processo.Id));
        await ProcessoComCorrecaoIndependente.Servicos(processo).ComRegrasDeConsolidacao(naPublicacao).PublicarAsync(processo.Id);

        foreach (IRegraCatalogoReader leitor in new[] { noPainel, naPublicacao })
        {
            await leitor.Received(1).ObterAsync(ProcessoComCorrecaoIndependente.CorrecaoIndependente.Codigo, "v1", Arg.Any<CancellationToken>());
        }
    }
}
