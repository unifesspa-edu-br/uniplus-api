namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// A edição das etapas na retificação recusa a remoção da etapa que já começou, conferida contra a versão
/// publicada vigente (UNI-REQ-0364), com a mesma recusa que o fechamento da sessão devolve. A leitura das fases
/// iniciadas só acontece com a sessão aberta, no instante lido uma vez, e depois das recusas que a antecedem.
/// </summary>
public sealed class DefinirEtapasRemocaoIniciadaTests
{
    private const string CodigoDaRemocao = "FaseCronograma.EtapaIniciadaNaoSaiDoCronograma";
    private static readonly DateTimeOffset Instante = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Na retificação, remover a etapa de uma fase iniciada é recusado com a recusa do fechamento")]
    public async Task Handle_EtapaIniciadaRemovida_RecusaComAMesmaRecusaDoFechamento()
    {
        (CenarioDasEtapas cenario, ProcessoSeletivo processo) = RetificacaoAberta();
        FasesIniciadas iniciadas = IniciadasComAEtapaDe(processo);
        cenario.FasesIniciadas = FasesIniciadasDeTeste.Com(iniciadas);

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(SemEtapas(processo, PrecondicaoIfMatch.Curinga));

        resultado.Errors.Should().ContainSingle().Which.Error.Should().Be(iniciadas.RecusaDaRemocaoDaEtapa([]));
        resultado.Error!.Code.Should().Be(CodigoDaRemocao);
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Na retificação, remover a etapa de uma fase que não começou é aceito")]
    public async Task Handle_EtapaNaoIniciadaRemovida_Aceita()
    {
        (CenarioDasEtapas cenario, ProcessoSeletivo processo) = RetificacaoAberta();

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(SemEtapas(processo, PrecondicaoIfMatch.Curinga));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().BeEmpty();
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Na retificação, a etapa iniciada que o payload mantém não é remoção")]
    public async Task Handle_EtapaIniciadaMantida_Aceita()
    {
        (CenarioDasEtapas cenario, ProcessoSeletivo processo) = RetificacaoAberta();
        cenario.FasesIniciadas = FasesIniciadasDeTeste.Com(IniciadasComAEtapaDe(processo));

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            new DefinirEtapasCommand(processo.Id, [ReenvioDaEtapa(processo.Etapas.Single())], PrecondicaoIfMatch.Curinga));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().ContainSingle();
    }

    [Fact(DisplayName = "Na retificação, a etapa iniciada mantida com o mesmo id aceita a mudança de nome e de ordem")]
    public async Task Handle_EtapaIniciadaMantidaComCamposAlterados_Aceita()
    {
        (CenarioDasEtapas cenario, ProcessoSeletivo processo) = RetificacaoAberta();
        EtapaProcesso iniciada = processo.Etapas.Single();
        cenario.FasesIniciadas = FasesIniciadasDeTeste.Com(IniciadasDa(iniciada));

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            new DefinirEtapasCommand(processo.Id, [ReenvioDaEtapa(iniciada, "Prova Objetiva — 1ª fase", ordem: 2)], PrecondicaoIfMatch.Curinga));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        (iniciada.Nome, iniciada.Ordem).Should().Be(("Prova Objetiva — 1ª fase", 2));
    }

    [Fact(DisplayName = "A remoção iniciada recusada depois de reconciliar outra etapa descarta a mutação e nunca grava")]
    public async Task Handle_RemocaoIniciadaAposReconciliarOutraEtapa_DescartaSemGravar()
    {
        (CenarioDasEtapas cenario, ProcessoSeletivo processo) = RetificacaoAberta();
        EtapaProcesso reconciliada = processo.Etapas.Single();
        cenario.FasesIniciadas = FasesIniciadasDeTeste.Com(IniciadasDa(AcrescentarEtapaDeRedacao(processo)));

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            new DefinirEtapasCommand(processo.Id, [ReenvioDaEtapa(reconciliada, "Prova Objetiva — 1ª fase")], PrecondicaoIfMatch.Curinga));

