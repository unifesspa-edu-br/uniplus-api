namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaClassificacao;

public sealed class DescricaoDePesosPorAreaDoEnemTests
{
    private static readonly string DocumentoComResolucao = $$"""{ "classificacao": { "baseadoEmEnem": true, "resolucaoPesoAreaEnem": "{{ResolucaoDePesos}}" } }""";

    private readonly IPesoAreaEnemReader _pesos = Substitute.For<IPesoAreaEnemReader>();

    private static FormaDoNo FormaDoDocumento() => FormaDeTeste.Objeto(new()
    {
        ["classificacao"] = FormaDeTeste.Objeto(new()
        {
            ["baseadoEmEnem"] = FormaDeTeste.Valor(),
            ["resolucaoPesoAreaEnem"] = FormaDeTeste.Valor(),
            ["quadroPesoAreaEnem"] = FormaDeTeste.Lista(FormaDoGrupo(), somenteLeitura: true),
        }),
    });

    private static FormaDoNo FormaDoGrupo() => FormaDeTeste.Objeto(new()
    {
        ["grupoAreaEnemCodigo"] = FormaDeTeste.Valor(),
        ["grupoAreaEnemRotulo"] = FormaDeTeste.Valor(),
        ["baseLegal"] = FormaDeTeste.Valor(),
        ["areas"] = FormaDeTeste.Lista(FormaDeTeste.Objeto(new()
        {
            ["codigo"] = FormaDeTeste.Valor(),
            ["rotulo"] = FormaDeTeste.Valor(),
            ["peso"] = FormaDeTeste.Valor(false, "number"),
            ["corte"] = FormaDeTeste.Valor(false, "number"),
        })),
    });

    private DescricaoDePesosPorAreaDoEnem CriarDescricao()
    {
        IEsquemaDoDocumentoDaConfiguracao esquema = Substitute.For<IEsquemaDoDocumentoDaConfiguracao>();
        esquema.Forma.Returns(FormaDoDocumento());
        return new(_pesos, esquema);
    }

    private Task<JsonObject> DescreverAsync(string documento) =>
        DocumentoDescritoDeTeste.DescreverAsync(documento, FormaDoDocumento(), CriarDescricao());

    [Fact(DisplayName = "A resolução completa traz o quadro com os quatro grupos do catálogo, na ordem do código, ao lado da resolução")]
    public async Task Descrever_ResolucaoCompleta_TrazUmItemPorGrupoComOsRotulosDoCatalogo()
    {
        _pesos.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>()).Returns(ResolucaoCom(GruposDoAnexoI));

        JsonObject documento = await DescreverAsync(DocumentoComResolucao);

