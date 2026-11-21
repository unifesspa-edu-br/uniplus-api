namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O documento jsonb do conteúdo do modelo, sem banco: a forma gravada é a do contrato, na ordem das
/// propriedades, com os opcionais presentes ou nulos e os textos escapados como o gravador escreve.
/// </summary>
public sealed class ConteudoDoModeloJsonTests
{
    private static readonly Guid TermoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid VersaoId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact(DisplayName = "O conteúdo com todos os opcionais presentes grava o JSON na ordem do contrato")]
    public void Serializar_ConteudoComOpcionaisPresentes_GravaOJsonNaOrdemDoContrato()
    {
        string json = ConteudoDoModeloJson.Serializar(ConteudoCompleto());

        json.Should().Be(JsonCompleto);
    }

    [Fact(DisplayName = "O conteúdo com os opcionais ausentes grava nulo nos textos e nas regras e lista vazia nas coleções")]
    public void Serializar_ConteudoComOpcionaisAusentes_GravaNuloEListaVazia()
    {
        string json = ConteudoDoModeloJson.Serializar(ConteudoMinimo());

        json.Should().Be(JsonMinimo);
    }

    [Fact(DisplayName = "O JSON com todos os opcionais presentes remonta cada parte e volta idêntico ao ser gravado de novo")]
    public void Desserializar_JsonComOpcionaisPresentes_VoltaIdenticoAoSerGravado()
    {
        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(JsonCompleto);

        ConteudoDoModeloJson.Serializar(conteudo).Should().Be(JsonCompleto);
    }

    [Fact(DisplayName = "O JSON com os opcionais nulos remonta cada parte e volta idêntico ao ser gravado de novo")]
    public void Desserializar_JsonComOpcionaisAusentes_VoltaIdenticoAoSerGravado()
    {
        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(JsonMinimo);

        ConteudoDoModeloJson.Serializar(conteudo).Should().Be(JsonMinimo);
    }

    [Fact(DisplayName = "O JSON gravado antes dos documentos exigidos e sem as chaves opcionais é lido como ausente e gravado com nulo")]
    public void Desserializar_JsonSemChavesOpcionais_LeComoAusente()
    {
        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(JsonLegado);

        ConteudoDoModeloJson.Serializar(conteudo).Should().Be(JsonLegadoRegravado);
    }

    [Fact(DisplayName = "O documento exigido sem a observação da base legal e com a exigência por opção nula é lido como ausente")]
    public void Desserializar_DocumentoSemOpcionais_LeComoAusente()
    {
        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(JsonDocumentoLegado);

        ConteudoDoModeloJson.Serializar(conteudo).Should().Be(JsonDocumentoLegadoRegravado);
    }

    [Theory(DisplayName = "A exigência por opção ausente, ou presente e que não é um objeto, é lida como ausente")]
    [InlineData("")]
    [InlineData(",\"porOpcao\":\"MEDICINA\"")]
    [InlineData(",\"porOpcao\":3")]
    [InlineData(",\"porOpcao\":[]")]
    public void Desserializar_PorOpcaoAusenteOuQueNaoEObjeto_LeComoAusente(string porOpcao)
    {
        string json = JsonDocumentoLegado.Replace(",\"porOpcao\":null", porOpcao, StringComparison.Ordinal);

        ConteudoDoModeloJson.Serializar(ConteudoDoModeloJson.Desserializar(json)).Should().Be(JsonDocumentoLegadoRegravado);
    }

    [Fact(DisplayName = "O máximo de ocorrências do grupo é lido como inteiro")]
    public void Desserializar_MaximoInteiro_LeOMaximo()
    {
        string json = JsonLegado.Replace("\"maximo\":null", "\"maximo\":5", StringComparison.Ordinal);

        ConteudoDoModeloJson.Serializar(ConteudoDoModeloJson.Desserializar(json)).Should().Contain("\"maximo\":5");
    }

    [Fact(DisplayName = "O máximo de ocorrências do grupo ausente é dado corrompido e lança")]
    public void Desserializar_MaximoAusente_Lanca()
    {
        string json = JsonLegado.Replace("\"maximo\":null,", string.Empty, StringComparison.Ordinal);

        Action ler = () => ConteudoDoModeloJson.Desserializar(json);

        ler.Should().Throw<KeyNotFoundException>();
    }

    [Theory(DisplayName = "O máximo de ocorrências do grupo que não é inteiro nem nulo é dado corrompido e lança")]
    [InlineData("\"cinco\"")]
    [InlineData("true")]
    [InlineData("[]")]
    public void Desserializar_MaximoQueNaoEInteiro_Lanca(string maximo)
    {
        string json = JsonLegado.Replace("\"maximo\":null", $"\"maximo\":{maximo}", StringComparison.Ordinal);

        Action ler = () => ConteudoDoModeloJson.Desserializar(json);

        ler.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "A comparação de conteúdos é a do JSON gravado")]
    public void Comparer_ConteudosComOMesmoJson_SaoIguais()
    {
        ConteudoDoModelo copia = ConteudoDoModeloJson.Desserializar(JsonCompleto);

        ConteudoDoModeloJson.Comparer.Equals(ConteudoCompleto(), copia).Should().BeTrue();
    }

    [Theory(DisplayName = "O tipo de campo do item é gravado no documento pelo código")]
    [InlineData(TipoRenderizacao.Numero, "NUMERO")]
    [InlineData(TipoRenderizacao.Booleano, "BOOLEANO")]
    [InlineData(TipoRenderizacao.SelecaoUnica, "SELECAO_UNICA")]
    [InlineData(TipoRenderizacao.SelecaoMultipla, "SELECAO_MULTIPLA")]
    [InlineData(TipoRenderizacao.Texto, "TEXTO")]
    [InlineData(TipoRenderizacao.Data, "DATA")]
    [InlineData(TipoRenderizacao.Endereco, "ENDERECO")]
    [InlineData(TipoRenderizacao.Municipio, "MUNICIPIO")]
    public void Serializar_TipoDeCampoDoItem_GravaOCodigo(TipoRenderizacao tipo, string codigo)
    {
        ConteudoDoModelo conteudo = ConteudoMinimo() with { Itens = [ItemMinimo("QUILOMBOLA", 0).ComTipoDeCampo(tipo)] };

        ConteudoDoModeloJson.Serializar(conteudo).Should().Contain($"\"tipoRenderizacao\":\"{codigo}\"");
    }

    [Theory(DisplayName = "O tipo de campo gravado que o catálogo não declara é dado corrompido e lança")]
    [InlineData("NENHUMA")]
    [InlineData("selecao_unica")]
    [InlineData("0")]
    public void Desserializar_TipoDeCampoForaDoCatalogo_Lanca(string tipo)
    {
        string corrompido = JsonLegado.Replace("\"tipoRenderizacao\":\"BOOLEANO\"", $"\"tipoRenderizacao\":\"{tipo}\"", StringComparison.Ordinal);

        Action ler = () => ConteudoDoModeloJson.Desserializar(corrompido);

        ler.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A forma que não remonta é dado corrompido e lança")]
    public void Desserializar_ExibicaoQueNaoRemonta_Lanca()
    {
        string corrompido = JsonMinimo.Replace("\"exibicao\":null", "\"exibicao\":\"texto\"", StringComparison.Ordinal);

        Action ler = () => ConteudoDoModeloJson.Desserializar(corrompido);

        ler.Should().Throw<InvalidOperationException>();
    }

    private const string EtapaDadosJson =
        """{"codigo":"DADOS","ordem":0,"tipo":"SECAO","bloco":null,"titulo":"Dados","descricao":"Dados do candidato","aviso":"Confira os dados","exibicao":[[{"fato":"QUILOMBOLA","operador":"IGUAL","valor":true}]]}""";

    private const string EtapaRevisaoJson =
        """{"codigo":"REVISAO","ordem":1,"tipo":"BLOCO","bloco":"REVISAO_E_ACEITE","titulo":"Revis\u00E3o","descricao":null,"aviso":null,"exibicao":null}""";

    private const string ItemRendaJson =
        """{"fatoCodigo":"RENDA","ordem":1,"etapaCodigo":"DADOS","rotulo":"Renda familiar","tipoRenderizacao":"NUMERO","formato":"LIVRE","ajuda":"Some a renda de todos","obrigatoriedade":{"tipo":"QUANDO","predicado":[[{"fato":"BAIXA_RENDA","operador":"IGUAL","valor":true}]]},"exibicao":[[{"fato":"QUILOMBOLA","operador":"IGUAL","valor":true}]],"restricoes":[{"tipo":"FAIXA_NUMERICA","minimo":0,"maximo":10},{"tipo":"OPCOES_DAS_RESPOSTAS","fatos":["COR_RACA"]}],"pedirConfirmacao":true,"impedimento":{"quando":[[{"fato":"BAIXA_RENDA","operador":"IGUAL","valor":false}]],"mensagem":"Renda acima do limite"}}""";

    private const string TermoUsoJson =
        """{"codigo":"TERMO_USO","ordem":0,"termoId":"11111111-1111-1111-1111-111111111111","versaoId":"22222222-2222-2222-2222-222222222222","exibicao":[[{"fato":"QUILOMBOLA","operador":"IGUAL","valor":true}]],"obrigatoriedade":{"tipo":"QUANDO","predicado":[[{"fato":"BAIXA_RENDA","operador":"IGUAL","valor":true}]]}}""";

    private const string ItemParentescoJson =
        """{"fatoCodigo":"PARENTESCO","ordem":0,"etapaCodigo":null,"rotulo":"Pergunta","tipoRenderizacao":"BOOLEANO","formato":null,"ajuda":null,"obrigatoriedade":{"tipo":"SEMPRE","predicado":null},"exibicao":null,"restricoes":[],"pedirConfirmacao":false,"impedimento":null}""";

    private const string ItemRendaMembroJson =
        """{"fatoCodigo":"RENDA_MEMBRO","ordem":1,"etapaCodigo":null,"rotulo":"Pergunta","tipoRenderizacao":"BOOLEANO","formato":null,"ajuda":null,"obrigatoriedade":{"tipo":"SEMPRE","predicado":null},"exibicao":null,"restricoes":[],"pedirConfirmacao":false,"impedimento":null}""";

    private const string GrupoFamiliaJson =
        $$"""{"codigo":"FAMILIA","ordem":2,"etapaCodigo":"DADOS","rotulo":"Composi\u00E7\u00E3o familiar","minimo":1,"maximo":5,"incluiCandidato":true,"exibicao":[[{"fato":"QUILOMBOLA","operador":"IGUAL","valor":true}]],"obrigatoriedade":{"tipo":"QUANDO","predicado":[[{"fato":"BAIXA_RENDA","operador":"IGUAL","valor":true}]]},"subitens":[{{ItemParentescoJson}},{{ItemRendaMembroJson}}]}""";

    private const string DocumentoCertidaoJson =
        """{"faseCodigo":"HABILITACAO","tipoDocumentoCodigo":"CERTIDAO","obrigatorio":false,"quando":[[{"fato":"QUILOMBOLA","operador":"IGUAL","valor":true}]],"consequenciaIndeferimento":"PERDA_DA_VAGA","basesLegais":[{"referencia":"Lei 12.711/2012","abrangencia":"NACIONAL","status":"VIGENTE","observacao":"Art. 3"},{"referencia":"Portaria 18/2012","abrangencia":"NACIONAL","status":"VIGENTE","observacao":null}],"porOpcao":{"fatoCodigo":"OPCAO_DE_CURSO","exceto":["MEDICINA","ODONTOLOGIA"]}}""";

    private const string ItemQuilombolaJson =
        """{"fatoCodigo":"QUILOMBOLA","ordem":0,"etapaCodigo":null,"rotulo":"Pergunta","tipoRenderizacao":"BOOLEANO","formato":null,"ajuda":null,"obrigatoriedade":{"tipo":"SEMPRE","predicado":null},"exibicao":null,"restricoes":[],"pedirConfirmacao":false,"impedimento":null}""";

    private const string EtapaDadosMinimaJson =
        """{"codigo":"DADOS","ordem":0,"tipo":"SECAO","bloco":null,"titulo":"Dados","descricao":null,"aviso":null,"exibicao":null}""";

    private const string TermoUsoMinimoJson =
        """{"codigo":"TERMO_USO","ordem":0,"termoId":"11111111-1111-1111-1111-111111111111","versaoId":"22222222-2222-2222-2222-222222222222","exibicao":null,"obrigatoriedade":{"tipo":"SEMPRE","predicado":null}}""";

    private const string GrupoFamiliaMinimoJson =
        $$"""{"codigo":"FAMILIA","ordem":0,"etapaCodigo":null,"rotulo":"Fam\u00EDlia","minimo":1,"maximo":null,"incluiCandidato":false,"exibicao":null,"obrigatoriedade":{"tipo":"NUNCA","predicado":null},"subitens":[{{ItemParentescoJson}}]}""";

    private const string DocumentoMinimoJson =
        """{"faseCodigo":"INSCRICAO","tipoDocumentoCodigo":"RG","obrigatorio":true,"quando":null,"consequenciaIndeferimento":"INDEFERIMENTO","basesLegais":[],"porOpcao":null}""";

    private const string JsonCompleto =
        $$"""{"titulo":"Inscri\u00E7\u00E3o \u0022Medicina\u0022 \u003C2027\u003E","etapas":[{{EtapaDadosJson}},{{EtapaRevisaoJson}}],"itens":[{{ItemRendaJson}}],"termos":[{{TermoUsoJson}}],"pressupostos":["COR_RACA"],"grupos":[{{GrupoFamiliaJson}}],"documentosExigidos":[{{DocumentoCertidaoJson}}]}""";

    private const string JsonMinimo =
        $$"""{"titulo":null,"etapas":[{{EtapaDadosMinimaJson}}],"itens":[{{ItemQuilombolaJson}}],"termos":[{{TermoUsoMinimoJson}}],"pressupostos":[],"grupos":[{{GrupoFamiliaMinimoJson}}],"documentosExigidos":[{{DocumentoMinimoJson}}]}""";

    private const string JsonLegado =
        """{"titulo":"Legado","etapas":[{"codigo":"DADOS","ordem":0,"tipo":"SECAO","titulo":"Dados"}],"itens":[{"fatoCodigo":"QUILOMBOLA","ordem":0,"rotulo":"Pergunta","tipoRenderizacao":"BOOLEANO","obrigatoriedade":{"tipo":"SEMPRE"},"restricoes":[],"pedirConfirmacao":false}],"termos":[],"pressupostos":[],"grupos":[{"codigo":"FAMILIA","ordem":0,"rotulo":"Fam\u00EDlia","minimo":1,"maximo":null,"obrigatoriedade":{"tipo":"NUNCA"},"subitens":[],"incluiCandidato":false}]}""";

    private const string JsonLegadoRegravado =
        $$"""{"titulo":"Legado","etapas":[{"codigo":"DADOS","ordem":0,"tipo":"SECAO","bloco":null,"titulo":"Dados","descricao":null,"aviso":null,"exibicao":null}],"itens":[{{ItemQuilombolaJson}}],"termos":[],"pressupostos":[],"grupos":[{"codigo":"FAMILIA","ordem":0,"etapaCodigo":null,"rotulo":"Fam\u00EDlia","minimo":1,"maximo":null,"incluiCandidato":false,"exibicao":null,"obrigatoriedade":{"tipo":"NUNCA","predicado":null},"subitens":[]}],"documentosExigidos":[]}""";

    private const string JsonDocumentoLegado =
        """{"titulo":null,"etapas":[],"itens":[],"termos":[],"pressupostos":[],"grupos":[],"documentosExigidos":[{"faseCodigo":"INSCRICAO","tipoDocumentoCodigo":"RG","obrigatorio":true,"consequenciaIndeferimento":"INDEFERIMENTO","basesLegais":[{"referencia":"Lei 1","abrangencia":"NACIONAL","status":"VIGENTE"}],"porOpcao":null}]}""";

    private const string JsonDocumentoLegadoRegravado =
        """{"titulo":null,"etapas":[],"itens":[],"termos":[],"pressupostos":[],"grupos":[],"documentosExigidos":[{"faseCodigo":"INSCRICAO","tipoDocumentoCodigo":"RG","obrigatorio":true,"quando":null,"consequenciaIndeferimento":"INDEFERIMENTO","basesLegais":[{"referencia":"Lei 1","abrangencia":"NACIONAL","status":"VIGENTE","observacao":null}],"porOpcao":null}]}""";

    private static ConteudoDoModelo ConteudoCompleto() => new(
        "Inscrição \"Medicina\" <2027>",
        [EtapaCompleta(), new EtapaDoModelo("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null)],
        [ItemCompleto()],
        [new TermoDoModelo(
            "TERMO_USO", 0, TermoId, VersaoId,
            new ExibicaoEObrigatoriedade(Quando("QUILOMBOLA", true), Obrigatoriedade.Quando(Quando("BAIXA_RENDA", true))))],
        ["COR_RACA"],
        [GrupoCompleto()],
        [DocumentoCompleto()]);

    private static ConteudoDoModelo ConteudoMinimo() => new(
        null,
        [new EtapaDoModelo("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null)],
        [ItemMinimo("QUILOMBOLA", 0)],
        [new TermoDoModelo("TERMO_USO", 0, TermoId, VersaoId, new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre))],
        [],
        [new GrupoDoModelo(
            "FAMILIA", 0, null, "Família", new OcorrenciasDoGrupo(1, null, IncluiCandidato: false), new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Nunca), [ItemMinimo("PARENTESCO", 0)])],
        [new DocumentoExigidoDoModelo("INSCRICAO", "RG", true, null, "INDEFERIMENTO", [])]);

    private static EtapaDoModelo EtapaCompleta() => new(
        "DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", "Dados do candidato", "Confira os dados", Quando("QUILOMBOLA", true));

    private static ItemDoModelo ItemCompleto() => new(
        "RENDA",
        1,
        "DADOS",
        new ApresentacaoDoCampo("Renda familiar", TipoRenderizacao.Numero, "Some a renda de todos", pedirConfirmacao: true),
        "LIVRE",
        new RegrasDoCampo(
            Quando("QUILOMBOLA", true),
            Obrigatoriedade.Quando(Quando("BAIXA_RENDA", true)),
            [new FaixaNumerica(0, 10), new OpcoesDasRespostas(["COR_RACA"])],
            new Impedimento(Quando("BAIXA_RENDA", false), "Renda acima do limite")));

    private static ItemDoModelo ItemMinimo(string fatoCodigo, int ordem) =>
        new(fatoCodigo, ordem, null, new ApresentacaoDoCampo("Pergunta", TipoRenderizacao.Booleano), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []));

    private static GrupoDoModelo GrupoCompleto() => new(
        "FAMILIA", 2, "DADOS", "Composição familiar", new OcorrenciasDoGrupo(1, 5, true),
        new ExibicaoEObrigatoriedade(Quando("QUILOMBOLA", true), Obrigatoriedade.Quando(Quando("BAIXA_RENDA", true))),
        [ItemMinimo("PARENTESCO", 0), ItemMinimo("RENDA_MEMBRO", 1)]);

    private static DocumentoExigidoDoModelo DocumentoCompleto() => new(
        "HABILITACAO", "CERTIDAO", false, Quando("QUILOMBOLA", true), "PERDA_DA_VAGA",
        [new BaseLegalDoDocumentoDoModelo("Lei 12.711/2012", "NACIONAL", "VIGENTE", "Art. 3"), new BaseLegalDoDocumentoDoModelo("Portaria 18/2012", "NACIONAL", "VIGENTE", null)],
        new ExigenciaPorOpcao("OPCAO_DE_CURSO", ["MEDICINA", "ODONTOLOGIA"]));

    private static PredicadoDnf Quando(string fato, bool valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;
}
