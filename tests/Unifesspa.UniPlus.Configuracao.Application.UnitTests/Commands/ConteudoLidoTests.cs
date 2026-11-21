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
/// (etapas, itens, termos, grupos, documentos) e guarda a posição de cada parte lida na entrada.
/// </summary>
public sealed class ConteudoLidoTests
{
    private const string ClausulaSemCondicao = "Uma cláusula deve ter ao menos uma condição.";
    private const string EntradaNula = "A etapa, o item, o grupo ou o termo veio nulo.";
    private const string ObrigatoriedadeIncoerente = "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.";

    private static readonly Condicoes PredicadoSemCondicao = [[]];
    private static readonly Dictionary<string, string> Formatos = new(StringComparer.Ordinal) { ["CPF_RESPONSAVEL"] = "CPF" };
    private static readonly Guid TermoId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid VersaoId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    [Fact(DisplayName = "A entrada que recusa em toda parte devolve as recusas na ordem da leitura, com a posição de cada uma na entrada")]
    public void Ler_RecusaEmTodasAsPartes_ListaAsRecusasNaOrdemDaLeitura()
    {
        ConteudoDoModeloInput entrada = new(
            "Habilitação", EtapasRecusadas(), ItensRecusados(), TermosRecusados(), ["CERTIFICADO", ElementoNulo<string>()], GruposRecusados(), DocumentosRecusados());

        ConteudoLido lido = ConteudoLido.Ler(entrada, Formatos);

        lido.Erros.Select(static e => (e.Field!, e.Error.Code, e.Error.Message)).Should().Equal(RecusasEsperadas());
        lido.ParaConteudo().Should().BeEquivalentTo(new ConteudoDoModelo("Habilitação", [], [], [], ["CERTIFICADO", string.Empty], [], []), PorMembros);
    }

    [Fact(DisplayName = "A entrada válida em toda parte vira o conteúdo do modelo, sem recusa, com o formato do fato vindo do catálogo")]
    public void Ler_EntradaValidaEmTodasAsPartes_ProduzOConteudoEsperado()
    {
        ConteudoDoModeloInput entrada = new(
            "Habilitação", [EtapaValida()], [ItemValido()], [TermoValido()], ["CERTIFICADO"], [GrupoValido()], [DocumentoValido()]);

        ConteudoLido lido = ConteudoLido.Ler(entrada, Formatos);

        lido.Erros.Should().BeEmpty();
        lido.ParaConteudo().Should().BeEquivalentTo(
            new ConteudoDoModelo("Habilitação", [EtapaEsperada()], [ItemEsperado()], [TermoEsperado()], ["CERTIFICADO"], [GrupoEsperado()], [DocumentoEsperado()]),
            PorMembros);
    }

    [Fact(DisplayName = "A parte lida guarda a posição dela na entrada; a nula e a recusada ficam de fora")]
    public void Ler_PartesLidasEEntreRecusadas_GuardamAPosicaoNaEntrada()
    {
        ConteudoLido lido = ConteudoLido.Ler(ComPartesRecusadasNoMeio(), Formatos);

        lido.Etapas.Select(static e => e.Indice).Should().Equal(0, 3);
        lido.Itens.Select(static i => i.Indice).Should().Equal(0, 2);
        lido.Termos.Select(static t => t.Indice).Should().Equal(1);
        lido.Grupos.Select(static g => g.Indice).Should().Equal(0, 2);
        lido.Documentos.Select(static d => d.Indice).Should().Equal(0, 3);
    }

    [Fact(DisplayName = "A entrada ausente ou com as listas ausentes é lida como conteúdo vazio, sem recusa")]
    public void Ler_EntradaOuListasAusentes_ConteudoVazioSemRecusa()
    {
        ConteudoLido semEntrada = ConteudoLido.Ler(null, Formatos);
        ConteudoLido semListas = ConteudoLido.Ler(new ConteudoDoModeloInput(null, null, null, null, null), Formatos);

        semEntrada.Erros.Should().BeEmpty();
        semListas.Erros.Should().BeEmpty();
        semEntrada.ParaConteudo().Should().BeEquivalentTo(new ConteudoDoModelo(null, [], [], [], [], [], []), PorMembros);
        semListas.ParaConteudo().Should().BeEquivalentTo(new ConteudoDoModelo(null, [], [], [], [], [], []), PorMembros);
    }