        JsonNode? classificacao = documento["classificacao"];
        DocumentoDescritoDeTeste.Chaves(classificacao).Should().Equal("baseadoEmEnem", "resolucaoPesoAreaEnem", "quadroPesoAreaEnem");
        JsonArray quadro = classificacao!["quadroPesoAreaEnem"]!.AsArray();
        quadro.Select(static grupo => (grupo!["grupoAreaEnemCodigo"]!.GetValue<string>(), grupo["grupoAreaEnemRotulo"]!.GetValue<string>())).Should().Equal(
            ("HUMANISTICA_I", "Humanística I"), ("HUMANISTICA_II", "Humanística II"), ("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas"), ("TECNOLOGICA", "Tecnológica"));
        DocumentoDescritoDeTeste.Chaves(quadro[0]).Should().Equal("grupoAreaEnemCodigo", "grupoAreaEnemRotulo", "baseLegal", "areas");
        quadro[0]!["baseLegal"]!.GetValue<string>().Should().Be("Resolução nº 805/2024/Consepe – Anexo I (Humanística I)");
    }

    [Fact(DisplayName = "As áreas saem na ordem do código, com peso e corte de quatro casas e sem corte quando a área não tem")]
    public async Task Descrever_ResolucaoCompleta_TrazAsAreasOrdenadasComPesoECorteCanonicos()
    {
        _pesos.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>()).Returns(ResolucaoCom(GruposDoAnexoI));

        JsonObject documento = await DescreverAsync(DocumentoComResolucao);

        JsonArray areas = documento["classificacao"]!["quadroPesoAreaEnem"]![0]!["areas"]!.AsArray();
        areas.Select(static area => area!["codigo"]!.GetValue<string>()).Should().Equal(
            "CIENCIAS_DA_NATUREZA", "CIENCIAS_HUMANAS", "LINGUAGENS", "MATEMATICA", "REDACAO");
        areas[4]!.ToJsonString().Should().Be(JsonNode.Parse("""{"codigo":"REDACAO","rotulo":"Redação","peso":2.0000,"corte":400.0000}""")!.ToJsonString());
        areas[0]!.ToJsonString().Should().Be(JsonNode.Parse("""{"codigo":"CIENCIAS_DA_NATUREZA","rotulo":"Ciências da Natureza e suas Tecnologias","peso":1.5000}""")!.ToJsonString());
    }

    [Fact(DisplayName = "A resolução que o cadastro não conhece deixa a classificação sem o quadro, sem erro")]
    public async Task Descrever_ResolucaoInexistente_OmiteOQuadro()
    {
        _pesos.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>()).Returns((ResolucaoPesoAreaEnemView?)null);

        JsonObject documento = await DescreverAsync(DocumentoComResolucao);

        DocumentoDescritoDeTeste.Chaves(documento["classificacao"]).Should().Equal("baseadoEmEnem", "resolucaoPesoAreaEnem");
    }

    [Fact(DisplayName = "A resolução sem pesos para todos os grupos deixa a classificação sem o quadro, sem erro")]
    public async Task Descrever_ResolucaoIncompleta_OmiteOQuadro()
    {
        _pesos.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>()).Returns(ResolucaoCom(GruposDoAnexoI.Take(3), GruposDoAnexoI[3]));

        JsonObject documento = await DescreverAsync(DocumentoComResolucao);

        DocumentoDescritoDeTeste.Chaves(documento["classificacao"]).Should().Equal("baseadoEmEnem", "resolucaoPesoAreaEnem");
    }

    [Fact(DisplayName = "A resolução de forma recusada deixa a classificação sem o quadro e não consulta o cadastro")]
    public async Task Descrever_ResolucaoComCaractereNulo_OmiteOQuadroSemConsultar()
    {
        JsonObject documento = await DescreverAsync("""{ "classificacao": { "resolucaoPesoAreaEnem": "Res. 805\u0000/2024" } }""");

        DocumentoDescritoDeTeste.Chaves(documento["classificacao"]).Should().Equal("resolucaoPesoAreaEnem");
        await _pesos.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);
    }

    [Fact(DisplayName = "A classificação que não cita resolução não consulta o cadastro")]
    public async Task Descrever_ClassificacaoSemResolucao_NaoConsultaOCadastro()
    {
        await DescreverAsync("""{ "classificacao": { "baseadoEmEnem": false, "resolucaoPesoAreaEnem": null } }""");

        await _pesos.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);
    }

    [Fact(DisplayName = "Cada exportação lê o cadastro vivo de novo e traz o peso que ele tem no momento")]
    public async Task Descrever_PesoAlteradoNoCadastro_ASegundaExportacaoTrazOPesoNovo()
    {
        _pesos.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>()).Returns(ResolucaoCom(GruposDoAnexoI));
        DescricaoDePesosPorAreaDoEnem descricao = CriarDescricao();

        JsonObject primeira = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComResolucao, FormaDoDocumento(), descricao);
        _pesos.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>()).Returns(ResolucaoComPesoDeRedacao(3.25m));
        JsonObject segunda = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComResolucao, FormaDoDocumento(), descricao);

        PesoDaRedacao(primeira).Should().Be(2.00m);
        PesoDaRedacao(segunda).Should().Be(3.25m);
    }

    private static decimal PesoDaRedacao(JsonObject documento) => documento["classificacao"]!["quadroPesoAreaEnem"]![0]!["areas"]!.AsArray()
        .Single(static area => area!["codigo"]!.GetValue<string>() == "REDACAO")!["peso"]!.GetValue<decimal>();

    private static ResolucaoPesoAreaEnemView ResolucaoComPesoDeRedacao(decimal peso)
    {
        ResolucaoPesoAreaEnemView original = ResolucaoCom(GruposDoAnexoI);
        return original with
        {
            Linhas = [.. original.Linhas.Select(linha => linha with
            {
                Areas = [.. linha.Areas.Select(area => area.Codigo == "REDACAO" ? area with { Peso = peso } : area)],
            })],
        };
    }
}
