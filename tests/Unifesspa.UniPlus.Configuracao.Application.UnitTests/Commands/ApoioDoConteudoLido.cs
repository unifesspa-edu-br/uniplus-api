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

/// <summary>O apoio comum aos testes da leitura do conteúdo do modelo: as entradas válidas, as esperadas e as recusas, por composição.</summary>
internal static class ApoioDoConteudoLido
{
    internal const string ClausulaSemCondicao = "Uma cláusula deve ter ao menos uma condição.";
    internal const string EntradaNula = "A página, o item, o grupo ou o termo veio nulo.";
    internal const string ObrigatoriedadeIncoerente = "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.";

    internal static readonly Condicoes PredicadoSemCondicao = [[]];
    internal static readonly Dictionary<string, string> Formatos = new(StringComparer.Ordinal) { ["CPF_RESPONSAVEL"] = "CPF" };
    internal static readonly Guid TermoId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    internal static readonly Guid VersaoId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    internal static IReadOnlyList<string> CamposRecusadosDoItemCom(RestricaoValorInput restricao)
    {
        LeituraDoConteudo leitura = new(Formatos);
        leitura.Ler(new ConteudoDoModeloInput(null, null, [Item("MAIOR_IDADE", 0).ComRestricoes(restricao).Construir()], null, null));
        return [.. leitura.Recusas.Select(static e => e.Field!)];
    }

    internal static ConteudoDoModeloInput ComPartesRecusadasNoMeio() => new(
        "Habilitação",
        [PaginaValida(), ElementoNulo<PaginaDoFormularioInput>(), PaginaValida() with { Exibicao = PredicadoSemCondicao }, PaginaValida()],
        [ItemValido(), ElementoNulo<FatoColetadoInput>(), ItemValido()],
        [ElementoNulo<TermoExigidoInput>(), TermoValido()],
        [],
        [GrupoValido(), GrupoValido() with { Regras = new ExibicaoEObrigatoriedadeInput(null, new ObrigatoriedadeInput("TALVEZ")) }, GrupoValido()],
        ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(
            DocumentoValido(), ElementoNulo<DocumentoExigidoDoModeloInput>(), DocumentoValido() with { FaseCodigo = string.Empty }, DocumentoValido()));

    internal static EquivalencyOptions<ConteudoDoModelo> PorMembros(EquivalencyOptions<ConteudoDoModelo> opcoes) => opcoes
        .ComparingRecordsByMembers()
        .Using<JsonElement>(static contexto => JsonElement.DeepEquals(contexto.Subject, contexto.Expectation).Should().BeTrue())
        .WhenTypeIs<JsonElement>();

    internal static Recusa[] RecusasEsperadas() =>
        [.. RecusasDasPaginas(), .. RecusasDosItens(), .. RecusasDosTermos(), .. RecusasDosGrupos(), .. RecusasDosDocumentos(), .. RecusasDeTermoRepetido()];

    internal static Recusa[] RecusasDeTermoRepetido() =>
        [("termos[2].codigo", TermoFormularioErrorCodes.CodigoDuplicado, "O código aparece em mais de um termo do formulário.")];

