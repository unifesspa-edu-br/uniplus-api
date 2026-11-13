namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;

/// <summary>
/// A versão vigente que o codec não sabe ler, percorrendo o fluxo inteiro de cada ato que parte dela. O fechamento da
/// sessão tem os fatos já coletados guardados no rascunho e só esbarra no codec ao ler os tipos de ato congelados. A
/// retificação de ato único os lê das versões publicadas, pelo mesmo codec, e a recópia já recusa ali.
/// </summary>
public sealed class CodecIlegivelNoFluxoDoCongelamentoTests
{
    private const string CodigoDoCodec = "EnvelopeCodec.VersaoDesconhecida";

    public static TheoryData<DefeitoDoCongelamento> DefeitosAnterioresAosTiposDeAto() =>
        [DefeitoDoCongelamento.Cronograma, DefeitoDoCongelamento.UnidadeExcluida];

    [Fact(DisplayName = "No fechamento, a recópia ignora a versão ilegível: sem proteção da versão vigente, não recusa")]
    public async Task Fechamento_ARecopiaIgnoraAVersaoIlegivel()
    {
        CenarioDeRecusaNoCongelamento cenario = Ilegivel(OperacaoQueCongela.FecharRetificacao);
        ServicosDoCongelamentoDeTeste servicos = cenario.Servicos;
        Result<BaseDaRetificacao> baseLida = await servicos.LeituraDaBase().DaSessaoAbertaAsync(cenario.Processo, CancellationToken.None);
        CatalogoVivo catalogo = await servicos.Catalogos().LerAsync(cenario.Processo, CancellationToken.None);
        var origem = new FechamentoDaSessao(baseLida.Value!, cenario.Processo.Rascunho!.FatosDasVersoesPublicadas);

        Result<RecopiadoAoCongelar> recopiado = await servicos.Recopia().RecopiarAsync(cenario.Processo, catalogo, origem, CancellationToken.None);

        origem.CongeladoNaVersaoVigente.Should().BeNull();
        recopiado.IsSuccess.Should().BeTrue(recopiado.Error?.Message);
    }

    [Theory(DisplayName = "No fechamento, a recusa anterior aos tipos de ato tem prioridade sobre o erro do codec")]
    [MemberData(nameof(DefeitosAnterioresAosTiposDeAto))]
    public async Task Fechamento_RecusaAnteriorVenceOErroDoCodec(DefeitoDoCongelamento defeito)
    {
        CenarioDeRecusaNoCongelamento cenario = Ilegivel(OperacaoQueCongela.FecharRetificacao).Com(defeito);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be(CodigoDa(defeito));
        eventos.Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "No fechamento, satisfeitos os portões anteriores, a recusa é o erro do codec, único e sem campo")]
    public async Task Fechamento_ChegaAoErroDoCodec()
    {
        CenarioDeRecusaNoCongelamento cenario = Ilegivel(OperacaoQueCongela.FecharRetificacao);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        FieldError erro = resposta.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().BeNull();
        erro.Error.Code.Should().Be(CodigoDoCodec);
        eventos.Should().BeEmpty();
        await cenario.Servicos.TiposDeAto.DidNotReceiveWithAnyArgs().ListarVigentesAsync(default!, default, default);
        await cenario.Servicos.NadaFoiGravadoAsync();
        cenario.AfirmarQueNaoTransitou();
    }

    [Fact(DisplayName = "Na retificação de ato único, a recópia recusa com o erro do codec ao ler as versões publicadas, único e sem campo")]
    public async Task Retificacao_ARecopiaRecusaComOErroDoCodec()
    {
        CenarioDeRecusaNoCongelamento cenario = Ilegivel(OperacaoQueCongela.Retificar);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        FieldError erro = resposta.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().BeNull();
        erro.Error.Code.Should().Be(CodigoDoCodec);
        eventos.Should().BeEmpty();
        await cenario.Servicos.TiposDeAto.DidNotReceiveWithAnyArgs().ListarVigentesAsync(default!, default, default);
        await cenario.Servicos.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Na retificação de ato único, a recusa que precede a recópia tem prioridade sobre o erro do codec")]
    public async Task Retificacao_RecusaAnteriorVenceOErroDoCodec()
    {
        CenarioDeRecusaNoCongelamento cenario = Ilegivel(OperacaoQueCongela.Retificar).Com(DefeitoDoCongelamento.ProtecaoPendente);

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be(CodigoDa(DefeitoDoCongelamento.ProtecaoPendente));
        await cenario.Servicos.NadaFoiGravadoAsync();
    }

    private static CenarioDeRecusaNoCongelamento Ilegivel(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        var erroDoCodec = new DomainError(CodigoDoCodec, "A versão do envelope congelado não está no registro de codecs.");
        cenario.Servicos.Codecs.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(Result<EnvelopeReidratado>.Failure(erroDoCodec));

        return cenario;
    }
}
