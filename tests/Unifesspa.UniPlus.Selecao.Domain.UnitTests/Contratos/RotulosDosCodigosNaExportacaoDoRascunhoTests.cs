namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A exportação do rascunho escreve, ao lado de cada código de enum, o rótulo que o esquema <c>v1</c> real declara só de leitura,
/// com o texto atual do catálogo; o código que o catálogo não conhece fica sem rótulo, e o documento segue válido.
/// </summary>
public sealed class RotulosDosCodigosNaExportacaoDoRascunhoTests
{
    private const string Cabecalho = """ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1 """;

    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    [Fact(DisplayName = "O rótulo do código sai logo depois do código, com o texto do catálogo")]
    public void Descrever_CodigoConhecido_EscreveORotuloLogoDepoisDoCodigo()
    {
        JsonObject documento = Descrever($$"""{ {{Cabecalho}}, "etapas": [ { "chave": "etapa_prova", "carater": "CLASSIFICATORIA", "formaDeAvaliacao": "OBJETIVA" } ] }""");

        JsonObject etapa = documento["etapas"]![0]!.AsObject();
        etapa.Select(static propriedade => propriedade.Key).Should()
            .Equal("chave", "carater", "caraterRotulo", "formaDeAvaliacao", "formaDeAvaliacaoRotulo");
        etapa["caraterRotulo"]!.GetValue<string>().Should().Be(CaraterEtapa.Classificatoria.Rotulo());
        etapa["formaDeAvaliacaoRotulo"]!.GetValue<string>().Should().Be(FormaDeAvaliacao.Objetiva.Rotulo());
    }

    [Theory(DisplayName = "O código que o catálogo não conhece ou que não é texto fica sem rótulo, sem recusa")]
    [InlineData("\"carater\": \"INEXISTENTE\"")]
    [InlineData("\"carater\": \"classificatoria\"")]
    [InlineData("\"carater\": 3")]
    [InlineData("\"carater\": null")]
    public void Descrever_CodigoDesconhecidoOuQueNaoETexto_OmiteORotulo(string campo)
    {
        ArgumentNullException.ThrowIfNull(campo);

        JsonObject documento = Descrever($$"""{ {{Cabecalho}}, "etapas": [ { "chave": "etapa_prova", {{campo}}, "formaDeAvaliacao": "HUMANA" } ] }""");

        JsonObject etapa = documento["etapas"]![0]!.AsObject();
        etapa.ContainsKey("caraterRotulo").Should().BeFalse();
        etapa["formaDeAvaliacaoRotulo"]!.GetValue<string>().Should().Be(FormaDeAvaliacao.Humana.Rotulo());
    }

    [Fact(DisplayName = "A lista de rótulos sai na ordem do rascunho, logo depois da lista de códigos")]
    public void Descrever_ListaDeCodigos_EscreveOsRotulosNaOrdemDoRascunho()
    {
        JsonObject documento = Descrever($$"""{ {{Cabecalho}}, "taxaDeInscricao": { "cobra": true, "fundamentos": [ "DOACAO_MEDULA_OSSEA", "CADASTRO_UNICO" ], "valor": 80 } }""");

        JsonObject taxa = documento["taxaDeInscricao"]!.AsObject();
        taxa.Select(static propriedade => propriedade.Key).Should().Equal("cobra", "fundamentos", "fundamentosRotulos", "valor");
        taxa["fundamentosRotulos"]!.AsArray().Select(static rotulo => rotulo!.GetValue<string>()).Should()
            .Equal(FundamentoIsencao.DoacaoMedulaOssea.Rotulo(), FundamentoIsencao.CadastroUnico.Rotulo());
    }

    [Fact(DisplayName = "A lista com um código que o catálogo não conhece fica sem a lista de rótulos")]
    public void Descrever_ListaComUmCodigoDesconhecido_OmiteAListaInteira()
    {
        JsonObject documento = Descrever($$"""{ {{Cabecalho}}, "taxaDeInscricao": { "fundamentos": [ "CADASTRO_UNICO", "INEXISTENTE" ] } }""");

        documento["taxaDeInscricao"]!.AsObject().ContainsKey("fundamentosRotulos").Should().BeFalse();
    }

