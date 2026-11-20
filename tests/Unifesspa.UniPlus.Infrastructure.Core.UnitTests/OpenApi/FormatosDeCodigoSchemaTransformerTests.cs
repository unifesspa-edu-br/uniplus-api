namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio.ModeloDeCodigosDeTeste;

public sealed class FormatosDeCodigoSchemaTransformerTests
{
    private const string ComponenteDosCodigos = "#/components/schemas/AndamentoCodigo";

    private static readonly Lazy<Task<DocumentoPublicado>> DocumentoGerado = new(GerarDocumentoAsync);

    [DescricaoDoContrato("Como anda o trabalho, para todos os códigos.")]
    public enum Andamento
    {
        [CodigoDeContrato("em_andamento", "Em andamento", Descricao = "Ainda há o que fazer.")]
        EmAndamento = 1,

        [CodigoDeContrato("concluido", "Concluído")]
        Concluido = 2,
    }

    public enum Etapa
    {
        [CodigoDeContrato("unica", "Única")]
        Unica = 1,
    }

    [Fact(DisplayName = "O código com rótulo vira um componente com o nome do enum, sem o tipo genérico")]
    public async Task Componente_TemONomeDoEnum()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        documento.Componentes.Should().Contain(nameof(Andamento)).And.NotContain(chave => chave.Contains("CodigoComRotulo", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O código referencia o componente dos códigos do enum, sem lista embutida")]
    public async Task Codigo_ReferenciaOComponenteDosCodigos()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        JsonNode codigo = documento.Propriedade(nameof(Andamento), "codigo");

        DocumentoPublicado.Referencia(codigo).Should().Be(ComponenteDosCodigos);
        codigo["enum"].Should().BeNull();
    }

    [Fact(DisplayName = "O componente dos códigos é texto com os códigos na ordem declarada")]
    public async Task ComponenteDosCodigos_TextoComOsCodigosNaOrdemDeclarada()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        JsonNode codigos = documento.Componente("AndamentoCodigo");

        codigos["type"]!.GetValue<string>().Should().Be("string");
        codigos["enum"]!.AsArray().Select(static c => c!.GetValue<string>()).Should().Equal("em_andamento", "concluido");
    }

