namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class DescricaoDeBasesLegaisDaFadaTests
{
    private const string DocumentoComBaseLegal = """{ "fada": { "aplica": true, "baseLegalCodigo": "LEI_ESTADUAL_EXEMPLO" } }""";

    private readonly IBaseLegalDoFadaReader _bases = Substitute.For<IBaseLegalDoFadaReader>();

    public DescricaoDeBasesLegaisDaFadaTests() =>
        _bases.ObterPorCodigoAsync("LEI_ESTADUAL_EXEMPLO", Arg.Any<CancellationToken>()).Returns(new BaseLegalDoFadaView(
            Guid.CreateVersion7(), "LEI_ESTADUAL_EXEMPLO", "LEI", "Lei estadual de exemplo", "Atenua as desigualdades de acesso na região.",
            [new("1504208", "Marabá", "PA"), new("1505502", "Parauapebas", "PA")]));

    [Fact(DisplayName = "A base legal da FADA traz o instrumento, a identificação, a descrição e os municípios com código IBGE, nome e UF, na ordem do cadastro")]
    public async Task Descrever_BaseLegalViva_TrazOsDadosEOsMunicipiosDoCadastro()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComBaseLegal, new DescricaoDeBasesLegaisDaFada(_bases));

        JsonNode? fada = documento["fada"];
        DocumentoDescritoDeTeste.Chaves(fada).Should().Equal(
            "aplica", "baseLegalCodigo", "tipoInstrumento", "identificacao", "descricao", "municipios");
        fada!["tipoInstrumento"]!.GetValue<string>().Should().Be("LEI");
        fada["identificacao"]!.GetValue<string>().Should().Be("Lei estadual de exemplo");
        fada["descricao"]!.GetValue<string>().Should().Be("Atenua as desigualdades de acesso na região.");
        fada["municipios"]!.AsArray().Select(static municipio => (municipio!["codigoIbge"]!.GetValue<string>(), municipio["nome"]!.GetValue<string>(), municipio["uf"]!.GetValue<string>()))
            .Should().Equal(("1504208", "Marabá", "PA"), ("1505502", "Parauapebas", "PA"));
    }

    [Fact(DisplayName = "A base legal que o cadastro não devolve viva deixa a FADA sem os descritivos")]
    public async Task Descrever_BaseLegalSemCadastroVivo_OmiteOsDescritivos()
    {
        const string documentoComBaseRemovida = """{ "fada": { "aplica": true, "baseLegalCodigo": "BASE_REMOVIDA" } }""";

        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(documentoComBaseRemovida, new DescricaoDeBasesLegaisDaFada(_bases));

        DocumentoDescritoDeTeste.Chaves(documento["fada"]).Should().Equal("aplica", "baseLegalCodigo");
    }

    [Fact(DisplayName = "A FADA que não cita base legal não consulta o cadastro")]
    public async Task Descrever_FadaSemBaseLegal_NaoConsultaOCadastro()
    {
        await DocumentoDescritoDeTeste.DescreverAsync("""{ "fada": { "aplica": false } }""", new DescricaoDeBasesLegaisDaFada(_bases));

        await _bases.DidNotReceiveWithAnyArgs().ObterPorCodigoAsync(default!, default);
    }

    [Fact(DisplayName = "Cada exportação monta os municípios de novo, sem repartir o mesmo nó entre documentos")]
    public async Task Descrever_DoisDocumentos_NaoCompartilhamOsMunicipios()
    {
        DescricaoDeBasesLegaisDaFada descricao = new(_bases);

        JsonObject primeiro = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComBaseLegal, descricao);
        JsonObject segundo = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComBaseLegal, descricao);

        primeiro["fada"]!["municipios"]!.AsArray().RemoveAt(0);
        segundo["fada"]!["municipios"]!.AsArray().Should().HaveCount(2);
    }
}
