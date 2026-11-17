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
/// A versão vigente que o codec não sabe ler, percorrendo o fluxo inteiro da retificação. A retificação lê os fatos já
/// coletados das versões publicadas, pelo mesmo codec, e a recópia já recusa ali.
/// </summary>
public sealed class CodecIlegivelNoFluxoDoCongelamentoTests
{
    private const string CodigoDoCodec = "EnvelopeCodec.VersaoDesconhecida";

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

    [Fact(DisplayName = "Na retificação de ato único, a recusa do catálogo sai junto com o erro do codec, nessa ordem")]
    public async Task Retificacao_RecusaDoCatalogoAcumulaComOErroDoCodec()
    {
        CenarioDeRecusaNoCongelamento cenario = Ilegivel(OperacaoQueCongela.Retificar).Com(DefeitoDoCongelamento.ProtecaoPendente);

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal([.. CausasDo(DefeitoDoCongelamento.ProtecaoPendente), CodigoDoCodec]);
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
