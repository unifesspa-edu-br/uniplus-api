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
/// A fase que começa entre a abertura da sessão e o fechamento é recusada junto das demais causas da operação, antes de
/// a versão ser montada. A leitura das fases iniciadas acontece no instante lido depois da trava, se acumula com as
/// causas da conferência da configuração e do edital, e não ocorre quando o estado, o ato ou uma leitura de cadastro recusa sozinho o fechamento.
/// </summary>
public sealed class RecusaDaFaseIniciadaNoFechamentoTests
{
    public static TheoryData<DefeitoDoCongelamento> DefeitosQueRecusamSozinhos() =>
        [.. PorPrioridade(OperacaoQueCongela.FecharRetificacao).Where(RecusaSozinho)];

    [Fact(DisplayName = "A unidade trocada numa fase iniciada recusa antes da montagem, e a sessão segue aberta")]
    public async Task UnidadeTrocada_RecusaAntesDaMontagem()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao);
        Guid fase = cenario.Processo.CronogramaFases.Single().FaseCanonicaOrigemId;
        cenario.Servicos.ComFasesIniciadas(FasesIniciadasDeTeste.Com(new FasesIniciadas([KeyValuePair.Create<Guid, Guid?>(fase, UnidadesDeTeste.Cga.OrigemId)])));

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be("FaseCronograma.UnidadeDeFaseIniciadaNaoMuda");
        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be("cronograma.fases");
        await AfirmarRecusadoAntesDaMontagemAsync(cenario, eventos);
    }

    [Fact(DisplayName = "A etapa removida de uma fase iniciada recusa antes da montagem, e a sessão segue aberta")]
    public async Task EtapaRemovida_RecusaAntesDaMontagem()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao);
        FasesIniciadas iniciadas = new([], etapas: [new EtapaIniciada(Guid.CreateVersion7(), "Heteroidentificação", NaVersaoVigente: true)]);
        cenario.Servicos.ComFasesIniciadas(FasesIniciadasDeTeste.Com(iniciadas));

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be("FaseCronograma.EtapaIniciadaNaoSaiDoCronograma");
        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas");
        await AfirmarRecusadoAntesDaMontagemAsync(cenario, eventos);
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

    [Fact(DisplayName = "A leitura das fases iniciadas vem antes da canonicalização")]
    public async Task Leitura_VemAntesDaCanonicalizacao()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao);
        IFasesIniciadasDoProcesso leitor = FasesIniciadasDeTeste.Nenhuma();
        cenario.Servicos.ComFasesIniciadas(leitor);

        await cenario.ExecutarAsync();

        Received.InOrder(() =>
        {
            _ = leitor.ObterAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
            _ = cenario.Servicos.Canonicalizer.Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        });
    }

    [Fact(DisplayName = "A fase iniciada se acumula às causas da conferência da configuração, depois delas")]
    public async Task FaseIniciada_AcumulaComAsCausasDaConferencia()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao).Com(DefeitoDoCongelamento.Cronograma);
        Guid fase = cenario.Processo.CronogramaFases.Single().FaseCanonicaOrigemId;
        cenario.Servicos.ComFasesIniciadas(FasesIniciadasDeTeste.Com(new FasesIniciadas([KeyValuePair.Create<Guid, Guid?>(fase, UnidadesDeTeste.Cga.OrigemId)])));

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().ContainInOrder(
            CodigoDa(DefeitoDoCongelamento.Cronograma), "FaseCronograma.UnidadeDeFaseIniciadaNaoMuda");
    }

    [Theory(DisplayName = "Quando o estado, o ato ou uma leitura de cadastro recusa sozinho o fechamento, as fases iniciadas não são lidas")]
    [MemberData(nameof(DefeitosQueRecusamSozinhos))]
    public async Task RecusaSozinha_NaoLeAsFasesIniciadas(DefeitoDoCongelamento defeito)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao).Com(defeito);
        IFasesIniciadasDoProcesso leitor = Substitute.For<IFasesIniciadasDoProcesso>();
        cenario.Servicos.ComFasesIniciadas(leitor);

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal(CausasDo(defeito));
        await leitor.DidNotReceiveWithAnyArgs().ObterAsync(default, default, default);
    }

    private static async Task AfirmarRecusadoAntesDaMontagemAsync(CenarioDeRecusaNoCongelamento cenario, IEnumerable<object> eventos)
    {
        eventos.Should().BeEmpty();
        _ = cenario.Servicos.Canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        cenario.Processo.Rascunho.Should().NotBeNull("a sessão editorial só se encerra depois de a recusa da fase iniciada passar");
        cenario.AfirmarQueNaoTransitou();
        cenario.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.Servicos.Atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
        await cenario.Servicos.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
