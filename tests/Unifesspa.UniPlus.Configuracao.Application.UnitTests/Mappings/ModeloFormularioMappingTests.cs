namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Mappings;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ModeloFormularioMappingTests
{
    private static readonly Guid TermoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid VersaoId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact(DisplayName = "O conteúdo com todos os opcionais presentes vira a entrada com cada parte no seu campo")]
    public void ParaEntrada_ConteudoComOpcionaisPresentes_ProjetaCadaParteNoSeuCampo()
    {
        ConteudoDoModeloInput entrada = ConteudoCompleto().ParaEntrada();

        Serializar(entrada).Should().Be(Serializar(EntradaCompleta()));
    }

    [Fact(DisplayName = "O conteúdo com os opcionais ausentes vira a entrada com os opcionais nulos e as listas vazias")]
    public void ParaEntrada_ConteudoComOpcionaisAusentes_ProjetaNuloEListaVazia()
    {
        ConteudoDoModeloInput entrada = ConteudoMinimo().ParaEntrada();

        Serializar(entrada).Should().Be(Serializar(EntradaMinima()));
    }

    [Fact(DisplayName = "O conteúdo sem conjunto de documentos vira a entrada com as listas de documentos e de grupos vazias")]
    public void ParaEntrada_ConteudoSemConjuntoDeDocumentos_ProjetaListasVazias()
    {
        ConteudoDoModeloInput entrada = (ConteudoMinimo() with { ConjuntoDeDocumentos = null }).ParaEntrada();

        entrada.ConjuntoDeDocumentos!.DocumentosExigidos.Should().BeEmpty();
        entrada.ConjuntoDeDocumentos.GruposDeDocumentos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O grupo de documentos vai para a entrada e para a leitura com a quantidade, o indeferimento e os documentos dele")]
    public void ParaEntradaEToDto_GrupoDeDocumentos_ProjetaQuantidadeIndeferimentoEDocumentos()
    {
        ConteudoDoModelo conteudo = ConteudoMinimo() with { ConjuntoDeDocumentos = new([], [GrupoDeDocumentosCompleto(), GrupoDeDocumentosSemIndeferimento()]) };

        ConjuntoDeDocumentosDoModeloInput entrada = conteudo.ParaEntrada().ConjuntoDeDocumentos!;
        ConjuntoDeDocumentosDoModeloDto leitura = conteudo.ToDto().ConjuntoDeDocumentos;

        entrada.GruposDeDocumentos!.Select(static g => (g.QuantidadeMinima, g.Indeferimento?.ConsequenciaDoDescumprimento, g.Documentos!.Count)).Should().Equal((2, "RETIRA_A_MODALIDADE", 2), (null, null, 1));
        entrada.GruposDeDocumentos![0].Indeferimento!.BasesLegais!.Should().ContainSingle().Which!.Referencia.Should().Be("Lei 12.711/2012");
        leitura.GruposDeDocumentos.Select(static g => (g.QuantidadeMinima, g.Indeferimento?.ConsequenciaDoDescumprimento, g.Documentos.Count)).Should().Equal((2, "RETIRA_A_MODALIDADE", 2), (null, null, 1));
        leitura.GruposDeDocumentos[0].Indeferimento!.BasesLegais.Should().ContainSingle().Which.Referencia.Should().Be("Lei 12.711/2012");
    }

    [Fact(DisplayName = "O conteúdo completo na leitura tem o mesmo JSON da entrada que a escrita recebe")]
    public void ToDto_ConteudoCompleto_TemOMesmoJsonDaEntradaDaEscrita()
    {
        ConteudoDoModeloDto leitura = ConteudoCompleto().ToDto();

        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(leitura), JsonSerializer.SerializeToNode(EntradaCompleta())).Should().BeTrue();
    }

    [Fact(DisplayName = "O conteúdo mínimo na leitura traz os blocos presentes: listas vazias, exibição nula e documentos sem condição")]
    public void ToDto_ConteudoMinimo_TrazOsBlocosPresentes()
    {
        ConteudoDoModeloDto leitura = ConteudoMinimo().ToDto();

        leitura.Pressupostos.Should().BeEmpty();
        leitura.Itens.Single().Regras.Exibicao.Should().BeNull();
        leitura.ConjuntoDeDocumentos.DocumentosExigidos.Single().Quando.Should().BeNull();
        leitura.ConjuntoDeDocumentos.DocumentosExigidos.Single().PorOpcao.Should().BeNull();
    }

    [Fact(DisplayName = "Os requisitos escolhidos vão para a entrada com o código de cada enum e a fase pelo código")]
    public void ParaEntrada_RequisitosEscolhidos_ProjetaOsCodigos()
    {
        ConteudoDoModelo conteudo = ConteudoComRequisitosEscolhidos();

        RequisitosDoDocumentoDoModeloInput requisitos = conteudo.ParaEntrada().ConjuntoDeDocumentos!.DocumentosExigidos!.Single().Requisitos!;

        requisitos.Should().Be(RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos()
            .ComPrazoDe(90, "DIAS", RequisitosDoDocumentoDoModeloBuilder.NoInicioDaFase("HABILITACAO"))
            .ExigindoValidadeNa(new DataDeReferenciaDoDocumentoDoModeloInput("DATA_ESPECIFICA", new DateOnly(2027, 2, 1), null)).Construir());
    }

    [Fact(DisplayName = "Os requisitos escolhidos na leitura trazem código e rótulo de cada enum")]
    public void ToDto_RequisitosEscolhidos_TrazCodigoERotulo()
    {
        ConteudoDoModelo conteudo = ConteudoComRequisitosEscolhidos();

        RequisitosDoDocumentoDoModeloDto requisitos = conteudo.ToDto().ConjuntoDeDocumentos.DocumentosExigidos.Single().Requisitos;

        requisitos.PrazoDeEmissao!.Escolha.Codigo.Should().Be("COM_PRAZO");
        requisitos.PrazoDeEmissao.Escolha.Rotulo.Should().NotBeNullOrWhiteSpace();
        requisitos.PrazoDeEmissao.Unidade!.Codigo.Should().Be("DIAS");
        requisitos.PrazoDeEmissao.Referencia!.Tipo.Codigo.Should().Be("INICIO_DA_FASE");
        requisitos.PrazoDeEmissao.Referencia.FaseCodigo.Should().Be("HABILITACAO");
        requisitos.PrazoDeEmissao.EfeitoForaDoPrazo!.Codigo.Should().Be("RECUSAR_DOCUMENTO");
        requisitos.Validade!.Escolha.Codigo.Should().Be("EXIGE_DOCUMENTO_VALIDO");
        requisitos.Validade.Referencia!.Data.Should().Be(new DateOnly(2027, 2, 1));
        requisitos.Validade.EfeitoDoVencimento!.Codigo.Should().Be("ACEITAR_COM_AVISO");
    }

    [Fact(DisplayName = "Os requisitos ainda não escolhidos na leitura trazem o prazo e a validade nulos")]
    public void ToDto_RequisitosAindaNaoEscolhidos_TrazPrazoEValidadeNulos()
    {
        RequisitosDoDocumentoDoModeloDto requisitos = ConteudoMinimo().ToDto().ConjuntoDeDocumentos.DocumentosExigidos.Single().Requisitos;

        requisitos.PrazoDeEmissao.Should().BeNull();
        requisitos.Validade.Should().BeNull();
    }

    [Fact(DisplayName = "O conteúdo nulo é recusado")]
    public void ParaEntrada_ConteudoNulo_Lanca()
    {
        Action projetar = () => ((ConteudoDoModelo)null!).ParaEntrada();

        projetar.Should().Throw<ArgumentNullException>();
    }

    private static RequisitosDoDocumentoDoModeloInput NenhumaEscolha { get; } = RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos().Construir();

    private static string Serializar(ConteudoDoModeloInput entrada) => JsonSerializer.Serialize(entrada);

    private static ConteudoDoModelo ConteudoCompleto() => new(
        "Inscrição Medicina",
        [PaginaCompleta(), new PaginaDoModelo("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null)],
        [ItemCompleto()],
        [new TermoDoModelo(
            "TERMO_USO", 0, TermoId, VersaoId,
            new ExibicaoEObrigatoriedade(Quando("QUILOMBOLA", true), Obrigatoriedade.Quando(Quando("BAIXA_RENDA", true))))],
        ["COR_RACA"],
        [GrupoCompleto()],
        new ConjuntoDeDocumentosDoModelo([DocumentoCompleto()], [GrupoDeDocumentosCompleto(), GrupoDeDocumentosSemIndeferimento()]));

    private static ConteudoDoModelo ConteudoMinimo() => new(
        null,
        [new PaginaDoModelo("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", null, null, null)],
        [ItemMinimo("QUILOMBOLA", 0)],
        [new TermoDoModelo("TERMO_USO", 0, TermoId, VersaoId, new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre))],
        [],
        [new GrupoDoModelo(
            "FAMILIA", 0, null, "Família", new OcorrenciasDoGrupo(1, null, IncluiCandidato: false), new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Nunca), [ItemMinimo("PARENTESCO", 0)])],
        new ConjuntoDeDocumentosDoModelo(
            [new DocumentoExigidoDoModelo("INSCRICAO", "RG", null, new IndeferimentoDoDocumentoDoModelo(true, "ELIMINA_DO_PROCESSO", []), RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos)], []));

    private static ConteudoDoModelo ConteudoComRequisitosEscolhidos() =>
        ConteudoMinimo() with { ConjuntoDeDocumentos = new([ConteudoMinimo().Documentos.DocumentosExigidos.Single() with { Requisitos = RequisitosEscolhidos() }], []) };

    private static RequisitosDoDocumentoDoModelo RequisitosEscolhidos() => new(
        new PrazoDeEmissaoDoModelo(
            EscolhaDoPrazoDeEmissao.ComPrazo, 90, UnidadeDoPrazoDeEmissao.Dias,
            new DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia.InicioDaFase, "HABILITACAO", null), EfeitoNaAnaliseDoDocumento.RecusarDocumento),
        new ValidadeDoDocumentoDoModelo(
            EscolhaDaValidade.ExigeDocumentoValido,
            new DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia.DataEspecifica, null, new DateOnly(2027, 2, 1)),
            EfeitoNaAnaliseDoDocumento.AceitarComAviso));

    private static PaginaDoModelo PaginaCompleta() => new(
        "DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", "Dados do candidato", "Confira os dados", Quando("QUILOMBOLA", true));

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
            false, "RETIRA_A_MODALIDADE",
            [new BaseLegalDoModelo("Lei 12.711/2012", "NACIONAL", "VIGENTE", "Art. 3"), new BaseLegalDoModelo("Portaria 18/2012", "NACIONAL", "VIGENTE", null)]),
        RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos,
        new ExigenciaPorOpcao("OPCAO_DE_CURSO", ["MEDICINA"]));

    private static GrupoDeDocumentosDoModelo GrupoDeDocumentosCompleto() => new(
        2,
        new IndeferimentoDoGrupoDeDocumentosDoModelo("RETIRA_A_MODALIDADE", [new BaseLegalDoModelo("Lei 12.711/2012", "NACIONAL", "VIGENTE", "Art. 3")]),
        [DocumentoCompleto(), DocumentoCompleto() with { TipoDocumentoCodigo = "RG" }]);

    private static GrupoDeDocumentosDoModelo GrupoDeDocumentosSemIndeferimento() => new(null, null, [DocumentoCompleto()]);

    private static ConteudoDoModeloInput EntradaCompleta() => new(
        "Inscrição Medicina",
        [PaginaCompletaNaEntrada(), EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão").Construir()],
        [ItemCompletoNaEntrada()],
        [
            EntradaDoTermoExigidoDeTeste.Exigindo("TERMO_USO").DaVersao(TermoId, VersaoId).ExibidoQuando(QuandoNaEntrada("QUILOMBOLA", true))
                .ComObrigatoriedade("QUANDO", QuandoNaEntrada("BAIXA_RENDA", true)).Construir(),
        ],
        ["COR_RACA"],
        [GrupoCompletoNaEntrada()],
        ConjuntoDeDocumentosDoModeloBuilder.Vazio()
            .ComDocumentos(DocumentoCompletoNaEntrada())
            .ComGrupos(GrupoDeDocumentosCompletoNaEntrada(), ConjuntoDeDocumentosDoModeloBuilder.Grupo(null, DocumentoCompletoNaEntrada()))
            .Construir());

    private static ConteudoDoModeloInput EntradaMinima() => new(
        null,
        [EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir()],
        [ItemMinimoNaEntrada("QUILOMBOLA", 0)],
        [EntradaDoTermoExigidoDeTeste.Exigindo("TERMO_USO").DaVersao(TermoId, VersaoId).Construir()],
        [],
        [EntradaDoGrupoColetadoDeTeste.Compondo("FAMILIA").ComRotulo("Família").ComOcorrencias(1)
            .ComCampos(ItemMinimoNaEntrada("PARENTESCO", 0)).Construir()],
        ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(
            new DocumentoExigidoDoModeloInput("INSCRICAO", "RG", null, new IndeferimentoDoDocumentoDoModeloInput(true, "ELIMINA_DO_PROCESSO", [], false), NenhumaEscolha)));

    private static PaginaDoFormularioInput PaginaCompletaNaEntrada() =>
        EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").ComDescricao("Dados do candidato").ComAviso("Confira os dados")
            .ExibidaQuando(QuandoNaEntrada("QUILOMBOLA", true)).Construir();

    private static FatoColetadoInput ItemCompletoNaEntrada() =>
        EntradaDoFatoColetadoDeTeste.Coletando("RENDA", 1)
            .ComRotulo("Renda familiar").DoTipo("NUMERO").ComAjuda("Some a renda de todos").PedindoConfirmacao().NaSecao("DADOS")
            .ExibidoQuando(QuandoNaEntrada("QUILOMBOLA", true))
            .ComObrigatoriedade("QUANDO", QuandoNaEntrada("BAIXA_RENDA", true))
            .ComRestricoes(new RestricaoValorInput("FAIXA_NUMERICA", 0, 10), new RestricaoValorInput("OPCOES_DAS_RESPOSTAS", Fatos: ["COR_RACA"]))
            .ComImpedimento(new ImpedimentoInput(QuandoNaEntrada("BAIXA_RENDA", false), "Renda acima do limite"))
            .Construir();

    private static FatoColetadoInput ItemMinimoNaEntrada(string fatoCodigo, int ordem) =>
        EntradaDoFatoColetadoDeTeste.Coletando(fatoCodigo, ordem).ComRotulo("Pergunta").ComRestricoes().Construir();

    private static GrupoColetadoInput GrupoCompletoNaEntrada() =>
        EntradaDoGrupoColetadoDeTeste.Compondo("FAMILIA", 2).ComRotulo("Composição familiar").NaSecao("DADOS")
            .ComOcorrencias(1, 5).IncluindoOCandidato().ExibidoQuando(QuandoNaEntrada("QUILOMBOLA", true))
            .ComObrigatoriedade("QUANDO", QuandoNaEntrada("BAIXA_RENDA", true))
            .ComCampos(ItemMinimoNaEntrada("PARENTESCO", 0), ItemMinimoNaEntrada("RENDA_MEMBRO", 1)).Construir();

    private static DocumentoExigidoDoModeloInput DocumentoCompletoNaEntrada() => new(
        "HABILITACAO", "CERTIDAO", QuandoNaEntrada("QUILOMBOLA", true),
        new IndeferimentoDoDocumentoDoModeloInput(
            false, "RETIRA_A_MODALIDADE",
            [new BaseLegalDoModeloInput("Lei 12.711/2012", "NACIONAL", "VIGENTE", "Art. 3"), new BaseLegalDoModeloInput("Portaria 18/2012", "NACIONAL", "VIGENTE", null)], false),
        NenhumaEscolha,
        new ExigenciaPorOpcaoInput("OPCAO_DE_CURSO", ["MEDICINA"]));

    private static GrupoDeDocumentosDoModeloInput GrupoDeDocumentosCompletoNaEntrada() => new(
        2,
        new IndeferimentoDoGrupoDeDocumentosDoModeloInput("RETIRA_A_MODALIDADE", [new BaseLegalDoModeloInput("Lei 12.711/2012", "NACIONAL", "VIGENTE", "Art. 3")], false),
        [DocumentoCompletoNaEntrada(), DocumentoCompletoNaEntrada() with { TipoDocumentoCodigo = "RG" }]);

    private static PredicadoDnf Quando(string fato, bool valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;

    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> QuandoNaEntrada(string fato, bool valor) =>
        [[new CondicaoPrecondicaoInput(fato, "IGUAL", JsonSerializer.SerializeToElement(valor))]];
}
