namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;
using AwesomeAssertions.Equivalency;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Condicoes = System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Regras.Entradas.CondicaoPrecondicaoInput>>;
using Recusa = (string Campo, string Codigo, string Mensagem);

/// <summary>
/// A leitura do conteúdo do modelo pela forma das regras: acumula as recusas na ordem das partes
/// (páginas, itens, termos, grupos, documentos) e guarda a posição de cada parte lida na entrada.
/// </summary>
public sealed class ConteudoLidoTests
{
    private const string ClausulaSemCondicao = "Uma cláusula deve ter ao menos uma condição.";
    private const string EntradaNula = "A página, o item, o grupo ou o termo veio nulo.";
    private const string ObrigatoriedadeIncoerente = "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.";

    private static readonly Condicoes PredicadoSemCondicao = [[]];
    private static readonly Dictionary<string, string> Formatos = new(StringComparer.Ordinal) { ["CPF_RESPONSAVEL"] = "CPF" };
    private static readonly Guid TermoId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid VersaoId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    [Fact(DisplayName = "A entrada que recusa em toda parte devolve as recusas na ordem da leitura, com a posição de cada uma na entrada")]
    public void Ler_RecusaEmTodasAsPartes_ListaAsRecusasNaOrdemDaLeitura()
    {
        ConteudoDoModeloInput entrada = new(
            "Habilitação", PaginasRecusadas(), ItensRecusados(), TermosRecusados(), ["CERTIFICADO", ElementoNulo<string>()], GruposRecusados(),
            ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(DocumentosRecusados()));

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(entrada);

        leitura.Recusas.Select(static e => (e.Field!, e.Error.Code, e.Error.Message)).Should().Equal(RecusasEsperadas());
        lido.ParaConteudo().Should().BeEquivalentTo(new ConteudoDoModelo("Habilitação", [], [], [], ["CERTIFICADO", string.Empty], [], ConjuntoDeDocumentosDoModelo.Nenhum), PorMembros);
    }

    [Fact(DisplayName = "A entrada válida em toda parte vira o conteúdo do modelo, sem recusa, com o formato do fato vindo do catálogo")]
    public void Ler_EntradaValidaEmTodasAsPartes_ProduzOConteudoEsperado()
    {
        ConteudoDoModeloInput entrada = new(
            "Habilitação", [PaginaValida()], [ItemValido()], [TermoValido()], ["CERTIFICADO"], [GrupoValido()], ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(DocumentoValido()));

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(entrada);

        leitura.Recusas.Should().BeEmpty();
        lido.ParaConteudo().Should().BeEquivalentTo(
            new ConteudoDoModelo("Habilitação", [PaginaEsperada()], [ItemEsperado()], [TermoEsperado()], ["CERTIFICADO"], [GrupoEsperado()], new ConjuntoDeDocumentosDoModelo([DocumentoEsperado()], [])),
            PorMembros);
    }

