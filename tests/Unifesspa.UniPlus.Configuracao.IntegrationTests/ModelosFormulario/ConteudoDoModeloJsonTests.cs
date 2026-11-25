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

    [Fact(DisplayName = "O documento gravado sem as chaves do prazo de emissão e da validade é lido como ainda não escolhido")]
    public void Desserializar_DocumentoSemPrazoENemValidade_LeComoAindaNaoEscolhido()
    {
        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(JsonDocumentoLegado);

        conteudo.Documentos.DocumentosExigidos.Single().Requisitos.Should().Be(RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos);
    }

    [Fact(DisplayName = "O conteúdo gravado sem a chave dos grupos de documentos é lido como nenhum grupo e regravado com a lista vazia")]
    public void Desserializar_SemGruposDeDocumentos_LeComoListaVazia()
    {
        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(JsonDocumentoLegado);

        conteudo.Documentos.GruposDeDocumentos.Should().BeEmpty();
        ConteudoDoModeloJson.Serializar(conteudo).Should().Contain("\"gruposDeDocumentos\":[]");
    }

    [Fact(DisplayName = "O grupo de documentos volta do JSON com a quantidade, a consequência e os documentos dele")]
    public void Desserializar_GrupoDeDocumentos_RemontaOGrupo()
    {
        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(JsonCompleto);

        GrupoDeDocumentosDoModelo grupo = conteudo.Documentos.GruposDeDocumentos.Single();
        grupo.QuantidadeMinima.Should().Be(2);
        grupo.Indeferimento!.Consequencia.Should().Be("ELIMINA");
        grupo.Documentos.Should().ContainSingle().Which.TipoDocumentoCodigo.Should().Be("CERTIDAO");
    }

    [Fact(DisplayName = "O grupo sem quantidade e sem consequência é gravado com nulo e volta sem quantidade e sem indeferimento")]
    public void Desserializar_GrupoSemQuantidadeENemConsequencia_VoltaIdenticoAoSerGravado()
    {
        string json = JsonDocumentoLegadoRegravado.Replace(
            "\"gruposDeDocumentos\":[]",
            $"\"gruposDeDocumentos\":[{{\"quantidadeMinima\":null,\"consequenciaIndeferimento\":null,\"basesLegais\":[],\"documentos\":[{DocumentoMinimoJson}]}}]",
            StringComparison.Ordinal);

        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(json);

        GrupoDeDocumentosDoModelo grupo = conteudo.Documentos.GruposDeDocumentos.Single();
        grupo.QuantidadeMinima.Should().BeNull();
        grupo.Indeferimento.Should().BeNull();
        ConteudoDoModeloJson.Serializar(conteudo).Should().Be(json);
    }

    [Fact(DisplayName = "O prazo de emissão e a validade escolhidos voltam do JSON com a fase pelo código e a data específica")]
    public void Desserializar_PrazoEValidadeEscolhidos_RemontaOsRequisitos()
    {
        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(JsonCompleto);

        conteudo.Documentos.DocumentosExigidos.Single().Requisitos.Should().Be(RequisitosCompletos());
    }

    [Fact(DisplayName = "Sem prazo e sem exigir validade, o JSON grava as escolhas com os detalhes nulos e volta idêntico")]
    public void Desserializar_SemPrazoESemValidade_VoltaIdenticoAoSerGravado()
    {
        string json = JsonDocumentoLegadoRegravado.Replace(
            "\"prazoDeEmissao\":null,\"validade\":null", $"\"prazoDeEmissao\":{PrazoSemPrazoJson},\"validade\":{ValidadeNaoExigidaJson}", StringComparison.Ordinal);

        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(json);

        conteudo.Documentos.DocumentosExigidos.Single().Requisitos.Should().Be(new RequisitosDoDocumentoDoModelo(
            new PrazoDeEmissaoDoModelo(EscolhaDoPrazoDeEmissao.SemPrazo, null, null, null, null),
            new ValidadeDoDocumentoDoModelo(EscolhaDaValidade.NaoExigeDocumentoValido, null, null)));
        ConteudoDoModeloJson.Serializar(conteudo).Should().Be(json);
    }

    [Theory(DisplayName = "O código do prazo de emissão ou da validade que o catálogo não declara é dado corrompido e lança")]
    [InlineData("\"escolha\":\"COM_PRAZO\"", "\"escolha\":\"PRAZO_INEXISTENTE\"")]
    [InlineData("\"unidade\":\"DIAS\"", "\"unidade\":\"semanas\"")]
    [InlineData("\"tipo\":\"INICIO_DA_FASE\"", "\"tipo\":\"inicio\"")]
    [InlineData("\"efeitoDoVencimento\":\"ACEITAR_COM_AVISO\"", "\"efeitoDoVencimento\":\"aceitar\"")]
    public void Desserializar_CodigoDeRequisitoForaDoCatalogo_Lanca(string codigo, string corrompido)
    {
        string json = JsonCompleto.Replace(codigo, corrompido, StringComparison.Ordinal);

        Action ler = () => ConteudoDoModeloJson.Desserializar(json);

        ler.Should().Throw<ArgumentException>();
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

    [Theory(DisplayName = "As chaves antigas das páginas e do código da página são dados corrompidos e lançam")]
    [InlineData("\"paginas\":", "\"etapas\":")]
    [InlineData("\"paginaCodigo\":null", "\"etapaCodigo\":null")]
    [InlineData("\"codigo\":\"FAMILIA\",\"ordem\":2,\"paginaCodigo\":\"DADOS\"", "\"codigo\":\"FAMILIA\",\"ordem\":2,\"etapaCodigo\":\"DADOS\"")]
    public void Desserializar_ChaveAntigaDePagina_Lanca(string chave, string chaveAntiga)
    {
        string json = ConteudoDoModeloJson.Serializar(ConteudoCompleto()).Replace(chave, chaveAntiga, StringComparison.Ordinal);

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

    [Fact(DisplayName = "O item gravado usa as chaves de campo e dado e volta com os mesmos valores")]
    public void Serializar_ItemComCampoEDado_GravaAsChavesERemontaOItem()
    {
        ConteudoDoModelo original = ConteudoMinimo();
        string json = ConteudoDoModeloJson.Serializar(original);
        ItemDoModelo item = ConteudoDoModeloJson.Desserializar(json).Itens.Single();

        json.Should().Contain("\"tipoDeCampo\":\"SIM_OU_NAO\"").And.Contain("\"codigoDoDado\":\"QUILOMBOLA\"");
        json.Should().NotContain("\"fatoCodigo\"").And.NotContain("tipoRenderizacao");
        item.Apresentacao.TipoDeCampo.Should().Be(TipoDeCampo.SimOuNao);
        item.FatoCodigo.Should().Be("QUILOMBOLA");
    }

    [Theory(DisplayName = "O tipo de campo do item é gravado no documento pelo código")]
    [InlineData(TipoDeCampo.Numero, "NUMERO")]
    [InlineData(TipoDeCampo.SimOuNao, "SIM_OU_NAO")]
    [InlineData(TipoDeCampo.EscolhaUnica, "ESCOLHA_UNICA")]
    [InlineData(TipoDeCampo.EscolhaMultipla, "ESCOLHA_MULTIPLA")]
    [InlineData(TipoDeCampo.Texto, "TEXTO")]
    [InlineData(TipoDeCampo.Data, "DATA")]
    [InlineData(TipoDeCampo.Endereco, "ENDERECO")]
    [InlineData(TipoDeCampo.Municipio, "MUNICIPIO")]
    public void Serializar_TipoDeCampoDoItem_GravaOCodigo(TipoDeCampo tipo, string codigo)
    {
        ConteudoDoModelo conteudo = ConteudoMinimo() with { Itens = [ItemMinimo("QUILOMBOLA", 0).ComTipoDeCampo(tipo)] };

        ConteudoDoModeloJson.Serializar(conteudo).Should().Contain($"\"tipoDeCampo\":\"{codigo}\"");
    }

    [Theory(DisplayName = "O tipo de campo gravado que o catálogo não declara é dado corrompido e lança")]
    [InlineData("NENHUMA")]
    [InlineData("selecao_unica")]
    [InlineData("0")]
    public void Desserializar_TipoDeCampoForaDoCatalogo_Lanca(string tipo)
    {
        string corrompido = JsonLegado.Replace("\"tipoDeCampo\":\"SIM_OU_NAO\"", $"\"tipoDeCampo\":\"{tipo}\"", StringComparison.Ordinal);

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
        """{"codigo":"DADOS","ordem":0,"tipo":"SECAO","bloco":null,"titulo":"Dados","descricao":"Dados do candidato","aviso":"Confira os dados","exibicao":[[{"codigoDoDado":"QUILOMBOLA","operador":"IGUAL","valor":true}]]}""";

    private const string EtapaRevisaoJson =
        """{"codigo":"REVISAO","ordem":1,"tipo":"BLOCO","bloco":"REVISAO_E_ACEITE","titulo":"Revis\u00E3o","descricao":null,"aviso":null,"exibicao":null}""";

    private const string ItemRendaJson =
        """{"codigoDoDado":"RENDA","ordem":1,"paginaCodigo":"DADOS","rotulo":"Renda familiar","tipoDeCampo":"NUMERO","formato":"LIVRE","ajuda":"Some a renda de todos","obrigatoriedade":{"tipo":"QUANDO","predicado":[[{"codigoDoDado":"BAIXA_RENDA","operador":"IGUAL","valor":true}]]},"exibicao":[[{"codigoDoDado":"QUILOMBOLA","operador":"IGUAL","valor":true}]],"restricoes":[{"tipo":"FAIXA_NUMERICA","minimo":0,"maximo":10},{"tipo":"OPCOES_DAS_RESPOSTAS","codigosDosDados":["COR_RACA"]}],"pedirConfirmacao":true,"impedimento":{"quando":[[{"codigoDoDado":"BAIXA_RENDA","operador":"IGUAL","valor":false}]],"mensagem":"Renda acima do limite"}}""";

    private const string TermoUsoJson =
        """{"codigo":"TERMO_USO","ordem":0,"termoId":"11111111-1111-1111-1111-111111111111","versaoId":"22222222-2222-2222-2222-222222222222","exibicao":[[{"codigoDoDado":"QUILOMBOLA","operador":"IGUAL","valor":true}]],"obrigatoriedade":{"tipo":"QUANDO","predicado":[[{"codigoDoDado":"BAIXA_RENDA","operador":"IGUAL","valor":true}]]}}""";

    private const string ItemParentescoJson =
        """{"codigoDoDado":"PARENTESCO","ordem":0,"paginaCodigo":null,"rotulo":"Pergunta","tipoDeCampo":"SIM_OU_NAO","formato":null,"ajuda":null,"obrigatoriedade":{"tipo":"SEMPRE","predicado":null},"exibicao":null,"restricoes":[],"pedirConfirmacao":false,"impedimento":null}""";

    private const string ItemRendaMembroJson =
        """{"codigoDoDado":"RENDA_MEMBRO","ordem":1,"paginaCodigo":null,"rotulo":"Pergunta","tipoDeCampo":"SIM_OU_NAO","formato":null,"ajuda":null,"obrigatoriedade":{"tipo":"SEMPRE","predicado":null},"exibicao":null,"restricoes":[],"pedirConfirmacao":false,"impedimento":null}""";

    private const string GrupoFamiliaJson =
        $$"""{"codigo":"FAMILIA","ordem":2,"paginaCodigo":"DADOS","rotulo":"Composi\u00E7\u00E3o familiar","minimo":1,"maximo":5,"incluiCandidato":true,"exibicao":[[{"codigoDoDado":"QUILOMBOLA","operador":"IGUAL","valor":true}]],"obrigatoriedade":{"tipo":"QUANDO","predicado":[[{"codigoDoDado":"BAIXA_RENDA","operador":"IGUAL","valor":true}]]},"subitens":[{{ItemParentescoJson}},{{ItemRendaMembroJson}}]}""";

    private const string PrazoCompletoJson =
        """{"escolha":"COM_PRAZO","valor":90,"unidade":"DIAS","referencia":{"tipo":"INICIO_DA_FASE","data":null,"faseCodigo":"HABILITACAO"},"efeitoForaDoPrazo":"RECUSAR_DOCUMENTO"}""";

    private const string ValidadeCompletaJson =
        """{"escolha":"EXIGE_DOCUMENTO_VALIDO","referencia":{"tipo":"DATA_ESPECIFICA","data":"2027-02-01","faseCodigo":null},"efeitoDoVencimento":"ACEITAR_COM_AVISO"}""";

    private const string DocumentoCertidaoJson =
        $$$"""{"faseCodigo":"HABILITACAO","tipoDocumentoCodigo":"CERTIDAO","obrigatorio":false,"quando":[[{"codigoDoDado":"QUILOMBOLA","operador":"IGUAL","valor":true}]],"consequenciaIndeferimento":"PERDA_DA_VAGA","basesLegais":[{"referencia":"Lei 12.711/2012","abrangencia":"NACIONAL","situacao":"VIGENTE","observacao":"Art. 3"},{"referencia":"Portaria 18/2012","abrangencia":"NACIONAL","situacao":"VIGENTE","observacao":null}],"prazoDeEmissao":{{{PrazoCompletoJson}}},"validade":{{{ValidadeCompletaJson}}},"porOpcao":{"codigoDoDado":"OPCAO_DE_CURSO","exceto":["MEDICINA","ODONTOLOGIA"]}}""";

    private const string ItemQuilombolaJson =
        """{"codigoDoDado":"QUILOMBOLA","ordem":0,"paginaCodigo":null,"rotulo":"Pergunta","tipoDeCampo":"SIM_OU_NAO","formato":null,"ajuda":null,"obrigatoriedade":{"tipo":"SEMPRE","predicado":null},"exibicao":null,"restricoes":[],"pedirConfirmacao":false,"impedimento":null}""";

    private const string EtapaDadosMinimaJson =
        """{"codigo":"DADOS","ordem":0,"tipo":"SECAO","bloco":null,"titulo":"Dados","descricao":null,"aviso":null,"exibicao":null}""";

    private const string TermoUsoMinimoJson =
        """{"codigo":"TERMO_USO","ordem":0,"termoId":"11111111-1111-1111-1111-111111111111","versaoId":"22222222-2222-2222-2222-222222222222","exibicao":null,"obrigatoriedade":{"tipo":"SEMPRE","predicado":null}}""";

    private const string GrupoFamiliaMinimoJson =
        $$"""{"codigo":"FAMILIA","ordem":0,"paginaCodigo":null,"rotulo":"Fam\u00EDlia","minimo":1,"maximo":null,"incluiCandidato":false,"exibicao":null,"obrigatoriedade":{"tipo":"NUNCA","predicado":null},"subitens":[{{ItemParentescoJson}}]}""";

    private const string PrazoSemPrazoJson =
        """{"escolha":"SEM_PRAZO","valor":null,"unidade":null,"referencia":null,"efeitoForaDoPrazo":null}""";

    private const string ValidadeNaoExigidaJson =
        """{"escolha":"NAO_EXIGE_DOCUMENTO_VALIDO","referencia":null,"efeitoDoVencimento":null}""";

    private const string DocumentoMinimoJson =
        """{"faseCodigo":"INSCRICAO","tipoDocumentoCodigo":"RG","obrigatorio":true,"quando":null,"consequenciaIndeferimento":"INDEFERIMENTO","basesLegais":[],"prazoDeEmissao":null,"validade":null,"porOpcao":null}""";

    private const string GrupoDeDocumentosJson =
        $$"""{"quantidadeMinima":2,"consequenciaIndeferimento":"ELIMINA","basesLegais":[],"documentos":[{{DocumentoCertidaoJson}}]}""";

    private const string JsonCompleto =
        $$"""{"titulo":"Inscri\u00E7\u00E3o \u0022Medicina\u0022 \u003C2027\u003E","paginas":[{{EtapaDadosJson}},{{EtapaRevisaoJson}}],"itens":[{{ItemRendaJson}}],"termos":[{{TermoUsoJson}}],"pressupostos":["COR_RACA"],"grupos":[{{GrupoFamiliaJson}}],"documentosExigidos":[{{DocumentoCertidaoJson}}],"gruposDeDocumentos":[{{GrupoDeDocumentosJson}}]}""";

    private const string JsonMinimo =
        $$"""{"titulo":null,"paginas":[{{EtapaDadosMinimaJson}}],"itens":[{{ItemQuilombolaJson}}],"termos":[{{TermoUsoMinimoJson}}],"pressupostos":[],"grupos":[{{GrupoFamiliaMinimoJson}}],"documentosExigidos":[{{DocumentoMinimoJson}}],"gruposDeDocumentos":[]}""";

    private const string JsonLegado =
        """{"titulo":"Legado","paginas":[{"codigo":"DADOS","ordem":0,"tipo":"SECAO","titulo":"Dados"}],"itens":[{"codigoDoDado":"QUILOMBOLA","ordem":0,"paginaCodigo":null,"rotulo":"Pergunta","tipoDeCampo":"SIM_OU_NAO","obrigatoriedade":{"tipo":"SEMPRE"},"restricoes":[],"pedirConfirmacao":false}],"termos":[],"pressupostos":[],"grupos":[{"codigo":"FAMILIA","ordem":0,"paginaCodigo":null,"rotulo":"Fam\u00EDlia","minimo":1,"maximo":null,"obrigatoriedade":{"tipo":"NUNCA"},"subitens":[],"incluiCandidato":false}]}""";

    private const string JsonLegadoRegravado =
        $$"""{"titulo":"Legado","paginas":[{"codigo":"DADOS","ordem":0,"tipo":"SECAO","bloco":null,"titulo":"Dados","descricao":null,"aviso":null,"exibicao":null}],"itens":[{{ItemQuilombolaJson}}],"termos":[],"pressupostos":[],"grupos":[{"codigo":"FAMILIA","ordem":0,"paginaCodigo":null,"rotulo":"Fam\u00EDlia","minimo":1,"maximo":null,"incluiCandidato":false,"exibicao":null,"obrigatoriedade":{"tipo":"NUNCA","predicado":null},"subitens":[]}],"documentosExigidos":[],"gruposDeDocumentos":[]}""";

    private const string JsonDocumentoLegado =
        """{"titulo":null,"paginas":[],"itens":[],"termos":[],"pressupostos":[],"grupos":[],"documentosExigidos":[{"faseCodigo":"INSCRICAO","tipoDocumentoCodigo":"RG","obrigatorio":true,"consequenciaIndeferimento":"INDEFERIMENTO","basesLegais":[{"referencia":"Lei 1","abrangencia":"NACIONAL","situacao":"VIGENTE"}],"porOpcao":null}]}""";

    private const string JsonDocumentoLegadoRegravado =
        """{"titulo":null,"paginas":[],"itens":[],"termos":[],"pressupostos":[],"grupos":[],"documentosExigidos":[{"faseCodigo":"INSCRICAO","tipoDocumentoCodigo":"RG","obrigatorio":true,"quando":null,"consequenciaIndeferimento":"INDEFERIMENTO","basesLegais":[{"referencia":"Lei 1","abrangencia":"NACIONAL","situacao":"VIGENTE","observacao":null}],"prazoDeEmissao":null,"validade":null,"porOpcao":null}],"gruposDeDocumentos":[]}""";

    private static ConteudoDoModelo ConteudoCompleto() => new(
        "Inscrição \"Medicina\" <2027>",
        [EtapaCompleta(), new EtapaDoModelo("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null)],
        [ItemCompleto()],
        [new TermoDoModelo(
            "TERMO_USO", 0, TermoId, VersaoId,
            new ExibicaoEObrigatoriedade(Quando("QUILOMBOLA", true), Obrigatoriedade.Quando(Quando("BAIXA_RENDA", true))))],
        ["COR_RACA"],
        [GrupoCompleto()],
        new ConjuntoDeDocumentosDoModelo(
            [DocumentoCompleto()], [new GrupoDeDocumentosDoModelo(2, new IndeferimentoDoGrupoDeDocumentosDoModelo("ELIMINA", []), [DocumentoCompleto()])]));

    private static ConteudoDoModelo ConteudoMinimo() => new(
        null,
        [new EtapaDoModelo("DADOS", 0, TipoEtapaFormulario.Secao, null, "Dados", null, null, null)],
        [ItemMinimo("QUILOMBOLA", 0)],
        [new TermoDoModelo("TERMO_USO", 0, TermoId, VersaoId, new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre))],
        [],
        [new GrupoDoModelo(
            "FAMILIA", 0, null, "Família", new OcorrenciasDoGrupo(1, null, IncluiCandidato: false), new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Nunca), [ItemMinimo("PARENTESCO", 0)])],
        new ConjuntoDeDocumentosDoModelo(
            [new DocumentoExigidoDoModelo("INSCRICAO", "RG", null, new IndeferimentoDoDocumentoDoModelo(true, "INDEFERIMENTO", []), RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos)], []));

    private static EtapaDoModelo EtapaCompleta() => new(
        "DADOS", 0, TipoEtapaFormulario.Secao, null, "Dados", "Dados do candidato", "Confira os dados", Quando("QUILOMBOLA", true));

    private static ItemDoModelo ItemCompleto() => new(
        "RENDA",
        1,
        "DADOS",
        new ApresentacaoDoCampo("Renda familiar", TipoDeCampo.Numero, "Some a renda de todos", pedirConfirmacao: true),
        "LIVRE",
        new RegrasDoCampo(
            Quando("QUILOMBOLA", true),
            Obrigatoriedade.Quando(Quando("BAIXA_RENDA", true)),
            [new FaixaNumerica(0, 10), new OpcoesDasRespostas(["COR_RACA"])],
            new Impedimento(Quando("BAIXA_RENDA", false), "Renda acima do limite")));

    private static ItemDoModelo ItemMinimo(string fatoCodigo, int ordem) =>
        new(fatoCodigo, ordem, null, new ApresentacaoDoCampo("Pergunta", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []));

    private static GrupoDoModelo GrupoCompleto() => new(
        "FAMILIA", 2, "DADOS", "Composição familiar", new OcorrenciasDoGrupo(1, 5, true),
        new ExibicaoEObrigatoriedade(Quando("QUILOMBOLA", true), Obrigatoriedade.Quando(Quando("BAIXA_RENDA", true))),
        [ItemMinimo("PARENTESCO", 0), ItemMinimo("RENDA_MEMBRO", 1)]);

    private static DocumentoExigidoDoModelo DocumentoCompleto() => new(
        "HABILITACAO", "CERTIDAO", Quando("QUILOMBOLA", true),
        new IndeferimentoDoDocumentoDoModelo(
            false, "PERDA_DA_VAGA",
            [new BaseLegalDoModelo("Lei 12.711/2012", "NACIONAL", "VIGENTE", "Art. 3"), new BaseLegalDoModelo("Portaria 18/2012", "NACIONAL", "VIGENTE", null)]),
        RequisitosCompletos(),
        new ExigenciaPorOpcao("OPCAO_DE_CURSO", ["MEDICINA", "ODONTOLOGIA"]));

    private static RequisitosDoDocumentoDoModelo RequisitosCompletos() => new(
        new PrazoDeEmissaoDoModelo(
            EscolhaDoPrazoDeEmissao.ComPrazo, 90, UnidadeDoPrazoDeEmissao.Dias,
            new DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia.InicioDaFase, "HABILITACAO", null), EfeitoNaAnaliseDoDocumento.RecusarDocumento),
        new ValidadeDoDocumentoDoModelo(
            EscolhaDaValidade.ExigeDocumentoValido,
            new DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia.DataEspecifica, null, new DateOnly(2027, 2, 1)),
            EfeitoNaAnaliseDoDocumento.AceitarComAviso));

    private static PredicadoDnf Quando(string fato, bool valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;
}
