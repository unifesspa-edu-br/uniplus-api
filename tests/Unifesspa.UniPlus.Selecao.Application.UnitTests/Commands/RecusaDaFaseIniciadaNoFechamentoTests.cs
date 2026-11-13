namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;

/// <summary>
/// A fase que começa entre a abertura da sessão e o fechamento é recusada no ponto em que o handler transita o
/// agregado: depois da montagem da versão, antes de <c>FecharRetificacao</c>. A leitura das fases iniciadas só
/// acontece nesse ponto, no instante lido depois da trava, e não ocorre quando um portão anterior recusa.
/// </summary>
public sealed class RecusaDaFaseIniciadaNoFechamentoTests
{
    public static TheoryData<DefeitoDoCongelamento> PortoesAnteriores() =>
        [.. PorPrioridade(OperacaoQueCongela.FecharRetificacao)];

    [Fact(DisplayName = "A unidade trocada numa fase iniciada recusa depois da montagem e antes de fechar a sessão")]
    public async Task UnidadeTrocada_RecusaDepoisDaMontagemEAntesDeFechar()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao);
        Guid fase = cenario.Processo.CronogramaFases.Single().FaseCanonicaOrigemId;
        cenario.Servicos.ComFasesIniciadas(FasesIniciadasDeTeste.Com(new FasesIniciadas([KeyValuePair.Create<Guid, Guid?>(fase, UnidadesDeTeste.Cga.OrigemId)])));

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be("FaseCronograma.UnidadeDeFaseIniciadaNaoMuda");
        await AfirmarRecusadoDepoisDaMontagemAsync(cenario, eventos);
    }

    [Fact(DisplayName = "A etapa removida de uma fase iniciada recusa depois da montagem e antes de fechar a sessão")]
    public async Task EtapaRemovida_RecusaDepoisDaMontagemEAntesDeFechar()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao);
        FasesIniciadas iniciadas = new([], etapas: [new EtapaIniciada(Guid.CreateVersion7(), "Heteroidentificação", NaVersaoVigente: true)]);
        cenario.Servicos.ComFasesIniciadas(FasesIniciadasDeTeste.Com(iniciadas));

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be("FaseCronograma.EtapaIniciadaNaoSaiDoCronograma");
        await AfirmarRecusadoDepoisDaMontagemAsync(cenario, eventos);
    }

    [Fact(DisplayName = "Sem fase iniciada, a leitura acontece uma vez, no instante do relógio, e o fechamento segue")]
    public async Task SemFaseIniciada_LeUmaVezNoInstanteDoRelogio()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao);
        IFasesIniciadasDoProcesso leitor = FasesIniciadasDeTeste.Nenhuma();
        cenario.Servicos.ComFasesIniciadas(leitor);

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        await leitor.Received(1).ObterAsync(cenario.Processo.Id, PublicacaoDeTeste.Agora, Arg.Any<CancellationToken>());
        cenario.Processo.Rascunho.Should().BeNull("o fechamento encerra a sessão");
    }

    [Fact(DisplayName = "A leitura das fases iniciadas vem depois da canonicalização")]
    public async Task Leitura_VemDepoisDaCanonicalizacao()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao);
        IFasesIniciadasDoProcesso leitor = FasesIniciadasDeTeste.Nenhuma();
        cenario.Servicos.ComFasesIniciadas(leitor);

        await cenario.ExecutarAsync();

        Received.InOrder(() =>
        {
            _ = cenario.Servicos.Canonicalizer.Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
            _ = leitor.ObterAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        });
    }

    [Theory(DisplayName = "Quando um portão anterior recusa, as fases iniciadas não são lidas")]
    [MemberData(nameof(PortoesAnteriores))]
    public async Task PortaoAnterior_NaoLeAsFasesIniciadas(DefeitoDoCongelamento defeito)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao).Com(defeito);
        IFasesIniciadasDoProcesso leitor = Substitute.For<IFasesIniciadasDoProcesso>();
        cenario.Servicos.ComFasesIniciadas(leitor);

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be(CodigoDa(defeito));
        await leitor.DidNotReceiveWithAnyArgs().ObterAsync(default, default, default);
    }

    private static async Task AfirmarRecusadoDepoisDaMontagemAsync(CenarioDeRecusaNoCongelamento cenario, IEnumerable<object> eventos)
    {
        eventos.Should().BeEmpty();
        _ = cenario.Servicos.Canonicalizer.Received(1).Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        cenario.Processo.Rascunho.Should().NotBeNull("a sessão editorial só se encerra depois de a recusa da fase iniciada passar");
        cenario.AfirmarQueNaoTransitou();
        cenario.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.Servicos.Atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
        await cenario.Servicos.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