    [Fact(DisplayName = "A descrição do componente dos códigos traz a descrição do enum, uma linha em branco e um item de lista por código, com o rótulo e, quando há, a descrição")]
    public async Task ComponenteDosCodigos_DescricaoTemOParagrafoDoEnumEAListaDeCodigos()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        documento.Componente("AndamentoCodigo")["description"]!.GetValue<string>().Should().Be(
            "Como anda o trabalho, para todos os códigos.\n\n- `em_andamento`: Em andamento — Ainda há o que fazer.\n- `concluido`: Concluído");
    }

    [Fact(DisplayName = "O componente do enum não tem descrição: a descrição do enum vai só para o componente dos códigos")]
    public async Task DescricaoDoEnum_NaoVaiParaOComponenteDoEnum()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        documento.Componente(nameof(Andamento))["description"].Should().BeNull();
    }

    [Fact(DisplayName = "O enum sem a descrição do enum publica o componente do enum sem descrição e a descrição dos códigos só com a lista")]
    public async Task EnumSemDescricao_DescricaoDosCodigosSoComALista()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        documento.Componente(nameof(Etapa))["description"].Should().BeNull();
        documento.Componente("EtapaCodigo")["description"]!.GetValue<string>().Should().Be("- `unica`: Única");
    }

    [Fact(DisplayName = "O rótulo é publicado como texto livre")]
    public async Task Rotulo_PublicadoComoTexto()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        JsonNode rotulo = documento.Propriedade(nameof(Andamento), "rotulo");

        rotulo["type"]!.GetValue<string>().Should().Be("string");
        rotulo["enum"].Should().BeNull();
    }

    [Fact(DisplayName = "O código e o rótulo são obrigatórios")]
    public async Task CodigoERotulo_SaoObrigatorios()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        documento.Componente(nameof(Andamento))["required"]!.AsArray().Select(static n => n!.GetValue<string>()).Should().BeEquivalentTo(["codigo", "rotulo"]);
    }

    [Fact(DisplayName = "O texto de outra propriedade chamada codigo não recebe a lista de códigos")]
    public async Task OutraPropriedadeCodigo_NaoRecebeALista()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        JsonNode codigo = documento.Propriedade("Outro", "codigo");

        DocumentoPublicado.Referencia(codigo).Should().BeNull();
        codigo["enum"].Should().BeNull();
    }

    [Fact(DisplayName = "O código com descrição tem componente próprio, com código, rótulo e descrição obrigatórios")]
    public async Task ComDescricao_ComponenteProprioComOsTresCampos()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        JsonNode componente = documento.Componente("FaseComDescricao");

        documento.Componentes.Should().Contain([nameof(Fase), "FaseComDescricao", "FaseCodigo"]);
        componente["properties"]!.AsObject().Select(static p => p.Key).Should().Equal("codigo", "rotulo", "descricao");
        componente["required"]!.AsArray().Select(static n => n!.GetValue<string>()).Should().BeEquivalentTo(["codigo", "rotulo", "descricao"]);
    }

    [Fact(DisplayName = "O código com descrição referencia os mesmos códigos do código com rótulo, e a descrição é texto não anulável")]
    public async Task ComDescricao_ReferenciaOsMesmosCodigosEDescricaoEhTexto()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        DocumentoPublicado.Referencia(documento.Propriedade("FaseComDescricao", "codigo")).Should().Be("#/components/schemas/FaseCodigo");
        DocumentoPublicado.Referencia(documento.Propriedade(nameof(Fase), "codigo")).Should().Be("#/components/schemas/FaseCodigo");
        documento.Propriedade("FaseComDescricao", "descricao")["type"]!.GetValue<string>().Should().Be("string");
    }

    [Fact(DisplayName = "O componente do código com descrição não tem descrição: a descrição do enum fica no componente dos códigos")]
    public async Task ComDescricao_DescricaoDoEnumNaoVaiParaOComponente()
    {
        DocumentoPublicado documento = await DocumentoGerado.Value;

        documento.Componente("FaseComDescricao")["description"].Should().BeNull();
    }

    [Fact(DisplayName = "O código com descrição de enum com membro sem descrição faz a geração falhar, citando o enum e o código sem descrição")]
    public async Task ComDescricao_EnumComMembroSemDescricao_FalhaCitandoOEnumEOCodigo()
    {
        Func<Task> geracao = () => GeradorDeDocumentoDeTeste.GerarAsync(app => app.MapGet("/turno", () => new RespostaDoTurno(null)));

        InvalidOperationException erro = (await geracao.Should().ThrowAsync<InvalidOperationException>()).Which;

        erro.Message.Should().Contain("ModeloDeCodigosDeTeste+Turno").And.Contain("'noite'").And.NotContain("'manha'");
    }

    private static Task<DocumentoPublicado> GerarDocumentoAsync() =>
        GeradorDeDocumentoDeTeste.GerarAsync(app =>
        {
            app.MapGet("/andamento", () => new Resposta(CodigoComRotulo<Andamento>.De(Andamento.EmAndamento)));
            app.MapGet("/etapa", () => new RespostaDaEtapa(CodigoComRotulo<Etapa>.De(Etapa.Unica)));
            app.MapGet("/fase", () => new RespostaDeFase(CodigoComRotulo<Fase>.De(Fase.Inscricao), CodigoComDescricao<Fase>.De(Fase.Inscricao)));
            app.MapGet("/outro", () => new Outro("abc"));
        });

    public sealed record Resposta(CodigoComRotulo<Andamento> Andamento);

    public sealed record RespostaDaEtapa(CodigoComRotulo<Etapa> Etapa);

    public sealed record Outro(string Codigo);

    public sealed record RespostaDoTurno(CodigoComDescricao<Turno>? Situacao);
}