    [Fact(DisplayName = "A pagina com tipo e bloco que não são códigos do contrato é recusada em cada campo, depois da exibição, e não é montada")]
    public void Ler_PaginaComTipoEBlocoDesconhecidos_RecusaCadaCampoSemMontarAPagina()
    {
        PaginaDoFormularioInput pagina = PaginaValida() with { Exibicao = PredicadoSemCondicao, Tipo = "secao", Bloco = "RODAPE" };

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, [pagina], null, null, null));

        leitura.Recusas.Select(static e => (e.Field!, e.Error.Code)).Should().Equal(
            ("paginas[0].exibicao[0]", "ClausulaDnf.ClausulaVazia"),
            ("paginas[0].tipo", EstruturaFormularioErrorCodes.TipoDePaginaObrigatorio),
            ("paginas[0].bloco", EstruturaFormularioErrorCodes.BlocoNaoAdmitido));
        lido.Paginas.Should().BeEmpty();
    }

    [Fact(DisplayName = "As condições inválidas da exibição e do impedimento apontam os respectivos campos")]
    public void Ler_CondicoesInvalidas_RecusaNoCampoDeCadaRegra()
    {
        Condicoes condicoes = [[new CondicaoPrecondicaoInput(string.Empty, "IGUAL", JsonSerializer.SerializeToElement(true))]];
        FatoColetadoInput item = Item("CERTIFICADO", 0).ExibidoQuando(condicoes)
            .ComImpedimento(new ImpedimentoInput(condicoes, null)).Construir();
        LeituraDoConteudo leitura = new(Formatos);

        leitura.Ler(new ConteudoDoModeloInput(null, null, [item], null, null));

        leitura.Recusas.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].regras.exibicao[0][0].fato", "CondicaoDnf.FatoObrigatorio"),
            ("itens[0].regras.impedimento.quando[0][0].fato", "CondicaoDnf.FatoObrigatorio"));
    }

    [Fact(DisplayName = "O bloco ausente é a pagina sem bloco, e o código do bloco lido vai para a pagina")]
    public void Ler_BlocoAusenteOuDeclarado_VaiParaAPagina()
    {
        PaginaDoFormularioInput comBloco = EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").Construir();

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, [PaginaValida(), comBloco], null, null, null));

        leitura.Recusas.Should().BeEmpty();
        lido.Paginas.Select(static e => (e.Pagina.Tipo, e.Pagina.Bloco)).Should().Equal(
            (TipoPaginaFormulario.Secao, (BlocoSistema?)null), (TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite));
    }

    [Fact(DisplayName = "A parte lida guarda a posição dela na entrada; a nula e a recusada ficam de fora")]
    public void Ler_PartesLidasEEntreRecusadas_GuardamAPosicaoNaEntrada()
    {
        ConteudoLido lido = new LeituraDoConteudo(Formatos).Ler(ComPartesRecusadasNoMeio());

        lido.Paginas.Select(static e => e.Indice).Should().Equal(0, 3);
        lido.Itens.Select(static i => i.Indice).Should().Equal(0, 2);
        lido.Termos.Select(static t => t.Indice).Should().Equal(1);
        lido.Grupos.Select(static g => g.Indice).Should().Equal(0, 2);
        lido.Documentos.DocumentosExigidos.Select(static d => d.Indice).Should().Equal(0, 3);
    }

    [Fact(DisplayName = "A entrada ausente ou com as listas ausentes é lida como conteúdo vazio, sem recusa")]
    public void Ler_EntradaOuListasAusentes_ConteudoVazioSemRecusa()
    {
        LeituraDoConteudo leituraSemEntrada = new(Formatos);
        LeituraDoConteudo leituraSemListas = new(Formatos);
        ConteudoLido semEntrada = leituraSemEntrada.Ler(null);
        ConteudoLido semListas = leituraSemListas.Ler(new ConteudoDoModeloInput(null, null, null, null, null));

        leituraSemEntrada.Recusas.Should().BeEmpty();
        leituraSemListas.Recusas.Should().BeEmpty();
        semEntrada.ParaConteudo().Should().BeEquivalentTo(new ConteudoDoModelo(null, [], [], [], [], [], ConjuntoDeDocumentosDoModelo.Nenhum), PorMembros);
        semListas.ParaConteudo().Should().BeEquivalentTo(new ConteudoDoModelo(null, [], [], [], [], [], ConjuntoDeDocumentosDoModelo.Nenhum), PorMembros);
    }

    [Fact(DisplayName = "O documento sem bases legais e com a exigência por opção sem exceções é lido sem recusa, com as duas listas vazias")]
    public void Ler_DocumentoSemBasesLegaisNemExcecoes_LeSemRecusa()
    {
        DocumentoExigidoDoModeloInput documento = DocumentoValido() with { Indeferimento = new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", null, false), PorOpcao = new ExigenciaPorOpcaoInput("CONDICAO", null) };

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(documento)));

        leitura.Recusas.Should().BeEmpty();
        lido.Documentos.DocumentosExigidos.Single().Documento.Indeferimento.BasesLegais.Should().BeEmpty();
        lido.Documentos.DocumentosExigidos.Single().Documento.PorOpcao!.Exceto.Should().BeEmpty();
    }

    [Fact]
    public void Ler_DocumentoEGrupoSemConsequencia_PreservaReenvio()
    {
        DocumentoExigidoDoModeloInput documento = DocumentoValido() with { Indeferimento = new(true, null, null, true) };
        GrupoDeDocumentosDoModeloInput grupo = new(1, new(null, null, true), [documento]);
        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, new([documento], [grupo])));
        leitura.Recusas.Should().BeEmpty();
        lido.Documentos.DocumentosExigidos.Single().Documento.Indeferimento.ConsequenciaDoDescumprimento.Should().BeNull();
        lido.Documentos.GruposDeDocumentos.Single().Grupo.Grupo.Indeferimento!.AbrePrazoParaReenvio.Should().BeTrue();
    }

    [Fact(DisplayName = "Os requisitos do documento são lidos com a fase de cada referência pelo código")]
    public void Ler_DocumentoComRequisitos_GuardaOsRequisitosDeclarados()
    {
        RequisitosDoDocumentoDoModeloInput requisitos = RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos()
            .ComPrazoDe(90, "DIAS", RequisitosDoDocumentoDoModeloBuilder.NoInicioDaFase("HABILITACAO")).Construir();
        DocumentoExigidoDoModeloInput documento = DocumentoValido() with { Requisitos = requisitos };

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(documento)));

        leitura.Recusas.Should().BeEmpty();
        lido.Documentos.DocumentosExigidos.Single().Documento.Requisitos.PrazoDeEmissao!.Referencia!.FaseCodigo.Should().Be("HABILITACAO");
        lido.Documentos.DocumentosExigidos.Single().Documento.Requisitos.Validade.Should().BeNull();
    }

    [Fact(DisplayName = "O código desconhecido dos requisitos é recusado no caminho do documento, depois do indeferimento e antes da exigência por opção")]
    public void Ler_DocumentoComRequisitoDesconhecido_RecusaNoCaminhoDoDocumento()
    {
        DocumentoExigidoDoModeloInput documento = DocumentoValido() with
        {
            Indeferimento = new IndeferimentoDoDocumentoDoModeloInput(true, string.Empty, [], false),
            Requisitos = RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos().ComEscolhaDePrazo("naoExiste").Construir(),
            PorOpcao = new ExigenciaPorOpcaoInput(string.Empty, []),
        };

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(documento)));

        leitura.Recusas.Select(static e => e.Field).Should().Equal(
            "conjuntoDeDocumentos.documentosExigidos[0].indeferimento.consequenciaDoDescumprimento", "conjuntoDeDocumentos.documentosExigidos[0].requisitos.prazoDeEmissao.escolha", "conjuntoDeDocumentos.documentosExigidos[0].porOpcao.fatoCodigo");
        lido.Documentos.DocumentosExigidos.Should().BeEmpty();
    }

    private static ConteudoDoModeloInput ComPartesRecusadasNoMeio() => new(
        "Habilitação",
        [PaginaValida(), ElementoNulo<PaginaDoFormularioInput>(), PaginaValida() with { Exibicao = PredicadoSemCondicao }, PaginaValida()],
        [ItemValido(), ElementoNulo<FatoColetadoInput>(), ItemValido()],
        [ElementoNulo<TermoExigidoInput>(), TermoValido()],
        [],
        [GrupoValido(), GrupoValido() with { Regras = new ExibicaoEObrigatoriedadeInput(null, new ObrigatoriedadeInput("TALVEZ")) }, GrupoValido()],
        ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(
            DocumentoValido(), ElementoNulo<DocumentoExigidoDoModeloInput>(), DocumentoValido() with { FaseCodigo = string.Empty }, DocumentoValido()));

    private static EquivalencyOptions<ConteudoDoModelo> PorMembros(EquivalencyOptions<ConteudoDoModelo> opcoes) => opcoes
        .ComparingRecordsByMembers()
        .Using<JsonElement>(static contexto => JsonElement.DeepEquals(contexto.Subject, contexto.Expectation).Should().BeTrue())
        .WhenTypeIs<JsonElement>();

    private static Recusa[] RecusasEsperadas() =>
        [.. RecusasDasPaginas(), .. RecusasDosItens(), .. RecusasDosTermos(), .. RecusasDosGrupos(), .. RecusasDosDocumentos()];

    private static PaginaDoFormularioInput[] PaginasRecusadas() =>
        [ElementoNulo<PaginaDoFormularioInput>(), EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 1).ComTitulo("Dados").ExibidaQuando(PredicadoSemCondicao).Construir()];

    private static Recusa[] RecusasDasPaginas() =>
        [Malformada("paginas[0]", EntradaNula), ClausulaVazia("paginas[1].exibicao")];

    private static FatoColetadoInput[] ItensRecusados() =>
    [
        ElementoNulo<FatoColetadoInput>(),
        Item("CERTIFICADO", 1)
            .ExibidoQuando(PredicadoSemCondicao)
            .ComObrigatoriedade("QUANDO", PredicadoSemCondicao)
            .ComRestricoes(ElementoNulo<RestricaoValorInput>(), new RestricaoValorInput("DESCONHECIDA"))
            .ComImpedimento(new ImpedimentoInput(PredicadoSemCondicao, null))
            .Construir(),
        Item("MAIOR_IDADE", 2).ComObrigatoriedade("QUANDO").ComImpedimento(new ImpedimentoInput(null, null)).Construir(),
    ];

    private static Recusa[] RecusasDosItens() =>
    [
        Malformada("itens[0]", EntradaNula),
        ClausulaVazia("itens[1].regras.exibicao"),
        ClausulaVazia("itens[1].regras.obrigatoriedade.predicado"),
        ("itens[1].regras.restricoes[0]", RestricaoValorErrorCodes.TipoDesconhecido, "A restrição de valor é nula."),
        ("itens[1].regras.restricoes[1]", RestricaoValorErrorCodes.TipoDesconhecido,
            "O tipo da restrição é FAIXA_NUMERICA, TAMANHO_TEXTO, OPCOES_PERMITIDAS, OPCOES_DAS_RESPOSTAS, MUNICIPIOS_DA_UF ou FAIXA_DE_DATA."),
        ClausulaVazia("itens[1].regras.impedimento.quando"),
        ("itens[2].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida, ObrigatoriedadeIncoerente),
        ("itens[2].regras.impedimento.quando", ItemFormularioErrorCodes.ImpedimentoSemCondicao, "O impedimento tem a condição sobre a resposta do campo."),
    ];

    private static TermoExigidoInput[] TermosRecusados() =>
    [
        ElementoNulo<TermoExigidoInput>(),
        EntradaDoTermoExigidoDeTeste.Exigindo("TERMO_USO", 1).DaVersao(TermoId, VersaoId).ExibidoQuando(PredicadoSemCondicao)
            .ComObrigatoriedade("QUANDO", PredicadoSemCondicao).Construir(),
        EntradaDoTermoExigidoDeTeste.Exigindo("TERMO_USO", 2).DaVersao(TermoId, VersaoId).ComObrigatoriedade("TALVEZ").Construir(),
    ];

    private static Recusa[] RecusasDosTermos() =>
    [
        Malformada("termos[0]", EntradaNula),
        ClausulaVazia("termos[1].regras.exibicao"),
        ClausulaVazia("termos[1].regras.obrigatoriedade.predicado"),
        ("termos[2].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida, ObrigatoriedadeIncoerente),
    ];

    private static GrupoColetadoInput[] GruposRecusados() =>
    [
        ElementoNulo<GrupoColetadoInput>(),
        GrupoValido() with
        {
            Regras = new ExibicaoEObrigatoriedadeInput(PredicadoSemCondicao, new ObrigatoriedadeInput("QUANDO", PredicadoSemCondicao)),
            Subitens = [ElementoNulo<FatoColetadoInput>(), Item("MAIOR_IDADE", 1).ExibidoQuando(PredicadoSemCondicao).Construir()],
        },
        GrupoValido() with { Regras = new ExibicaoEObrigatoriedadeInput(null, new ObrigatoriedadeInput("TALVEZ")) },
    ];

    private static Recusa[] RecusasDosGrupos() =>
    [
        Malformada("grupos[0]", EntradaNula),
        ClausulaVazia("grupos[1].regras.exibicao"),
        ClausulaVazia("grupos[1].regras.obrigatoriedade.predicado"),
        Malformada("grupos[1].subitens[0]", EntradaNula),
        ClausulaVazia("grupos[1].subitens[1].regras.exibicao"),
        ("grupos[2].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida, ObrigatoriedadeIncoerente),
    ];

    private static DocumentoExigidoDoModeloInput[] DocumentosRecusados() =>
    [
        ElementoNulo<DocumentoExigidoDoModeloInput>(),
        new(string.Empty, " ", PredicadoSemCondicao, new IndeferimentoDoDocumentoDoModeloInput(true, null, [ElementoNulo<BaseLegalDoModeloInput>()], false), PorOpcao: new ExigenciaPorOpcaoInput(string.Empty, [" "])),
    ];

    private static Recusa[] RecusasDosDocumentos() =>
    [
        Malformada("conjuntoDeDocumentos.documentosExigidos[0]", EntradaNula),
        ClausulaVazia("conjuntoDeDocumentos.documentosExigidos[1].quando"),
        Malformada("conjuntoDeDocumentos.documentosExigidos[1].indeferimento.basesLegais", EntradaNula),
        Malformada("conjuntoDeDocumentos.documentosExigidos[1].faseCodigo", "O documento exigido do modelo precisa informar a fase."),
        Malformada("conjuntoDeDocumentos.documentosExigidos[1].tipoDocumentoCodigo", "O documento exigido do modelo precisa informar o tipo de documento."),
        Malformada("conjuntoDeDocumentos.documentosExigidos[1].porOpcao.fatoCodigo", "A exigência por opção precisa informar o fato cujas opções a multiplicam."),
        Malformada("conjuntoDeDocumentos.documentosExigidos[1].porOpcao.exceto", "A opção excetuada precisa ser um código."),
    ];

    private static PaginaDoFormularioInput PaginaValida() =>
        EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").ComDescricao("Descrição").ComAviso("Aviso").ExibidaQuando(Quando("CERTIFICADO")).Construir();

    private static PaginaDoModelo PaginaEsperada() =>
        new("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", "Descrição", "Aviso", Predicado("CERTIFICADO"));

    private static FatoColetadoInput ItemValido() =>
        EntradaDoFatoColetadoDeTeste.Coletando("CPF_RESPONSAVEL")
            .ComRotulo("CPF do responsável").DoTipo("TEXTO").ComAjuda("Ajuda").PedindoConfirmacao().NaSecao("DADOS")
            .ExibidoQuando(Quando("CERTIFICADO"))
            .ComObrigatoriedade("QUANDO", Quando("MAIOR_IDADE"))
            .ComRestricoes(new RestricaoValorInput("TAMANHO_TEXTO", 11, 11))
            .ComImpedimento(new ImpedimentoInput(Quando("SEM_RENDA"), "Mensagem"))
            .Construir();

    private static ItemDoModelo ItemEsperado() =>
        new(
            "CPF_RESPONSAVEL",
            0,
            "DADOS",
            new ApresentacaoDoCampo("CPF do responsável", TipoDeCampo.Texto, "Ajuda", pedirConfirmacao: true),
            "CPF",
            new RegrasDoCampo(
                Predicado("CERTIFICADO"),
                Obrigatoriedade.Quando(Predicado("MAIOR_IDADE")),
                [RestricoesDeValor.Tamanho(11, 11).Value!],
                new Impedimento(Predicado("SEM_RENDA"), "Mensagem")));

    private static TermoExigidoInput TermoValido() =>
        EntradaDoTermoExigidoDeTeste.Exigindo("TERMO_USO").DaVersao(TermoId, VersaoId).ExibidoQuando(Quando("CERTIFICADO")).ComObrigatoriedade("NUNCA").Construir();

    private static TermoDoModelo TermoEsperado() =>
        new("TERMO_USO", 0, TermoId, VersaoId, new ExibicaoEObrigatoriedade(Predicado("CERTIFICADO"), Obrigatoriedade.Nunca));

    private static GrupoColetadoInput GrupoValido() =>
        EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO", 1).ComRotulo("Composição").NaSecao("DADOS").ComOcorrencias(1, 5).IncluindoOCandidato()
            .ExibidoQuando(Quando("CERTIFICADO")).ComObrigatoriedade("SEMPRE").ComCampos(Item("MAIOR_IDADE", 0).Construir()).Construir();

    private static GrupoDoModelo GrupoEsperado() =>
        new("COMPOSICAO", 1, "DADOS", "Composição", new OcorrenciasDoGrupo(1, 5, true),
            new ExibicaoEObrigatoriedade(Predicado("CERTIFICADO"), Obrigatoriedade.Sempre),
            [new ItemDoModelo(
                "MAIOR_IDADE", 0, null, new ApresentacaoDoCampo("MAIOR_IDADE", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []))]);

    private static DocumentoExigidoDoModeloInput DocumentoValido() =>
        new("INSCRICAO", "LAUDO_MEDICO", Quando("CERTIFICADO"),
            new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", [new BaseLegalDoModeloInput("Lei 12.711/2012", "FEDERAL", "RESOLVIDO", null)], false),
            PorOpcao: new ExigenciaPorOpcaoInput("CONDICAO", ["OUTRA", "TERCEIRA"]));

    private static DocumentoExigidoDoModelo DocumentoEsperado() =>
        new("INSCRICAO", "LAUDO_MEDICO", Predicado("CERTIFICADO"),
            new IndeferimentoDoDocumentoDoModelo(true, "RETIRA_O_ACRESCIMO", [new BaseLegalDoModelo("Lei 12.711/2012", "FEDERAL", "RESOLVIDO", null)]),
            RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos,
            new ExigenciaPorOpcao("CONDICAO", ["OUTRA", "TERCEIRA"]));

    private static EntradaDoFatoColetadoDeTeste Item(string fato, int ordem) => EntradaDoFatoColetadoDeTeste.Coletando(fato, ordem);

    private static Condicoes Quando(string fato) =>
        [[new CondicaoPrecondicaoInput(fato, Operador.Igual.Codigo(), JsonSerializer.SerializeToElement(true))]];

    private static PredicadoDnf Predicado(string fato) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

    private static Recusa ClausulaVazia(string campo) => ($"{campo}[0]", "ClausulaDnf.ClausulaVazia", ClausulaSemCondicao);

    private static Recusa Malformada(string campo, string mensagem) => (campo, ModeloFormularioErrorCodes.EntradaMalformada, mensagem);

    /// <summary>O elemento nulo que o JSON pode trazer na lista, embora o tipo do contrato não o admita.</summary>
    private static T ElementoNulo<T>()
        where T : class => null!;
}
