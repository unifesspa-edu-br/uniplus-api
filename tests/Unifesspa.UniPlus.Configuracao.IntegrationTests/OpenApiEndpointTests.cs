namespace Unifesspa.UniPlus.Configuracao.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Infrastructure;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;

/// <summary>
/// Smoke + drift check do endpoint <c>/openapi/configuracao.json</c>: runtime
/// gera o spec OpenAPI 3.x do módulo Configuração e compara com o baseline
/// committed em <c>contracts/openapi.configuracao.json</c> (ADR-0030).
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<ConfiguracaoApiFactory>
{
    private const string BaselineRelativePath = "contracts/openapi.configuracao.json";

    private readonly ConfiguracaoApiFactory _factory;

    public OpenApiEndpointTests(ConfiguracaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/configuracao.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/configuracao.json", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
    }

    [Fact(DisplayName = "O código do programa de oferta é componente de texto com enum, sem o valor nenhum")]
    public Task ProgramaDeOfertaCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync(
            "ProgramaDeOfertaCodigo",
            ["REGULAR", "FORMA_PARA", "PARFOR", "PRONERA", "PEPETI", "CONVENIO_OUTRO", "OUTRO"]);

    [Fact(DisplayName = "O código do formato pedagógico é componente de texto com enum, sem o valor nenhum")]
    public Task FormatoPedagogicoCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync("FormatoPedagogicoCodigo", ["PRESENCIAL", "SEMIPRESENCIAL", "EAD"]);

    [Fact(DisplayName = "O código da resposta do programa de oferta referencia o componente do enum")]
    public Task RespostaDoPrograma_ReferenciaOComponenteDoEnum() =>
        ConferirReferenciaDaRespostaAsync("ProgramaDeOferta", "programaDeOferta", "ProgramaDeOfertaCodigo");

    [Fact(DisplayName = "O código da resposta do formato pedagógico referencia o componente do enum")]
    public Task RespostaDoFormato_ReferenciaOComponenteDoEnum() =>
        ConferirReferenciaDaRespostaAsync("FormatoPedagogico", "formatoPedagogico", "FormatoPedagogicoCodigo");

    [Fact(DisplayName = "O código do regime de funcionamento é componente de texto com enum, sem o valor nenhum")]
    public Task RegimeDeFuncionamentoCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync("RegimeDeFuncionamentoCodigo", ["INTENSIVO", "EXTENSIVO", "ALTERNANCIA_PEDAGOGICA"]);

    [Fact(DisplayName = "O código do regime de turno é componente de texto com enum, sem o valor nenhum")]
    public Task RegimeDeTurnoCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync("RegimeDeTurnoCodigo", ["REGULAR", "INTEGRAL"]);

    [Fact(DisplayName = "O código do turno é componente de texto com enum, sem o valor nenhum")]
    public Task TurnoOfertaCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync("TurnoOfertaCodigo", ["MATUTINO", "VESPERTINO", "NOTURNO"]);

    [Theory(DisplayName = "O código da resposta de cada enum da oferta referencia o componente dele")]
    [InlineData("RegimeDeFuncionamento", "regimeDeFuncionamento", "RegimeDeFuncionamentoCodigo")]
    [InlineData("RegimeDeTurno", "regimeDeTurno", "RegimeDeTurnoCodigo")]
    public Task RespostaDosRegimes_ReferenciaOComponenteDoEnum(string resposta, string campo, string componente) =>
        ConferirReferenciaDaRespostaAsync(resposta, campo, componente);

    [Fact(DisplayName = "Os turnos da resposta da oferta são uma lista de código e rótulo")]
    public async Task RespostaDosTurnos_EListaDeCodigoERotulo()
    {
        JsonElement schemas = await ObterSchemasAsync();

        schemas.GetProperty("OfertaCursoDto").GetProperty("properties").GetProperty("turnos").GetProperty("items").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/TurnoOferta");
        schemas.GetProperty("TurnoOferta").GetProperty("properties").GetProperty("codigo").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/TurnoOfertaCodigo");
    }

    [Theory(DisplayName = "Os pedidos da oferta documentam o item de turnos sem nulo, só com os códigos do enum")]
    [InlineData("CriarOfertaCursoCommand")]
    [InlineData("AtualizarOfertaCursoCommand")]
    public async Task PedidosDaOferta_ItemDeTurnosNaoAceitaNulo(string pedido)
    {
        JsonElement schemas = await ObterSchemasAsync();
        JsonElement itens = schemas.GetProperty(pedido).GetProperty("properties").GetProperty("turnos").GetProperty("items");

        itens.GetProperty("$ref").GetString().Should().Be("#/components/schemas/TurnoOfertaCodigo");
        itens.TryGetProperty("oneOf", out _).Should().BeFalse("o item nulo é recusado pelo domínio e não é publicado como aceito");
    }

    [Theory(DisplayName = "Os pedidos de criação e de atualização da oferta documentam programa, formato e regimes com o componente do enum")]
    [InlineData("CriarOfertaCursoCommand")]
    [InlineData("AtualizarOfertaCursoCommand")]
    public async Task PedidosDaOferta_ReferenciamOsComponentesDosEnums(string pedido)
    {
        JsonElement schemas = await ObterSchemasAsync();
        JsonElement propriedades = schemas.GetProperty(pedido).GetProperty("properties");

        ReferenciasDe(propriedades.GetProperty("programaDeOferta")).Should().Contain("#/components/schemas/ProgramaDeOfertaCodigo");
        ReferenciasDe(propriedades.GetProperty("formatoPedagogico")).Should().Contain("#/components/schemas/FormatoPedagogicoCodigo");
        ReferenciasDe(propriedades.GetProperty("regimeDeFuncionamento")).Should().Contain("#/components/schemas/RegimeDeFuncionamentoCodigo");
        ReferenciasDe(propriedades.GetProperty("regimeDeTurno")).Should().Contain("#/components/schemas/RegimeDeTurnoCodigo");
    }

    [Fact(DisplayName = "O código da natureza legal é componente de texto com enum, sem o valor nenhum")]
    public Task NaturezaLegalCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync("NaturezaLegalCodigo", ["COTA_RESERVADA", "AMPLA", "ACAO_AFIRMATIVA"]);

    [Fact(DisplayName = "O código da ação quando indeferido é componente de texto com enum, sem o valor nenhum")]
    public Task AcaoQuandoIndeferidoCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync("AcaoQuandoIndeferidoCodigo", ["RECLASSIFICA_AC", "RECLASSIFICA_CONFORME_EDITAL"]);

    [Fact(DisplayName = "O código da composição de vagas é componente de texto com enum, sem o valor nenhum")]
    public Task ComposicaoVagasCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync(
            "ComposicaoVagasCodigo",
            ["RESTANTE_DAS_VAGAS_OFERTADAS", "DENTRO_DA_RESERVA_LEGAL", "RETIRADA_DE_OUTRA_MODALIDADE", "SUPLEMENTAR_AO_TOTAL"]);

    [Fact(DisplayName = "O código do tipo de local de oferta é componente de texto com enum, sem o valor nenhum")]
    public Task TipoLocalOfertaCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync(
            "TipoLocalOfertaCodigo",
            ["CAMPUS_SEDE", "CAMPUS_FORA_DE_SEDE", "CURSO_FORA_DE_SEDE", "POLO_DE_EDUCACAO_A_DISTANCIA", "CONVENIO_DE_INTERIORIZACAO", "OUTRO"]);

    [Fact(DisplayName = "O código da resposta do local de oferta referencia o componente do enum")]
    public Task RespostaDoLocalOferta_ReferenciaOComponenteDoEnum() =>
        ConferirReferenciaDaRespostaAsync("TipoLocalOferta", "tipo", "TipoLocalOfertaCodigo", "LocalOfertaDto");

    [Theory(DisplayName = "Os pedidos de criação e de atualização do local de oferta documentam o tipo com o componente dos códigos")]
    [InlineData("CriarLocalOfertaCommand")]
    [InlineData("AtualizarLocalOfertaCommand")]
    public async Task PedidosDoLocalOferta_ReferenciamOComponenteDoTipo(string pedido)
    {
        JsonElement propriedades = (await ObterSchemasAsync()).GetProperty(pedido).GetProperty("properties");

        ReferenciasDe(propriedades.GetProperty("tipo")).Should().Contain("#/components/schemas/TipoLocalOfertaCodigo");
    }

    [Fact(DisplayName = "O código da origem da data da fase é componente de texto com enum, sem o valor nenhuma")]
    public Task OrigemDataFaseCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync("OrigemDataFaseCodigo", ["NO_EDITAL", "NA_EXECUCAO"]);

    [Fact(DisplayName = "O código da forma de aceite é componente de texto com enum, sem o valor nenhuma")]
    public Task FormaAceiteCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync(
            "FormaAceiteCodigo", ["A_DEFINIR", "REGISTRO_SEM_ENDERECO_IP", "REGISTRO_COM_ENDERECO_IP"]);

    [Theory(DisplayName = "O código da resposta de fase e de termo referencia o componente do enum")]
    [InlineData("OrigemDataFase", "origemData", "OrigemDataFaseCodigo", "FaseCanonicaDto")]
    [InlineData("FormaAceite", "formaAceiteRascunho", "FormaAceiteCodigo", "TermoConsentimentoDto")]
    [InlineData("FormaAceite", "formaAceiteRascunho", "FormaAceiteCodigo", "TermoConsentimentoResumoDto")]
    [InlineData("FormaAceite", "formaAceite", "FormaAceiteCodigo", "TermoConsentimentoVersaoDto")]
    public Task RespostaDeFaseETermo_ReferenciaOComponenteDoEnum(string resposta, string campo, string componente, string dto) =>
        ConferirReferenciaDaRespostaAsync(resposta, campo, componente, dto);

    [Theory(DisplayName = "Os pedidos de fase e de termo documentam o código com o componente dos códigos")]
    [InlineData("CriarFaseCanonicaCommand", "origemData", "OrigemDataFaseCodigo")]
    [InlineData("AtualizarFaseCanonicaCommand", "origemData", "OrigemDataFaseCodigo")]
    [InlineData("CriarTermoConsentimentoCommand", "formaAceiteRascunho", "FormaAceiteCodigo")]
    [InlineData("EditarRascunhoTermoConsentimentoCommand", "formaAceiteRascunho", "FormaAceiteCodigo")]
    public async Task PedidosDeFaseETermo_ReferenciamOComponenteDoCodigo(string pedido, string campo, string componente)
    {
        JsonElement propriedades = (await ObterSchemasAsync()).GetProperty(pedido).GetProperty("properties");

        ReferenciasDe(propriedades.GetProperty(campo)).Should().Contain($"#/components/schemas/{componente}");
    }

    [Fact(DisplayName = "O código da regra de remanejamento é componente de texto com enum, sem o valor nenhum")]
    public Task RegraRemanejamentoCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync("RegraRemanejamentoCodigo", ["SEGUE_ORDEM_DE_REMANEJAMENTO", "DESTINO_UNICO", "CRUZADO"]);

    [Theory(DisplayName = "O código da resposta de natureza legal e de composição de vagas da modalidade referencia o componente dele")]
    [InlineData("NaturezaLegal", "naturezaLegal", "NaturezaLegalCodigo")]
    [InlineData("ComposicaoVagas", "composicaoVagas", "ComposicaoVagasCodigo")]
    public Task RespostaDaModalidade_ReferenciaOComponenteDoEnum(string resposta, string campo, string componente) =>
        ConferirReferenciaDaRespostaAsync(resposta, campo, componente, "ModalidadeDto");

    [Theory(DisplayName = "Os pedidos de criação e de atualização da modalidade documentam os enums com o componente dos códigos")]
    [InlineData("CriarModalidadeCommand")]
    [InlineData("AtualizarModalidadeCommand")]
    public async Task PedidosDaModalidade_ReferenciamOsComponentesDosEnums(string pedido)
    {
        JsonElement schemas = await ObterSchemasAsync();
        JsonElement propriedades = schemas.GetProperty(pedido).GetProperty("properties");

        ReferenciasDe(propriedades.GetProperty("naturezaLegal")).Should().Contain("#/components/schemas/NaturezaLegalCodigo");
        ReferenciasDe(propriedades.GetProperty("composicaoVagas")).Should().Contain("#/components/schemas/ComposicaoVagasCodigo");
        ReferenciasDe(propriedades.GetProperty("acaoQuandoIndeferido")).Should().Contain("#/components/schemas/AcaoQuandoIndeferidoCodigo");
    }

    [Fact(DisplayName = "O bloco de remanejamento do pedido da modalidade documenta a regra com o componente dos códigos")]
    public async Task PedidoDoRemanejamento_ReferenciaOComponenteDaRegra()
    {
        JsonElement bloco = (await ObterSchemasAsync()).GetProperty("RemanejamentoDasVagasInformado").GetProperty("properties");

        ReferenciasDe(bloco.GetProperty("regra")).Should().Contain("#/components/schemas/RegraRemanejamentoCodigo");
    }

    private async Task ConferirComponenteDeCodigosAsync(string componente, string[] codigosEsperados)
    {
        JsonElement codigos = (await ObterSchemasAsync()).GetProperty(componente);

        codigos.GetProperty("type").GetString().Should().Be("string");
        codigos.GetProperty("enum").EnumerateArray().Select(c => c.GetString()).Should().Equal(codigosEsperados);
    }

    private async Task ConferirReferenciaDaRespostaAsync(string resposta, string campo, string componente, string dto = "OfertaCursoDto")
    {
        JsonElement schemas = await ObterSchemasAsync();

        schemas.GetProperty(resposta).GetProperty("properties").GetProperty("codigo").GetProperty("$ref").GetString()
            .Should().Be($"#/components/schemas/{componente}");
        schemas.GetProperty(dto).GetProperty("properties").GetProperty(campo).GetProperty("$ref").GetString()
            .Should().Be($"#/components/schemas/{resposta}");
    }

    private static IEnumerable<string?> ReferenciasDe(JsonElement propriedade) =>
        propriedade.GetProperty("oneOf").EnumerateArray()
            .Where(alternativa => alternativa.TryGetProperty("$ref", out _))
            .Select(alternativa => alternativa.GetProperty("$ref").GetString());

    private async Task<JsonElement> ObterSchemasAsync()
    {
        using JsonDocument doc = JsonDocument.Parse(await ObterDocumentoAsync());

        return doc.RootElement.GetProperty("components").GetProperty("schemas").Clone();
    }

    [Fact(DisplayName = "Spec runtime de Configuração bate com baseline committed em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        using HttpClient client = _factory.CreateClient();

        await ContratoOpenApiGravado.ConferirAsync(client, "/openapi/configuracao.json", BaselineRelativePath);
    }

    [Fact(DisplayName = "No contrato da Configuração, todo campo de texto que leva o código de um enum referencia o componente dos códigos")]
    public async Task CampoDeTextoComCodigoDeEnum_ReferenciaOComponenteDosCodigos()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/configuracao.json", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        using JsonDocument spec = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        CodigosDeEnumNoContrato.CamposComTextoCru(spec.RootElement).Should().BeEmpty(
            "o campo que leva código de enum de contrato publica o componente dos códigos do enum (ADR-0176)");
        CodigosDeEnumNoContrato.ReferenciaOsCodigos(spec.RootElement, "CondicaoDoPredicadoDto", "operador").Should().BeTrue();
        CodigosDeEnumNoContrato.ReferenciaOsCodigos(spec.RootElement, "ApresentacaoDoCampoDto", "tipoDeCampo").Should().BeTrue();
    }

    [Fact(DisplayName = "O componente de situação descreve a exibição do rótulo")]
    public async Task ComponenteDeSituacao_NoContrato_DescreveEtiqueta()
    {
        JsonElement schemas = await ObterSchemasAsync();

        schemas.GetProperty("ComponenteDeExibicaoCodigo").GetProperty("description").GetString()
            !.Split('\n').Single(static linha => linha.Contains("`SITUACAO`:", StringComparison.Ordinal))
            .Should().Contain("A tela mostra o rótulo numa etiqueta, sem mudar a forma do valor");
    }

    private async Task<string> ObterDocumentoAsync()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/configuracao.json", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