    internal static PaginaDoFormularioInput[] PaginasRecusadas() =>
        [ElementoNulo<PaginaDoFormularioInput>(), EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 1).ComTitulo("Dados").ExibidaQuando(PredicadoSemCondicao).Construir()];

    internal static Recusa[] RecusasDasPaginas() =>
        [Malformada("paginas[0]", EntradaNula), ClausulaVazia("paginas[1].exibicao")];

    internal static FatoColetadoInput[] ItensRecusados() =>
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

    internal static Recusa[] RecusasDosItens() =>
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

    internal static TermoExigidoInput[] TermosRecusados() =>
    [
        ElementoNulo<TermoExigidoInput>(),
        EntradaDoTermoExigidoDeTeste.Exigindo("TERMO_USO", 1).DaVersao(TermoId, VersaoId).ExibidoQuando(PredicadoSemCondicao)
            .ComObrigatoriedade("QUANDO", PredicadoSemCondicao).Construir(),
        EntradaDoTermoExigidoDeTeste.Exigindo("TERMO_USO", 2).DaVersao(TermoId, VersaoId).ComObrigatoriedade("TALVEZ").Construir(),
    ];

    internal static Recusa[] RecusasDosTermos() =>
    [
        Malformada("termos[0]", EntradaNula),
        ClausulaVazia("termos[1].regras.exibicao"),
        ClausulaVazia("termos[1].regras.obrigatoriedade.predicado"),
        ("termos[2].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida, ObrigatoriedadeIncoerente),
    ];

    internal static GrupoColetadoInput[] GruposRecusados() =>
    [
        ElementoNulo<GrupoColetadoInput>(),
        GrupoValido() with
        {
            Regras = new ExibicaoEObrigatoriedadeInput(PredicadoSemCondicao, new ObrigatoriedadeInput("QUANDO", PredicadoSemCondicao)),
            Subitens = [ElementoNulo<FatoColetadoInput>(), Item("MAIOR_IDADE", 1).ExibidoQuando(PredicadoSemCondicao).Construir()],
        },
        GrupoValido() with { Regras = new ExibicaoEObrigatoriedadeInput(null, new ObrigatoriedadeInput("TALVEZ")) },
    ];

    internal static Recusa[] RecusasDosGrupos() =>
    [
        Malformada("grupos[0]", EntradaNula),
        ClausulaVazia("grupos[1].regras.exibicao"),
        ClausulaVazia("grupos[1].regras.obrigatoriedade.predicado"),
        ParentescoAusente("grupos[1]"),
        Malformada("grupos[1].subitens[0]", EntradaNula),
        ClausulaVazia("grupos[1].subitens[1].regras.exibicao"),
        ("grupos[2].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida, ObrigatoriedadeIncoerente),
        ParentescoAusente("grupos[2]"),
    ];

    internal static Recusa ParentescoAusente(string grupo) =>
        ($"{grupo}.subitens", GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto, "O grupo que inclui o candidato pede o parentesco (PARENTESCO) em cada ocorrência.");

    internal static DocumentoExigidoDoModeloInput[] DocumentosRecusados() =>
    [
        ElementoNulo<DocumentoExigidoDoModeloInput>(),
        new(string.Empty, " ", PredicadoSemCondicao, new IndeferimentoDoDocumentoDoModeloInput(true, null, [ElementoNulo<BaseLegalDoModeloInput>()], false), PorOpcao: new ExigenciaPorOpcaoInput(string.Empty, [" "])),
    ];

    internal static Recusa[] RecusasDosDocumentos() =>
    [
        Malformada("conjuntoDeDocumentos.documentosExigidos[0]", EntradaNula),
        ClausulaVazia("conjuntoDeDocumentos.documentosExigidos[1].quando"),
        Malformada("conjuntoDeDocumentos.documentosExigidos[1].indeferimento.basesLegais", EntradaNula),
        Malformada("conjuntoDeDocumentos.documentosExigidos[1].faseCodigo", "O documento exigido do modelo precisa informar a fase."),
        Malformada("conjuntoDeDocumentos.documentosExigidos[1].tipoDocumentoCodigo", "O documento exigido do modelo precisa informar o tipo de documento."),
        Malformada("conjuntoDeDocumentos.documentosExigidos[1].porOpcao.fatoCodigo", "A exigência por opção precisa informar o fato cujas opções a multiplicam."),
        Malformada("conjuntoDeDocumentos.documentosExigidos[1].porOpcao.exceto", "A opção excetuada precisa ser um código."),
    ];

    internal static PaginaDoFormularioInput PaginaValida() =>
        EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").ComDescricao("Descrição").ComAviso("Aviso").ExibidaQuando(Quando("CERTIFICADO")).Construir();

    internal static PaginaDoModelo PaginaEsperada() =>
        new("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", "Descrição", "Aviso", Predicado("CERTIFICADO"));

    internal static FatoColetadoInput ItemValido() =>
        EntradaDoFatoColetadoDeTeste.Coletando("CPF_RESPONSAVEL")
            .ComRotulo("CPF do responsável").DoTipo("TEXTO").ComAjuda("Ajuda").PedindoConfirmacao().NaSecao("DADOS")
            .ExibidoQuando(Quando("CERTIFICADO"))
            .ComObrigatoriedade("QUANDO", Quando("MAIOR_IDADE"))
            .ComRestricoes(new RestricaoValorInput("TAMANHO_TEXTO", 11, 11))
            .ComImpedimento(new ImpedimentoInput(Quando("SEM_RENDA"), "Mensagem"))
            .Construir();

    internal static ItemDoModelo ItemEsperado() =>
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

    internal static TermoExigidoInput TermoValido() =>
        EntradaDoTermoExigidoDeTeste.Exigindo("TERMO_USO").DaVersao(TermoId, VersaoId).ExibidoQuando(Quando("CERTIFICADO")).ComObrigatoriedade("NUNCA").Construir();

    internal static TermoDoModelo TermoEsperado() =>
        new("TERMO_USO", 0, TermoId, VersaoId, new ExibicaoEObrigatoriedade(Predicado("CERTIFICADO"), Obrigatoriedade.Nunca));

    internal static GrupoColetadoInput GrupoValido() =>
        EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO", 1).ComRotulo("Composição").NaSecao("DADOS").ComOcorrencias(1, 5).IncluindoOCandidato()
            .ExibidoQuando(Quando("CERTIFICADO")).ComObrigatoriedade("SEMPRE").ComCampos(Item("MAIOR_IDADE", 0).Construir()).Construir();

    internal static GrupoDoModelo GrupoEsperado() =>
        new("COMPOSICAO", 1, "DADOS", "Composição", new OcorrenciasDoGrupo(1, 5, true),
            new ExibicaoEObrigatoriedade(Predicado("CERTIFICADO"), Obrigatoriedade.Sempre),
            [new ItemDoModelo(
                "MAIOR_IDADE", 0, null, new ApresentacaoDoCampo("MAIOR_IDADE", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []))]);

    internal static DocumentoExigidoDoModeloInput DocumentoValido() =>
        new("INSCRICAO", "LAUDO_MEDICO", Quando("CERTIFICADO"),
            new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", [new BaseLegalDoModeloInput("Lei 12.711/2012", "FEDERAL", "RESOLVIDO", null)], false),
            PorOpcao: new ExigenciaPorOpcaoInput("CONDICAO", ["OUTRA", "TERCEIRA"]));

    internal static DocumentoExigidoDoModelo DocumentoEsperado() =>
        new("INSCRICAO", "LAUDO_MEDICO", Predicado("CERTIFICADO"),
            new IndeferimentoDoDocumentoDoModelo(true, "RETIRA_O_ACRESCIMO", [new BaseLegalDoModelo("Lei 12.711/2012", "FEDERAL", "RESOLVIDO", null)]),
            RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos,
            new ExigenciaPorOpcao("CONDICAO", ["OUTRA", "TERCEIRA"]));

    internal static EntradaDoFatoColetadoDeTeste Item(string fato, int ordem) => EntradaDoFatoColetadoDeTeste.Coletando(fato, ordem);

    internal static Condicoes Quando(string fato) =>
        [[new CondicaoPrecondicaoInput(fato, Operador.Igual.Codigo(), JsonSerializer.SerializeToElement(true))]];

    internal static PredicadoDnf Predicado(string fato) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

    internal static Recusa ClausulaVazia(string campo) => ($"{campo}[0]", "ClausulaDnf.ClausulaVazia", ClausulaSemCondicao);

    internal static Recusa Malformada(string campo, string mensagem) => (campo, ModeloFormularioErrorCodes.EntradaMalformada, mensagem);

    /// <summary>O elemento nulo que o JSON pode trazer na lista, embora o tipo do contrato não o admita.</summary>
    internal static T ElementoNulo<T>()
        where T : class => null!;
}
