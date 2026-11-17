namespace Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

using ResultadoInvalidoDaAutorizacao = Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes.ResultadoDaVerificacaoInvalidoException;
using ResultadoInvalidoDaSelecao = Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao.ResultadoDaVerificacaoInvalidoException;

/// <summary>
/// O mesmo tipo de mensagem tem uma cadeia por handler preso à fila. A reentrega e a transação da Autorização escolhem a
/// cadeia pelo mesmo predicado, então as regras de falha da cadeia da Seleção mostram que ela fica de fora das duas: a
/// transação o Wolverine não aplica duas vezes à mesma cadeia, e a contagem dela não distingue a origem.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class PoliticasDaCadeiaDoResultadoDaVerificacaoTests
{
    private const string FilaMorta = "Move to error queue";
    private const string FrameDaTransacao = "EnrollDbContextInTransaction";

    private readonly HandlerChain _daAutorizacao;
    private readonly HandlerChain _daSelecao;

    public PoliticasDaCadeiaDoResultadoDaVerificacaoTests(MonolitoDaInstalacaoFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        HandlerGraph grafo = fixture.Factory.Services.GetRequiredService<HandlerGraph>();
        HandlerChain[] cadeias = [.. grafo.AllChains().Where(c => c.MessageType == typeof(ResultadoDaVerificacaoDeArquivo))];
        _daAutorizacao = DoHandler(cadeias, typeof(RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler));
        _daSelecao = DoHandler(cadeias, typeof(RegistrarResultadoDaVerificacaoDoArquivoHandler));
    }

    [Fact(DisplayName = "A cadeia da Autorização tem duas regras de falha: o resultado inválido primeiro, depois qualquer exceção")]
    public void Autorizacao_TemDuasRegrasNaOrdem()
    {
        FailureRule[] regras = [.. _daAutorizacao.Failures];

        regras.Should().HaveCount(2);
        regras[0].Match.Matches(new ResultadoInvalidoDaAutorizacao()).Should().BeTrue();
        regras[0].Match.Matches(new InvalidOperationException()).Should().BeFalse("a primeira regra é só do resultado inválido");
        regras[1].Match.Matches(new TimeoutException()).Should().BeTrue("a segunda regra pega a falha transitória");
        regras[1].Match.Matches(new InvalidOperationException()).Should().BeTrue("a segunda regra pega exceção de qualquer outro tipo");
    }

    [Fact(DisplayName = "O resultado inválido vai direto para a fila morta, sem retentativa")]
    public void ResultadoInvalido_VaiDiretoParaAFilaMorta()
    {
        Acoes(_daAutorizacao.Failures.First()).Should().Equal(FilaMorta);
    }

    [Fact(DisplayName = "Qualquer outra exceção tem retentativas com espera de 200 ms, 1 s e 5 s e depois vai para a fila morta")]
    public void OutraExcecao_ReentregaComEsperaEDepoisFilaMorta()
    {
        Acoes(_daAutorizacao.Failures.Last()).Should().Equal(ReentregaComEsperaEDepoisFilaMorta());
    }

    [Fact(DisplayName = "O resultado da verificação da Autorização roda enrolado numa só transação do banco do módulo")]
    public void Autorizacao_RodaEmUmaTransacao()
    {
        FramesDaTransacao(_daAutorizacao).Should().Be(1);
    }

    [Fact(DisplayName = "A cadeia da Seleção não recebe a reentrega da Autorização: tem só as duas regras dela")]
    public void Selecao_NaoRecebeAReentregaDaAutorizacao()
    {
        FailureRule[] regras = [.. _daSelecao.Failures];

        regras.Should().HaveCount(2);
        regras[0].Match.Matches(new ResultadoInvalidoDaSelecao()).Should().BeTrue();
        regras[0].Match.Matches(new ResultadoInvalidoDaAutorizacao()).Should().BeFalse("o resultado inválido da Autorização é outro tipo");
        regras[1].Match.Matches(new TimeoutException()).Should().BeTrue("a segunda regra pega a falha transitória");
        regras[1].Match.Matches(new InvalidOperationException()).Should().BeTrue("a segunda regra pega exceção de qualquer outro tipo");
        Acoes(regras[0]).Should().Equal(FilaMorta);
        Acoes(regras[1]).Should().Equal(ReentregaComEsperaEDepoisFilaMorta());
    }

    [Fact(DisplayName = "A cadeia da Seleção roda numa só transação, a ambiente que o Wolverine aplica aos handlers que gravam")]
    public void Selecao_RodaNumaSoTransacao()
    {
        FramesDaTransacao(_daSelecao).Should().Be(1);
    }

    private static HandlerChain DoHandler(HandlerChain[] cadeias, Type handler) =>
        cadeias.Single(c => c.Handlers.Any(h => h.HandlerType == handler));

    private static string[] Acoes(FailureRule regra) => [.. regra.Select(slot => slot.Describe())];

    private static string[] ReentregaComEsperaEDepoisFilaMorta() =>
    [
        EsperaDaRetentativa(TimeSpan.FromMilliseconds(200)),
        EsperaDaRetentativa(TimeSpan.FromSeconds(1)),
        EsperaDaRetentativa(TimeSpan.FromSeconds(5)),
        FilaMorta,
    ];

    private static string EsperaDaRetentativa(TimeSpan espera) => $"Retry inline with a delay of {espera}";

    private static int FramesDaTransacao(HandlerChain cadeia) =>
        cadeia.Middleware.Count(frame => frame.GetType().Name == FrameDaTransacao);
}