    [Fact(DisplayName = "A lista vazia de códigos leva a lista vazia de rótulos")]
    public void Descrever_ListaVazia_EscreveListaVaziaDeRotulos()
    {
        JsonObject documento = Descrever($$"""{ {{Cabecalho}}, "taxaDeInscricao": { "fundamentos": [] } }""");

        documento["taxaDeInscricao"]!["fundamentosRotulos"]!.AsArray().Should().BeEmpty();
    }

    [Fact(DisplayName = "A condição aninhada em listas de alternativas recebe o rótulo do operador")]
    public void Descrever_CondicaoAninhada_EscreveORotuloDoOperador()
    {
        JsonObject documento = Descrever($$"""
            { {{Cabecalho}}, "documentosExigidos": { "exigencias": [ { "chave": "exigencia_renda", "aplicabilidade": "CONDICIONAL",
              "condicoes": [ [ { "codigoDoDado": "renda_familiar", "operador": "IGUAL", "valor": "ate_um_salario" } ] ] } ] } }
            """);

        JsonObject exigencia = documento["documentosExigidos"]!["exigencias"]![0]!.AsObject();
        exigencia["aplicabilidadeRotulo"]!.GetValue<string>().Should().Be(Aplicabilidade.Condicional.Rotulo());
        exigencia["condicoes"]![0]![0]!["operadorRotulo"]!.GetValue<string>().Should().Be(Operador.Igual.Rotulo());
    }

    [Theory(DisplayName = "O tipo de decisão leva o mesmo rótulo no procedimento, no motivo e na lista da fase e da etapa")]
    [InlineData("cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/tipoDeDecisaoRotulo")]
    [InlineData("motivosDeDecisao/0/tipoDeDecisaoCodigoRotulo")]
    [InlineData("cronograma/fases/0/tiposDeDecisaoRotulos/0")]
    [InlineData("etapas/0/tiposDeDecisaoRotulos/0")]
    public void Descrever_TipoDeDecisao_EscreveORotuloDoCatalogo(string caminho)
    {
        ArgumentNullException.ThrowIfNull(caminho);

        JsonObject documento = Descrever($$"""
            { {{Cabecalho}},
              "cronograma": { "fases": [ { "chave": "fase_analise", "tiposDeDecisao": [ "ANALISE_DOCUMENTAL" ],
                "regraRecurso": { "procedimentosDeRecurso": [ { "tipoDeDecisao": "ANALISE_DOCUMENTAL" } ] } } ] },
              "etapas": [ { "chave": "etapa_prova", "tiposDeDecisao": [ "ANALISE_DOCUMENTAL" ] } ],
              "motivosDeDecisao": [ { "tipoDeDecisaoCodigo": "ANALISE_DOCUMENTAL", "codigo": "DOCUMENTO_ILEGIVEL" } ] }
            """);

        Em(documento, caminho).Should().Be(TipoMotivoDeDecisao.AnaliseDocumental.Rotulo());
    }

    [Fact(DisplayName = "O rótulo só é escrito onde o esquema o declara só de leitura")]
    public void Descrever_RotuloNaoDeclaradoSomenteLeitura_NaoEscreve()
    {
        FormaDoNo raiz = Forma([], new Dictionary<string, FormaDoNo>
        {
            ["carater"] = Forma(["etapas.schema.json#/properties/carater"], []),
            ["caraterRotulo"] = Forma([], [], somenteLeitura: false),
        });
        JsonObject documento = JsonNode.Parse("""{ "carater": "AMBAS" }""")!.AsObject();

        RotulosDosCodigosDeContrato.Descrever(documento, raiz);

        documento.ContainsKey("caraterRotulo").Should().BeFalse();
    }

