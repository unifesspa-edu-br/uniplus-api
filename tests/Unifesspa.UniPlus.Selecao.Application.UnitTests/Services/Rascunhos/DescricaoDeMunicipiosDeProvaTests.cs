namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

/// <summary>
/// O nome de cada município de prova vem do Geo, uma consulta por município; o município que o Geo não conhece fica sem nome, e
/// o Geo fora do ar ou a lista acima do limite deixam todos sem nome, sem erro.
/// </summary>
public sealed class DescricaoDeMunicipiosDeProvaTests
{
    private const string Maraba = "1504208";
    private const string Parauapebas = "1505502";
    private const string CodigoDesconhecido = "1599999";

    private readonly IConferenciaEnderecoGeo _geo = Substitute.For<IConferenciaEnderecoGeo>();

    private static FormaDoNo FormaDoDocumento() => FormaDeTeste.Objeto(new()
    {
        ["provaPresencial"] = FormaDeTeste.Objeto(new()
        {
            ["temProvaPresencial"] = FormaDeTeste.Valor(),
            ["municipios"] = FormaDeTeste.Lista(FormaDeTeste.Objeto(new()
            {
                ["codigoIbge"] = FormaDeTeste.Valor(),
                ["nome"] = FormaDeTeste.Valor(somenteLeitura: true),
                ["uf"] = FormaDeTeste.Valor(),
            })),
        }),
    });

    private static string DocumentoCom(params string[] codigos) =>
        DocumentoComUfs([.. codigos.Select(static codigo => (codigo, "PA"))]);

    private static string DocumentoComUfs(params (string Codigo, string Uf)[] municipios) =>
        $$"""{ "provaPresencial": { "temProvaPresencial": true, "municipios": [{{string.Join(", ", municipios.Select(static municipio => $$"""{ "codigoIbge": "{{municipio.Codigo}}", "uf": "{{municipio.Uf}}" }"""))}}] } }""";

    private Task<JsonObject> Descrever(string documento, int limite = 100) =>
        DocumentoDescritoDeTeste.DescreverAsync(documento, FormaDoDocumento(), new DescricaoDeMunicipiosDeProva(
            new ConferenciaDosMunicipiosNoGeo(_geo), Options.Create(new ProvaPresencialOptions { LimiteDeMunicipios = limite })));

    private void OGeoConhece(string codigo, string nome) =>
        _geo.ConferirMunicipioAsync(codigo, "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Success(nome));

    private void OGeoRecusa(string codigo, string codigoDoErro) =>
        _geo.ConferirMunicipioAsync(codigo, "PA", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new DomainError(codigoDoErro, "O Geo não conferiu o município.")));

    private static IEnumerable<string?> Nomes(JsonObject documento) => documento["provaPresencial"]!["municipios"]!.AsArray()
        .Select(static municipio => municipio!["nome"]?.GetValue<string>());

    [Fact(DisplayName = "Cada município traz o nome que o Geo devolve, logo depois do código IBGE e antes da UF")]
    public async Task Descrever_MunicipiosConhecidos_EscreveONomeAoLadoDoCodigo()
    {
        OGeoConhece(Maraba, "Marabá");
        OGeoConhece(Parauapebas, "Parauapebas");

        JsonObject documento = await Descrever(DocumentoCom(Maraba, Parauapebas));

        Nomes(documento).Should().Equal("Marabá", "Parauapebas");
        DocumentoDescritoDeTeste.Chaves(documento["provaPresencial"]!["municipios"]![0]).Should().Equal("codigoIbge", "nome", "uf");
    }

    [Fact(DisplayName = "O município que o Geo não conhece fica sem nome e os demais mantêm o seu")]
    public async Task Descrever_MunicipioDesconhecido_OmiteSoOSeuNome()
    {
        OGeoConhece(Maraba, "Marabá");
        OGeoRecusa(CodigoDesconhecido, ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado);
        OGeoConhece(Parauapebas, "Parauapebas");

        JsonObject documento = await Descrever(DocumentoCom(Maraba, CodigoDesconhecido, Parauapebas));

        Nomes(documento).Should().Equal("Marabá", null, "Parauapebas");
    }

    [Fact(DisplayName = "O Geo fora do ar deixa todos os municípios sem nome, interrompe a consulta e não gera erro")]
    public async Task Descrever_GeoIndisponivel_OmiteTodosOsNomes()
    {
        OGeoRecusa(Maraba, ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);

        JsonObject documento = await Descrever(DocumentoCom(Maraba, Parauapebas));

        Nomes(documento).Should().Equal(null, null);
        await _geo.DidNotReceive().ConferirMunicipioAsync(Parauapebas, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A lista acima do limite de municípios não chama o Geo e fica sem nomes")]
    public async Task Descrever_AcimaDoLimite_NaoConsultaOGeo()
    {
        JsonObject documento = await Descrever(DocumentoCom(Maraba, Parauapebas), limite: 1);

        Nomes(documento).Should().Equal(null, null);
        await _geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);
    }

    [Fact(DisplayName = "O limite conta municípios distintos: o mesmo código com duas UFs não faz passar do limite")]
    public async Task Descrever_CodigoRepetidoComOutraUf_ContaUmMunicipioSo()
    {
        OGeoConhece(Parauapebas, "Parauapebas");

        JsonObject documento = await Descrever(DocumentoComUfs((Maraba, "PA"), (Maraba, "AP"), (Parauapebas, "PA")), limite: 2);

        Nomes(documento).Should().Equal(null, null, "Parauapebas");
    }

    [Fact(DisplayName = "A lista que chega ao limite de municípios, sem passar dele, recebe os nomes")]
    public async Task Descrever_NoLimite_ConsultaOGeo()
    {
        OGeoConhece(Maraba, "Marabá");
        OGeoConhece(Parauapebas, "Parauapebas");

        JsonObject documento = await Descrever(DocumentoCom(Maraba, Parauapebas), limite: 2);

        Nomes(documento).Should().Equal("Marabá", "Parauapebas");
    }

    [Fact(DisplayName = "O município repetido é consultado uma só vez e todas as ocorrências recebem o nome")]
    public async Task Descrever_MunicipioRepetido_ConsultaOGeoUmaVez()
    {
        OGeoConhece(Maraba, "Marabá");

        JsonObject documento = await Descrever(DocumentoCom(Maraba, Maraba));

        Nomes(documento).Should().Equal("Marabá", "Marabá");
        await _geo.Received(1).ConferirMunicipioAsync(Maraba, "PA", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O código IBGE declarado com UFs diferentes fica sem nome em todas as ocorrências e não é consultado")]
    public async Task Descrever_MesmoCodigoComUfsDiferentes_NaoDaNomeANenhumaOcorrencia()
    {
        OGeoConhece(Maraba, "Marabá");
        OGeoConhece(Parauapebas, "Parauapebas");

        JsonObject documento = await Descrever(DocumentoComUfs((Maraba, "PA"), (Parauapebas, "PA"), (Maraba, "AP")));

        Nomes(documento).Should().Equal(null, "Parauapebas", null);
        await _geo.DidNotReceive().ConferirMunicipioAsync(Maraba, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O documento sem município de prova não consulta o Geo")]
    public async Task Descrever_SemMunicipios_NaoConsultaOGeo()
    {
        await Descrever("""{ "provaPresencial": { "temProvaPresencial": false, "municipios": [] } }""");

        await _geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);
    }
}