    [Fact(DisplayName = "O documento sem bases legais e com a exigência por opção sem exceções é lido sem recusa, com as duas listas vazias")]
    public void Ler_DocumentoSemBasesLegaisNemExcecoes_LeSemRecusa()
    {
        DocumentoExigidoDoModeloInput documento = DocumentoValido() with { BasesLegais = null, PorOpcao = new ExigenciaPorOpcaoInput("CONDICAO", null) };

        ConteudoLido lido = ConteudoLido.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, [documento]), Formatos);

        lido.Erros.Should().BeEmpty();
        lido.Documentos.Single().Documento.BasesLegais.Should().BeEmpty();
        lido.Documentos.Single().Documento.PorOpcao!.Exceto.Should().BeEmpty();
    }

    private static ConteudoDoModeloInput ComPartesRecusadasNoMeio() => new(
        "Habilitação",
        [EtapaValida(), ElementoNulo<EtapaFormularioInput>(), EtapaValida() with { Exibicao = PredicadoSemCondicao }, EtapaValida()],
        [ItemValido(), ElementoNulo<FatoColetadoInput>(), ItemValido()],
        [ElementoNulo<TermoExigidoInput>(), TermoValido()],
        [],
        [GrupoValido(), GrupoValido() with { Regras = new ExibicaoEObrigatoriedadeInput(null, new ObrigatoriedadeInput("TALVEZ")) }, GrupoValido()],
        [DocumentoValido(), ElementoNulo<DocumentoExigidoDoModeloInput>(), DocumentoValido() with { FaseCodigo = string.Empty }, DocumentoValido()]);

    private static EquivalencyOptions<ConteudoDoModelo> PorMembros(EquivalencyOptions<ConteudoDoModelo> opcoes) => opcoes
        .ComparingRecordsByMembers()
        .Using<JsonElement>(static contexto => JsonElement.DeepEquals(contexto.Subject, contexto.Expectation).Should().BeTrue())
        .WhenTypeIs<JsonElement>();

    private static Recusa[] RecusasEsperadas() =>
        [.. RecusasDasEtapas(), .. RecusasDosItens(), .. RecusasDosTermos(), .. RecusasDosGrupos(), .. RecusasDosDocumentos()];

    private static EtapaFormularioInput[] EtapasRecusadas() =>
        [ElementoNulo<EtapaFormularioInput>(), EntradaDaEtapaFormularioDeTeste.Secao("DADOS", 1).ComTitulo("Dados").ExibidaQuando(PredicadoSemCondicao).Construir()];

    private static Recusa[] RecusasDasEtapas() =>
        [Malformada("etapas[0]", EntradaNula), ClausulaVazia("etapas[1].exibicao")];

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
        new(string.Empty, " ", true, PredicadoSemCondicao, string.Empty, [ElementoNulo<BaseLegalDoModeloInput>()], new ExigenciaPorOpcaoInput(string.Empty, [" "])),
    ];

    private static Recusa[] RecusasDosDocumentos() =>
    [
        Malformada("documentosExigidos[0]", EntradaNula),
        ClausulaVazia("documentosExigidos[1].quando"),
        Malformada("documentosExigidos[1].basesLegais", EntradaNula),
        Malformada("documentosExigidos[1].faseCodigo", "O documento exigido do modelo precisa informar a fase."),
        Malformada("documentosExigidos[1].tipoDocumentoCodigo", "O documento exigido do modelo precisa informar o tipo de documento."),
        Malformada("documentosExigidos[1].consequenciaIndeferimento", "O documento exigido do modelo precisa informar a consequência do indeferimento."),
        Malformada("documentosExigidos[1].porOpcao.fatoCodigo", "A exigência por opção precisa informar o fato cujas opções a multiplicam."),
        Malformada("documentosExigidos[1].porOpcao.exceto", "A opção excetuada precisa ser um código."),
    ];

    private static EtapaFormularioInput EtapaValida() =>
        EntradaDaEtapaFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").ComDescricao("Descrição").ComAviso("Aviso").ExibidaQuando(Quando("CERTIFICADO")).Construir();

    private static EtapaDoModelo EtapaEsperada() =>
        new("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", "Descrição", "Aviso", Predicado("CERTIFICADO"));

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
            new ApresentacaoDoCampo("CPF do responsável", TipoRenderizacao.Texto, "Ajuda", pedirConfirmacao: true),
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
                "MAIOR_IDADE", 0, null, new ApresentacaoDoCampo("MAIOR_IDADE", TipoRenderizacao.Booleano), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []))]);

    private static DocumentoExigidoDoModeloInput DocumentoValido() =>
        new("INSCRICAO", "LAUDO_MEDICO", true, Quando("CERTIFICADO"), "REMOVE_VANTAGEM",
            [new BaseLegalDoModeloInput("Lei 12.711/2012", "TODOS", "VIGENTE", null)], new ExigenciaPorOpcaoInput("CONDICAO", ["OUTRA", "TERCEIRA"]));

    private static DocumentoExigidoDoModelo DocumentoEsperado() =>
        new("INSCRICAO", "LAUDO_MEDICO", true, Predicado("CERTIFICADO"), "REMOVE_VANTAGEM",
            [new BaseLegalDoDocumentoDoModelo("Lei 12.711/2012", "TODOS", "VIGENTE", null)], new ExigenciaPorOpcao("CONDICAO", ["OUTRA", "TERCEIRA"]));

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
