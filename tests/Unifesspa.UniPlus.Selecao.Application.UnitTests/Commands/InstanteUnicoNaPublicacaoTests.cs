namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;

/// <summary>
/// O comando que congela uma versão lê o relógio uma só vez, depois da trava do processo: a vigência da versão, o
/// ato entregue ao registro central e a divulgação valem o mesmo instante, e o tipo de ato vigente é o do dia desse instante
/// no fuso institucional, não o da data que o documento declara.
/// </summary>
public sealed class InstanteUnicoNaPublicacaoTests
{
    private static readonly DateTimeOffset UmaDoisMinutosDepoisDaMeiaNoiteEmBelem = new(2026, 3, 14, 3, 2, 0, TimeSpan.Zero);

    [Theory(DisplayName = "O comando lê o relógio uma vez e usa o mesmo instante na versão, no ato e no ato entregue à porta")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    [InlineData(OperacaoQueCongela.FecharRetificacao)]
    public async Task Congelamento_LeORelogioUmaVez_EOInstanteVaiParaAVersaoOAtoEAPorta(OperacaoQueCongela operacao)
    {
        RelogioDeLeituraContada relogio = new(UmaDoisMinutosDepoisDaMeiaNoiteEmBelem);
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        cenario.Servicos.ComRelogio(relogio);
        Gravacao gravado = await Gravacao.CapturarAsync(cenario.Servicos);

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        relogio.Leituras.Should().Be(1);
        gravado.Versao!.VigenteAPartirDe.Should().Be(UmaDoisMinutosDepoisDaMeiaNoiteEmBelem);
        gravado.Ato!.PublicadoEm.Should().Be(UmaDoisMinutosDepoisDaMeiaNoiteEmBelem);
        gravado.Entregue!.PublicadoEm.Should().Be(UmaDoisMinutosDepoisDaMeiaNoiteEmBelem);
    }

    [Theory(DisplayName = "O tipo de ato é consultado no dia do instante em Belém, e a data declarada pelo documento não entra na consulta")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    [InlineData(OperacaoQueCongela.FecharRetificacao)]
    public async Task Congelamento_ConsultaOTipoNoDiaDoInstanteEmBelem(OperacaoQueCongela operacao)
    {
        DateTimeOffset vinteETresEMeiaEmBelem = InstanteEmBelem.Em(2026, 3, 13, 23, 30);
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        cenario.Servicos.ComRelogio(new RelogioFixo(vinteETresEMeiaEmBelem));

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        vinteETresEMeiaEmBelem.UtcDateTime.Date.Should().Be(new DateTime(2026, 3, 14), "o dia em UTC já é o seguinte ao de Belém");
        DiasConsultadosNoCatalogo(cenario).Should().NotBeEmpty().And.OnlyContain(dia => dia == new DateOnly(2026, 3, 13));
    }

    [Fact(DisplayName = "Depois da meia-noite em Belém o tipo de ato é consultado no dia seguinte")]
    public async Task Publicacao_AposAMeiaNoiteEmBelem_ConsultaOTipoNoDiaSeguinte()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Publicar);
        cenario.Servicos.ComRelogio(new RelogioFixo(UmaDoisMinutosDepoisDaMeiaNoiteEmBelem));

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        DiasConsultadosNoCatalogo(cenario).Should().NotBeEmpty().And.OnlyContain(dia => dia == new DateOnly(2026, 3, 14));
    }

    [Theory(DisplayName = "Sem versão do tipo vigente no dia da publicação, a recusa nomeia esse dia e nada é gravado")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    [InlineData(OperacaoQueCongela.FecharRetificacao)]
    public async Task Congelamento_SemTipoVigenteNoDiaDaPublicacao_RecusaCitandoODia(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        cenario.Servicos.ComRelogio(new RelogioFixo(InstanteEmBelem.Em(2026, 3, 13, 23, 30)));
        cenario.Servicos.TiposDeAto.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);

        (Result resposta, IEnumerable<object> mensagens) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoSemVersaoVigente");
        resposta.Error.Message.Should().Contain("data da publicação (2026-03-13)");
        mensagens.Should().BeEmpty();
        await cenario.Servicos.Atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
    }

    private static IEnumerable<DateOnly> DiasConsultadosNoCatalogo(CenarioDeRecusaNoCongelamento cenario) =>
        cenario.Servicos.TiposDeAto.ReceivedCalls()
            .Where(chamada => chamada.GetMethodInfo().Name is "ObterVigenteAsync" or "ListarVigentesAsync")
            .Select(chamada => chamada.GetArguments().OfType<DateOnly>().Single());

    private sealed class Gravacao
    {
        public VersaoConfiguracao? Versao { get; private set; }

        public AtoDoProcesso? Ato { get; private set; }

        public AtoNormativoPublicado? Entregue { get; private set; }

        public static async Task<Gravacao> CapturarAsync(ServicosDoCongelamentoDeTeste servicos)
        {
            Gravacao gravacao = new();
            await servicos.Repositorio.AdicionarVersaoConfiguracaoAsync(Arg.Do<VersaoConfiguracao>(v => gravacao.Versao = v), Arg.Any<CancellationToken>());
            await servicos.Atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravacao.Ato = a), Arg.Any<CancellationToken>());
            servicos.Registro.RegistrarAsync(Arg.Do<AtoNormativoPublicado>(a => gravacao.Entregue = a), Arg.Any<CancellationToken>())
                .Returns(Result.Success());

            return gravacao;
        }
    }
}
