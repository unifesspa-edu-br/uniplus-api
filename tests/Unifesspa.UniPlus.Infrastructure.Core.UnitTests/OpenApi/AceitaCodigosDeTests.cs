namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Contratos;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio.ModeloDeCodigosDeTeste;

public sealed class AceitaCodigosDeTests
{
    private const string ComponenteDosCodigos = "#/components/schemas/FaseCodigo";

    private static readonly Lazy<Task<DocumentoPublicado>> Controladores = new(GeradorDeDocumentoDeTeste.GerarDosControladoresAsync);

    private static readonly Lazy<Task<DocumentoPublicado>> RotasMinimas = new(() => GeradorDeDocumentoDeTeste.GerarAsync(app =>
    {
        app.MapGet("/parametro", ([AceitaCodigosDe<Fase>] string? fase) => Results.Ok(fase));
        app.MapGet("/consulta", ([AsParameters] FiltroDeFase filtro) => Results.Ok(filtro));
    }));

    [Fact(DisplayName = "O campo de pedido de texto obrigatório referencia os códigos e continua obrigatório")]
    public async Task CampoObrigatorio_ReferenciaOsCodigosEContinuaObrigatorio()
    {
        DocumentoPublicado documento = await Controladores.Value;

        DocumentoPublicado.Referencia(documento.Propriedade(nameof(PedidoDeFase), "fase")).Should().Be(ComponenteDosCodigos);
        Obrigatorios(documento, nameof(PedidoDeFase)).Should().Contain("fase");
    }

    [Fact(DisplayName = "O campo de pedido de texto opcional aceita nulo ou os códigos e continua opcional")]
    public async Task CampoOpcional_AceitaNuloOuOsCodigosEContinuaOpcional()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonArray alternativas = documento.Propriedade(nameof(PedidoDeFase), "faseOpcional")["oneOf"]!.AsArray();

