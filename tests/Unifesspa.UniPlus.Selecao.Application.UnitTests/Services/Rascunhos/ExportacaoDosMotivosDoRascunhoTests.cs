namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

public sealed class ExportacaoDosMotivosDoRascunhoTests
{
    private readonly CenarioDoRascunho _cenario = new();

    [Fact]
    public async Task Exportar_MotivosDeTiposDistintos_ConsultaParesEDescreve()
    {
        MotivoDeDecisao primeiro = Motivo("ISENCAO_DA_TAXA", FundamentoIsencao.CadastroUnico.Codigo());
        MotivoDeDecisao segundo = Motivo("CLASSIFICACAO", null);
        _cenario.Motivos.ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<(string, string?)>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { primeiro, segundo });
        JsonObject documento = await Exportar("""{"motivosDeDecisao":[{"tipoDeDecisaoCodigo":"ISENCAO_DA_TAXA","codigo":"EXEMPLO"},{"tipoDeDecisaoCodigo":"CLASSIFICACAO","codigo":"EXEMPLO"},{"tipoDeDecisaoCodigo":"ISENCAO_DA_TAXA","codigo":"EXEMPLO"}]}""");
        JsonArray motivos = documento["motivosDeDecisao"]!.AsArray();
        Conferir(motivos[0]!.AsObject(), FundamentoIsencao.CadastroUnico.Codigo());
        Conferir(motivos[1]!.AsObject(), null);
        await _cenario.Motivos.Received(1).ListarVivosPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<(string, string?)>>(pares => pares.Count == 2 && pares.Contains(new ValueTuple<string, string?>("EXEMPLO", "ISENCAO_DA_TAXA")) && pares.Contains(new ValueTuple<string, string?>("EXEMPLO", "CLASSIFICACAO"))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exportar_MotivoSemCadastro_OmiteDescritivos()
    {
        JsonObject documento = await Exportar("""{"motivosDeDecisao":[{"tipoDeDecisaoCodigo":"CLASSIFICACAO","codigo":"AUSENTE"}]}""");
        JsonObject motivo = documento["motivosDeDecisao"]![0]!.AsObject();
        motivo.Select(static propriedade => propriedade.Key).Should().Equal("tipoDeDecisaoCodigo", "codigo", "versaoDoEditalDeEntrada");
        motivo["versaoDoEditalDeEntrada"]!.GetValue<int>().Should().Be(1);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"motivosDeDecisao\":[]}")]
    public async Task Exportar_SemMotivos_NaoConsulta(string conteudo)
    {
        await Exportar(conteudo);
        await _cenario.Motivos.DidNotReceive().ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<(string, string?)>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exportar_VersaoNaoDeclaradaComoSomenteLeitura_OmiteVersao()
    {
        _cenario.Esquema.Forma.Returns(FormaDeTeste.Objeto(new()
        {
            ["motivosDeDecisao"] = FormaDeTeste.Lista(FormaDeTeste.Objeto(new()
            {
                ["tipoDeDecisaoCodigo"] = FormaDeTeste.Valor(),
                ["codigo"] = FormaDeTeste.Valor(),
                ["versaoDoEditalDeEntrada"] = FormaDeTeste.Valor(),
            })),
        }));
        JsonObject documento = await Exportar("""{"motivosDeDecisao":[{"tipoDeDecisaoCodigo":"CLASSIFICACAO","codigo":"AUSENTE"}]}""");
        documento["motivosDeDecisao"]![0]!["versaoDoEditalDeEntrada"].Should().BeNull();
    }

    [Fact]
    public async Task Exportar_Municipios_ComNomeGuardadoOuAusente()
    {
        JsonObject documento = await Exportar("""{"provaPresencial":{"municipios":[{"codigoIbge":"1504208","uf":"PA"},{"codigoIbge":"1501402","nome":null,"uf":"PA"},{"codigoIbge":"1504208","nome":"Marabá","uf":"PA"}]}}""");
        documento["provaPresencial"]!["municipios"]![0]!.AsObject().Select(static propriedade => propriedade.Key)
            .Should().Equal("codigoIbge", "uf");
        documento["provaPresencial"]!["municipios"]![1]!.AsObject().ContainsKey("nome").Should().BeFalse();
        documento["provaPresencial"]!["municipios"]![2]!["nome"]!.GetValue<string>().Should().Be("Marabá");
    }

    private async Task<JsonObject> Exportar(string conteudo)
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, conteudo);
        return await _cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);
    }

    private static MotivoDeDecisao Motivo(string tipo, string? subclassificacao) =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(tipo, "EXEMPLO", "Descrição fictícia", subclassificacao, SentidoDoMotivo.Favoravel)).Value!;

    private static void Conferir(JsonObject motivo, string? subclassificacao)
    {
        motivo["descricao"]!.GetValue<string>().Should().Be("Descrição fictícia");
        motivo["subclassificacao"]?.GetValue<string>().Should().Be(subclassificacao);
        motivo.ContainsKey("subclassificacao").Should().Be(subclassificacao is not null);
        motivo["sentido"]!.GetValue<string>().Should().Be("FAVORAVEL");
        motivo["sentidoRotulo"]!.GetValue<string>().Should().Be("favorável");
        motivo["versaoDoEditalDeEntrada"]!.GetValue<int>().Should().Be(1);
        motivo.Select(static propriedade => propriedade.Key).Take(3).Should().Equal("tipoDeDecisaoCodigo", "codigo", "descricao");
    }
}