    [Fact(DisplayName = "O documento com os rótulos escritos continua válido contra o esquema v1")]
    public void Descrever_DocumentoComVariosCodigos_ValidaContraOEsquema()
    {
        JsonObject documento = Descrever($$"""
            { {{Cabecalho}}, "identificacao": { "origemCandidatos": "INSCRICAO_PROPRIA" },
              "taxaDeInscricao": { "fundamentos": [ "CADASTRO_UNICO" ] },
              "etapas": [ { "chave": "etapa_prova", "carater": "ELIMINATORIA", "tiposDeDecisao": [ "NOTA_DA_ETAPA" ] } ],
              "motivosDeDecisao": [ { "etapaChave": "etapa_prova", "tipoDeDecisaoCodigo": "NOTA_DA_ETAPA", "codigo": "NOTA_ABAIXO_DO_MINIMO" } ] }
            """);

        Esquema.Avaliar(documento).Should().BeEmpty();
    }

    [Fact(DisplayName = "A exportação do rascunho escreve o rótulo do formato do modelo que ela mesma descreve")]
    public async Task ExportarAsync_ExigenciaComModelo_EscreveORotuloDoFormatoDoModelo()
    {
        RelogioFixo relogio = new(new DateTimeOffset(2026, 11, 26, 15, 0, 0, TimeSpan.Zero));
        ModeloDeDocumento modelo = ModeloConfirmado(relogio);

        JsonObject exportado = await ExportarAsync(modelo);

        JsonObject descrito = exportado["documentosExigidos"]!["exigencias"]![0]!["modelo"]!.AsObject();
        descrito["formato"]!.GetValue<string>().Should().Be(FormatoDeDocumento.Docx.Codigo());
        descrito["formatoRotulo"]!.GetValue<string>().Should().Be(FormatoDeDocumento.Docx.Rotulo());
        Esquema.Avaliar(exportado).Should().BeEmpty();
    }

    private static JsonObject Descrever(string texto)
    {
        JsonObject documento = JsonNode.Parse(texto)!.AsObject();
        RotulosDosCodigosDeContrato.Descrever(documento, Esquema.Forma);
        return documento;
    }

    private static async Task<JsonObject> ExportarAsync(ModeloDeDocumento modelo)
    {
        IModeloDeDocumentoRepository modelos = Substitute.For<IModeloDeDocumentoRepository>();
        modelos.ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([modelo]);
        IUnidadeReader unidades = Substitute.For<IUnidadeReader>();
        ExportacaoDoRascunho exportacao = new(
            unidades,
            [],
            LeituraDasVagasDeTeste.SemCadastros(),
            new ResolucaoDosModelosCitados(modelos),
            new DescricaoDaLocalidade(Substitute.For<IConferenciaEnderecoGeo>()),
            Esquema);
        string documento = $$"""{ {{Cabecalho}}, "documentosExigidos": { "exigencias": [ { "chave": "exigencia_autodeclaracao", "obrigatorio": true, "abrePrazoParaReenvio": false, "modeloId": "{{modelo.Id}}" } ] } }""";
        RascunhoDeProcessoSeletivo rascunho = RascunhoDeProcessoSeletivo.Criar(Guid.NewGuid(), DocumentoDoRascunho.Criar(documento, null).Value!).Value!;

        return await exportacao.ExportarAsync(rascunho, CancellationToken.None);
    }

    private static ModeloDeDocumento ModeloConfirmado(TimeProvider relogio)
    {
        ModeloDeDocumento modelo = ModeloDeDocumento.IniciarPendente(Guid.NewGuid(), "autodeclaracao.docx", "DOCX", relogio, TimeSpan.FromMinutes(15)).Value!;
        modelo.Confirmar(10, new string('a', 64), relogio).IsSuccess.Should().BeTrue();
        return modelo;
    }

    private static FormaDoNo Forma(string[] localizacoes, Dictionary<string, FormaDoNo> propriedades, bool somenteLeitura = true) =>
        new(localizacoes, [], somenteLeitura, null, () => propriedades, () => null);

    private static string Em(JsonNode documento, string caminho)
    {
        JsonNode? no = documento;
        foreach (string passo in caminho.Split('/'))
        {
            no = int.TryParse(passo, out int indice) ? no![indice] : no![passo];
        }

        return no!.GetValue<string>();
    }
}