        alternativas[0]!["type"]!.GetValue<string>().Should().Be("null");
        DocumentoPublicado.Referencia(alternativas[1]!).Should().Be(ComponenteDosCodigos);
        Obrigatorios(documento, nameof(PedidoDeFase)).Should().NotContain("faseOpcional");
    }

    [Fact(DisplayName = "A coleção opcional de textos referencia os códigos em cada item e continua opcional e coleção")]
    public async Task ColecaoOpcional_ReferenciaOsCodigosNosItensEContinuaOpcional()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonNode colecao = documento.Propriedade(nameof(PedidoDeFase), "fases");

        colecao["type"]!.AsArray().Select(static t => t!.GetValue<string>()).Should().BeEquivalentTo(["null", "array"]);
        DocumentoPublicado.Referencia(colecao["items"]!).Should().Be(ComponenteDosCodigos);
        Obrigatorios(documento, nameof(PedidoDeFase)).Should().NotContain("fases");
    }

    [Fact(DisplayName = "O item da coleção publica só o código válido: a referência, sem alternativa nula nem tipo")]
    public async Task ItemDaColecao_PublicaSoOCodigoValido()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonNode item = documento.Propriedade(nameof(PedidoDeFase), "fases")["items"]!;

        item.AsObject().Select(static p => p.Key).Should().Equal("$ref");
    }

    [Fact(DisplayName = "O vetor obrigatório de textos referencia os códigos em cada item e continua obrigatório")]
    public async Task VetorObrigatorio_ReferenciaOsCodigosNosItensEContinuaObrigatorio()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonNode vetor = documento.Propriedade(nameof(PedidoDeFase), "fasesObrigatorias");

        vetor["type"]!.GetValue<string>().Should().Be("array");
        DocumentoPublicado.Referencia(vetor["items"]!).Should().Be(ComponenteDosCodigos);
        Obrigatorios(documento, nameof(PedidoDeFase)).Should().Contain("fasesObrigatorias");
    }

    [Fact(DisplayName = "O campo de pedido mantém a descrição que já tinha, nulo ou não")]
    public async Task Campo_MantemADescricao()
    {
        DocumentoPublicado documento = await Controladores.Value;

        documento.Propriedade(nameof(PedidoDeFase), "faseDescrita")["description"]!.GetValue<string>().Should().Be("A fase em que o processo está.");
        documento.Propriedade(nameof(PedidoDeFase), "faseOpcionalDescrita")["description"]!.GetValue<string>().Should().Be("A fase que o pedido muda, se mudar.");
    }

    [Fact(DisplayName = "O campo de pedido e o parâmetro de consulta sem descrição própria não copiam a descrição do enum")]
    public async Task CampoEParametroSemDescricaoPropria_NaoCopiamADescricaoDoEnum()
    {
        DocumentoPublicado documento = await Controladores.Value;

        documento.Propriedade(nameof(PedidoDeFase), "fase")["description"].Should().BeNull();
        documento.Parametro("/api/fases/direto", "get", "fase")["schema"]!["description"].Should().BeNull();
    }

    [Fact(DisplayName = "O enum com descrição que só aparece em campo de pedido leva a descrição no componente dos códigos, antes da lista")]
    public async Task EnumSoEmCampoDePedido_ComponenteDosCodigosLevaADescricaoDoEnumAntesDaLista()
    {
        DocumentoPublicado documento = await RotasMinimas.Value;

        documento.Componentes.Should().Contain("FaseCodigo").And.NotContain([nameof(Fase), "FaseComDescricao"]);
        documento.Componente("FaseCodigo")["description"]!.GetValue<string>().Should().Be(
            "Em que fase o processo está, para todos os códigos.\n\n"
            + "- `inscricao`: Inscrição — A pessoa ainda pode se inscrever.\n- `resultado`: Resultado — O resultado já saiu.");
    }

    [Fact(DisplayName = "O campo de pedido sem o atributo continua texto livre")]
    public async Task CampoSemAtributo_ContinuaTextoLivre()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonNode livre = documento.Propriedade(nameof(PedidoDeFase), "livre");

        DocumentoPublicado.Referencia(livre).Should().BeNull();
        livre["type"]!.AsArray().Select(static t => t!.GetValue<string>()).Should().BeEquivalentTo(["null", "string"]);
    }

    [Fact(DisplayName = "No registro posicional o atributo vale no parâmetro e na propriedade")]
    public async Task RegistroPosicional_AtributoNoParametroENaPropriedade()
    {
        DocumentoPublicado documento = await Controladores.Value;

        DocumentoPublicado.Referencia(documento.Propriedade(nameof(PedidoPosicional), "noParametro")).Should().Be(ComponenteDosCodigos);
        DocumentoPublicado.Referencia(documento.Propriedade(nameof(PedidoPosicional), "naPropriedade")["items"]!).Should().Be(ComponenteDosCodigos);
    }

    [Fact(DisplayName = "O parâmetro do construtor que o serializador usa carrega o atributo")]
    public async Task ParametroDoConstrutorUsado_CarregaOAtributo()
    {
        DocumentoPublicado documento = await Controladores.Value;

        DocumentoPublicado.Referencia(documento.Propriedade(nameof(PedidoComConstrutorMarcado), "fase")).Should().Be(ComponenteDosCodigos);
    }

    [Fact(DisplayName = "O atributo de um parâmetro de mesmo nome de outra sobrecarga do construtor não conta")]
    public async Task ParametroDeOutraSobrecarga_NaoConta()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonNode fase = documento.Propriedade(nameof(PedidoComOutraSobrecargaMarcada), "fase");

        DocumentoPublicado.Referencia(fase).Should().BeNull();
        fase["type"]!.GetValue<string>().Should().Be("string");
    }

    [Fact(DisplayName = "O parâmetro de consulta direto da ação referencia os códigos e continua opcional")]
    public async Task ParametroDiretoDaAcao_ReferenciaOsCodigosEContinuaOpcional()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonNode parametro = documento.Parametro("/api/fases/direto", "get", "fase");

        DocumentoPublicado.Referencia(parametro["schema"]!).Should().Be(ComponenteDosCodigos);
        Obrigatorio(parametro).Should().BeFalse();
    }

    [Fact(DisplayName = "O parâmetro de consulta obrigatório referencia os códigos e continua obrigatório")]
    public async Task ParametroObrigatorio_ReferenciaOsCodigosEContinuaObrigatorio()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonNode parametro = documento.Parametro("/api/fases/direto", "get", "faseObrigatoria");

        DocumentoPublicado.Referencia(parametro["schema"]!).Should().Be(ComponenteDosCodigos);
        Obrigatorio(parametro).Should().BeTrue();
    }

    [Fact(DisplayName = "O parâmetro de consulta de coleção de textos referencia os códigos em cada item")]
    public async Task ParametroDeColecao_ReferenciaOsCodigosNosItens()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonNode esquema = documento.Parametro("/api/fases/direto", "get", "fases")["schema"]!;

        esquema["type"]!.GetValue<string>().Should().Be("array");
        DocumentoPublicado.Referencia(esquema["items"]!).Should().Be(ComponenteDosCodigos);
    }

    [Fact(DisplayName = "O parâmetro de consulta de uma propriedade de tipo com [FromQuery] referencia os códigos")]
    public async Task ParametroDePropriedadeDeTipoDeConsulta_ReferenciaOsCodigos()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonNode parametro = documento.Parametro("/api/fases/filtro", "get", "faseDoFiltro");

        DocumentoPublicado.Referencia(parametro["schema"]!).Should().Be(ComponenteDosCodigos);
        Obrigatorio(parametro).Should().BeFalse();
    }

    [Fact(DisplayName = "O parâmetro de consulta sem o atributo continua texto livre")]
    public async Task ParametroSemAtributo_ContinuaTextoLivre()
    {
        DocumentoPublicado documento = await Controladores.Value;

        JsonNode direto = documento.Parametro("/api/fases/direto", "get", "livre")["schema"]!;
        JsonNode doFiltro = documento.Parametro("/api/fases/filtro", "get", "livreDoFiltro")["schema"]!;

        new[] { direto, doFiltro }.Select(DocumentoPublicado.Referencia).Should().OnlyContain(referencia => referencia == null);
        new[] { direto, doFiltro }.Select(static e => e["type"]!.GetValue<string>()).Should().OnlyContain(tipo => tipo == "string");
    }

    [Fact(DisplayName = "O parâmetro de uma rota mínima referencia os códigos")]
    public async Task ParametroDeRotaMinima_ReferenciaOsCodigos()
    {
        DocumentoPublicado documento = await RotasMinimas.Value;

        DocumentoPublicado.Referencia(documento.Parametro("/parametro", "get", "fase")["schema"]!).Should().Be(ComponenteDosCodigos);
    }

    [Fact(DisplayName = "A propriedade de um tipo de consulta de rota mínima referencia os códigos")]
    public async Task PropriedadeDeTipoDeConsultaDeRotaMinima_ReferenciaOsCodigos()
    {
        DocumentoPublicado documento = await RotasMinimas.Value;

        DocumentoPublicado.Referencia(documento.Parametro("/consulta", "get", "faseDoFiltro")["schema"]!).Should().Be(ComponenteDosCodigos);
    }

    [Fact(DisplayName = "O atributo em propriedade que não é texto nem coleção de texto faz a geração falhar, citando o campo e o tipo")]
    public async Task PropriedadeDeOutroTipo_FalhaCitandoOCampoEOTipo()
    {
        Func<Task> geracao = () => GeradorDeDocumentoDeTeste.GerarAsync(app =>
            app.MapPost("/pedido", (PedidoComNumeroMarcado pedido) => Results.Ok(pedido)));

        InvalidOperationException erro = (await geracao.Should().ThrowAsync<InvalidOperationException>()).Which;

        erro.Message.Should().Contain("PedidoComNumeroMarcado.quantidade").And.Contain("Int32").And.Contain("AceitaCodigosDe<Fase>");
    }

    [Fact(DisplayName = "O atributo em parâmetro que não é texto nem coleção de texto faz a geração falhar, citando o parâmetro e o tipo")]
    public async Task ParametroDeOutroTipo_FalhaCitandoOParametroEOTipo()
    {
        Func<Task> geracao = () => GeradorDeDocumentoDeTeste.GerarAsync(app =>
            app.MapGet("/consulta", ([AceitaCodigosDe<Fase>] int quantidade) => Results.Ok(quantidade)));

        InvalidOperationException erro = (await geracao.Should().ThrowAsync<InvalidOperationException>()).Which;

        erro.Message.Should().Contain("parâmetro quantidade").And.Contain("Int32").And.Contain("AceitaCodigosDe<Fase>");
    }

    private static bool Obrigatorio(JsonNode parametro) => parametro["required"]?.GetValue<bool>() ?? false;

    private static IEnumerable<string> Obrigatorios(DocumentoPublicado documento, string componente) =>
        documento.Componente(componente)["required"]?.AsArray().Select(static n => n!.GetValue<string>()) ?? [];
}