        resultado.Error!.Code.Should().Be(CodigoDaRemocao);
        reconciliada.Nome.Should().Be("Prova Objetiva — 1ª fase", "a reconciliação já mutou a instância rastreada");
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem sessão de retificação aberta, as fases iniciadas não são lidas")]
    public async Task Handle_SemSessaoAberta_NaoLeAsFasesIniciadas()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        IFasesIniciadasDoProcesso leitor = FasesIniciadasDeTeste.Com(IniciadasComAEtapaDe(processo));
        CenarioDasEtapas cenario = CenarioDasEtapas.Para(processo);
        cenario.FasesIniciadas = leitor;

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(SemEtapas(processo, PrecondicaoIfMatch.Ausente));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await leitor.DidNotReceiveWithAnyArgs().ObterAsync(default, default, default);
    }

    [Fact(DisplayName = "Com a sessão aberta, as fases iniciadas são lidas uma vez, no instante em que o relógio foi lido uma vez")]
    public async Task Handle_SessaoAberta_LeUmaVezNoInstanteDoRelogio()
    {
        (CenarioDasEtapas cenario, ProcessoSeletivo processo) = RetificacaoAberta();
        TimeProvider relogio = Substitute.For<TimeProvider>();
        relogio.GetUtcNow().Returns(Instante);
        IFasesIniciadasDoProcesso leitor = FasesIniciadasDeTeste.Nenhuma();
        (cenario.Relogio, cenario.FasesIniciadas) = (relogio, leitor);

        await cenario.DefinirAsync(SemEtapas(processo, PrecondicaoIfMatch.Curinga));

        await leitor.Received(1).ObterAsync(processo.Id, Instante, Arg.Any<CancellationToken>());
        relogio.Received(1).GetUtcNow();
    }

    [Fact(DisplayName = "A precondição recusada vem antes da leitura das fases iniciadas")]
    public async Task Handle_PrecondicaoAusente_RecusaAntesDeLerAsFasesIniciadas()
    {
        (CenarioDasEtapas cenario, ProcessoSeletivo processo) = RetificacaoAberta();
        IFasesIniciadasDoProcesso leitor = FasesIniciadasDeTeste.Com(IniciadasComAEtapaDe(processo));
        cenario.FasesIniciadas = leitor;

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(SemEtapas(processo, PrecondicaoIfMatch.Ausente));

        resultado.Error!.Code.Should().Be("Precondicao.Requerida");
        await leitor.DidNotReceiveWithAnyArgs().ObterAsync(default, default, default);
    }

    [Fact(DisplayName = "A recusa dos cadastros vem antes da leitura das fases iniciadas")]
    public async Task Handle_TipoDeEtapaInexistente_RecusaAntesDeLerAsFasesIniciadas()
    {
        (CenarioDasEtapas cenario, ProcessoSeletivo processo) = RetificacaoAberta();
        IFasesIniciadasDoProcesso leitor = FasesIniciadasDeTeste.Com(IniciadasComAEtapaDe(processo));
        cenario.FasesIniciadas = leitor;

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(ComEtapasNovas(processo, Guid.CreateVersion7(), ordens: [1]));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        await leitor.DidNotReceiveWithAnyArgs().ObterAsync(default, default, default);
    }

    private static (CenarioDasEtapas Cenario, ProcessoSeletivo Processo) RetificacaoAberta()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        AbrirRetificacao(processo, versaoAtual);
        return (CenarioDasEtapas.Para(processo), processo);
    }

    private static FasesIniciadas IniciadasComAEtapaDe(ProcessoSeletivo processo) => IniciadasDa(processo.Etapas.Single());

    private static FasesIniciadas IniciadasDa(EtapaProcesso etapa) =>
        new([], etapas: [new EtapaIniciada(etapa.Id, etapa.Nome, NaVersaoVigente: true)]);

    private static EtapaProcesso AcrescentarEtapaDeRedacao(ProcessoSeletivo processo)
    {
        EtapaProcesso existente = processo.Etapas.Single();
        TipoEtapaSnapshot redacao = TipoEtapaSnapshot.Criar(
            CenarioDasEtapas.TipoRedacaoOrigemId, "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        EtapaProcesso acrescentada = EtapaProcesso.Criar(
            "Redação", CaraterEtapa.Classificatoria, redacao, peso: 1m, ordem: 2, notaMaxima: 10m,
            formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: existente.FaseCodigo).Value!;
        processo.DefinirEtapas([existente, acrescentada], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        return acrescentada;
    }

    private static DefinirEtapasCommand SemEtapas(ProcessoSeletivo processo, PrecondicaoIfMatch precondicao) =>
        new(processo.Id, [], precondicao);

    private static DefinirEtapasCommand ComEtapasNovas(ProcessoSeletivo processo, Guid tipoEtapaId, int[] ordens) =>
        new(
            processo.Id,
            [.. ordens.Select(ordem => new EtapaProcessoInput(
                $"Etapa {ordem}", CaraterEtapa.Classificatoria, tipoEtapaId, 1m, null, ordem, Produtos: [], Bancas: [], Recursos: []))],
            PrecondicaoIfMatch.Curinga);

    private static EtapaProcessoInput ReenvioDaEtapa(EtapaProcesso etapa, string? nome = null, int? ordem = null) => new(
        nome ?? etapa.Nome, etapa.Carater, etapa.TipoEtapaOrigemId, etapa.Peso, null, ordem ?? etapa.Ordem, etapa.Id,
        FaseCodigo: etapa.FaseCodigo, Produtos: [], Bancas: [], Recursos: [], NotaMaxima: etapa.NotaMaxima, FormaDeAvaliacao: "OBJETIVA");
}
