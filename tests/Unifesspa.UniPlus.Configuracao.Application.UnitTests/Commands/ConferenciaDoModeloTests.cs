namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Condicoes = System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Regras.Entradas.CondicaoPrecondicaoInput>>;
using Recusa = (string? Campo, string Codigo, string Mensagem);

/// <summary>
/// A conferência do conteúdo do modelo contra o catálogo: acumula as recusas na ordem das partes
/// (itens, grupos, documentos, etapas, termos, pressupostos, vínculos novos) e aponta cada uma pela
/// posição da parte na entrada.
/// </summary>
public sealed class ConferenciaDoModeloTests
{
    private static readonly Guid TermoDesconhecido = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid TermoJaVinculado = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid VersaoJaVinculada = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private readonly CatalogoDoModelo _catalogo = CatalogoDoModelo.De(
    [
        Declarado("CERTIFICADO"),
        Declarado("MAIOR_IDADE", escopo: EscopoFato.MembroGrupo),
        Declarado("DESATIVADO", desativado: true),
        Declarado("ANTIGO", desativado: true),
        Declarado("PENDENTE", aprovado: false),
        Categorico("CONDICAO"),
        Categorico("CONDICAO_DO_MEMBRO", EscopoFato.MembroGrupo),
    ]);

    private readonly VinculosDoModelo _existentes = new(
        VinculosDeFatos.De(["ANTIGO"], []), new HashSet<(Guid TermoId, Guid VersaoId)> { (TermoJaVinculado, VersaoJaVinculada) });

    [Fact(DisplayName = "O conteúdo que recusa em toda parte devolve as recusas na ordem da conferência, cada uma na posição da parte na entrada")]
    public void Conferir_RecusaEmTodasAsPartes_ListaAsRecusasNaOrdemDaConferencia()
    {
        List<FieldError> recusas = ConferirEntradaQueRecusaEmTodasAsPartes();

        recusas.Select(static r => (r.Field, r.Error.Code, r.Error.Message)).Should().Equal(RecusasEsperadas());
    }

    [Fact(DisplayName = "Só a recusa de vínculo novo vem sem campo; a de item, de campo de grupo e de termo sempre aponta o campo dentro da parte")]
    public void Conferir_RecusaDeItemGrupoETermo_ApontaSempreOCampoDaParte()
    {
        List<FieldError> recusas = ConferirEntradaQueRecusaEmTodasAsPartes();

        recusas.Where(static r => r.Field is null).Select(static r => r.Error.Code).Should().Equal(RecusasDosVinculos().Select(static r => r.Codigo));
        recusas.Where(static r => r.Field is not null).Should().OnlyContain(static r => r.Field!.Contains('.') || r.Field.StartsWith("pressupostos[", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O campo de grupo com restrições e impedimento recusados devolve as restrições antes do impedimento")]
    public void Conferir_SubitemDeGrupoComRestricoesEImpedimentoRecusados_ListaAsRestricoesAntesDoImpedimento()
    {
        List<FieldError> recusas = Conferir(EntradaComSubitemQueRecusaRestricoesEImpedimento());

        recusas.Select(static r => (r.Field, r.Error.Code, r.Error.Message)).Should().Equal(RecusasDoSubitem());
    }

    private List<FieldError> ConferirEntradaQueRecusaEmTodasAsPartes() => Conferir(EntradaQueRecusaEmTodasAsPartes());

    private List<FieldError> Conferir(ConteudoDoModeloInput entrada) => ConferenciaDoModelo.Conferir(
        ConteudoLido.Ler(entrada, _catalogo.Formatos), _catalogo, new Dictionary<Guid, VersaoTermoConsentimentoView>(), _existentes);

    private static ConteudoDoModeloInput EntradaComSubitemQueRecusaRestricoesEImpedimento() => new(
        "Habilitação",
        [new("DADOS", 0, "SECAO", null, "Dados", null, null), new("REVISAO", 1, "BLOCO", "REVISAO_E_ACEITE", "Revisão", null, null)],
        [],
        [],
        [],
        [new("COMPOSICAO", 1, "Composição", null, 1, 5, null, "SEMPRE", null, [SubitemDeSelecaoComRestricoesEImpedimentoQueRecusam()])]);

    private static FatoColetadoInput SubitemDeSelecaoComRestricoesEImpedimentoQueRecusam() =>
        EntradaDoFatoColetadoDeTeste.Coletando("CONDICAO_DO_MEMBRO").DoTipo("SELECAO_UNICA")
            .ComImpedimento(new ImpedimentoInput(CitandoFatoInexistente, "Mensagem"))
            .ComRestricoes(
                new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new(CitandoFatoInexistente, ["A"])]),
                new RestricaoValorInput("OPCOES_DAS_RESPOSTAS", Fatos: ["NAO_EXISTE"]))
            .Construir();

    private static Recusa[] RecusasDoSubitem() =>
    [
        FatoInexistenteNoPredicado("grupos[0].subitens[0].regras.restricoes[0].entradas[0].quando"),
        ("grupos[0].subitens[0].regras.restricoes[1].fatos", ItemFormularioErrorCodes.OpcoesDeOutroDominio,
            "As opções formadas pelas respostas vêm de campos de seleção cujas opções são todas opções do campo."),
        FatoInexistenteNoPredicado("grupos[0].subitens[0].regras.impedimento.quando"),
    ];

    private static ConteudoDoModeloInput EntradaQueRecusaEmTodasAsPartes() => new(
        "Habilitação",
        [new("DADOS", 0, "SECAO", null, "Dados", null, null, CitandoFatoInexistente), new("REVISAO", 1, "BLOCO", "REVISAO_E_ACEITE", "Revisão", null, null)],
        ItensQueRecusam(),
        [
            new("TERMO_USO", 0, TermoDesconhecido, VersaoJaVinculada, CitandoFatoInexistente, "QUANDO", CitandoFatoInexistente),
            new("TERMO_USO", 1, TermoJaVinculado, VersaoJaVinculada, CitandoFatoInexistente, "SEMPRE", null),
        ],
        ["NAO_EXISTE", string.Empty, " CERTIFICADO ", "MAIOR_IDADE"],
        [GrupoQueRecusa()],
        DocumentosQueRecusam());

    private static Condicoes CitandoFatoInexistente => Citando("NAO_EXISTE");

    private static FatoColetadoInput[] ItensQueRecusam() =>
    [
        EntradaDoFatoColetadoDeTeste.Coletando("CERTIFICADO").ExibidoQuando([[]]).Construir(),
        Item("NAO_EXISTE", 1),
        ItemDeTipoIncoerenteComRegrasQueRecusam(),
        ItemDeSelecaoComImpedimentoERestricoesQueRecusam(),
        Item("DESATIVADO", 4),
        Item("PENDENTE", 5),
        Item("ANTIGO", 6),
    ];

    private static FatoColetadoInput ItemDeTipoIncoerenteComRegrasQueRecusam() =>
        EntradaDoFatoColetadoDeTeste.Coletando("CERTIFICADO", 2).DoTipo("NUMERO")
            .ExibidoQuando(CitandoFatoInexistente)
            .ComObrigatoriedade("QUANDO", CitandoFatoInexistente)
            .ComImpedimento(new ImpedimentoInput(CitandoFatoInexistente, "Mensagem"))
            .Construir();

    private static FatoColetadoInput ItemDeSelecaoComImpedimentoERestricoesQueRecusam() =>
        EntradaDoFatoColetadoDeTeste.Coletando("CONDICAO", 3).DoTipo("SELECAO_UNICA")
            .ComImpedimento(new ImpedimentoInput(CitandoFatoInexistente, "Mensagem"))
            .ComRestricoes(
                new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new(CitandoFatoInexistente, ["A"]), new(null, ["B", "Z"])]),
                new RestricaoValorInput("OPCOES_DAS_RESPOSTAS", Fatos: ["NAO_EXISTE"]))
            .Construir();

    private static GrupoColetadoInput GrupoQueRecusa() =>
        new("COMPOSICAO", 1, "Composição", null, 1, 5, CitandoFatoInexistente, "QUANDO", CitandoFatoInexistente,
            [Item("CERTIFICADO", 0), Item("MAIOR_IDADE", 1, "NUMERO")]);

    private static DocumentoExigidoDoModeloInput[] DocumentosQueRecusam() =>
    [
        new(string.Empty, "LAUDO_MEDICO", true, null, "REMOVE_VANTAGEM", []),
        new("INSCRICAO", "LAUDO_MEDICO", true, CitandoFatoInexistente, "REMOVE_VANTAGEM", [], new ExigenciaPorOpcaoInput("CERTIFICADO", [])),
        new("INSCRICAO", "LAUDO_MEDICO", true, null, "REMOVE_VANTAGEM", [], new ExigenciaPorOpcaoInput(" NAO_EXISTE ", [])),
    ];

    private static Recusa[] RecusasEsperadas() =>
    [
        .. RecusasDosItens(), .. RecusasDosGrupos(), .. RecusasDosDocumentos(), .. RecusasDasEtapas(), .. RecusasDosTermos(), .. RecusasDosPressupostos(), .. RecusasDosVinculos(),
    ];

    private static Recusa[] RecusasDosItens() =>
    [
        ("itens[1].fatoCodigo", ItemFormularioErrorCodes.FatoDesconhecido, "O fato 'NAO_EXISTE' não pertence ao catálogo de fatos do candidato."),
        ("itens[2].apresentacao.tipoRenderizacao", ItemFormularioErrorCodes.TipoRenderizacaoIncoerenteComDominio,
            "O tipo de campo 'NUMERO' não é coerente com o domínio 'BOOLEANO', a cardinalidade 'ESCALAR' e a fonte de valores 'nenhuma' do fato 'CERTIFICADO'."),
        FatoInexistenteNoPredicado("itens[2].regras.exibicao"),
        FatoInexistenteNoPredicado("itens[2].regras.obrigatoriedade.predicado"),
        FatoInexistenteNoPredicado("itens[2].regras.impedimento.quando"),
        FatoInexistenteNoPredicado("itens[3].regras.restricoes[0].entradas[0].quando"),
        ("itens[3].regras.restricoes[0].entradas[1].valores", PredicadoDnfErrorCodes.ValorForaDoDominio,
            "O valor 'Z' da condição EM sobre 'CONDICAO' não pertence ao domínio declarado."),
        ("itens[3].regras.restricoes[1].fatos", ItemFormularioErrorCodes.OpcoesDeOutroDominio,
            "As opções formadas pelas respostas vêm de campos de seleção cujas opções são todas opções do campo."),
        FatoInexistenteNoPredicado("itens[3].regras.impedimento.quando"),
    ];

    private static Recusa[] RecusasDosGrupos() =>
    [
        FatoInexistenteNoPredicado("grupos[0].exibicao"),
        FatoInexistenteNoPredicado("grupos[0].predicadoObrigatoriedade"),
        ("grupos[0].subitens[0].fatoCodigo", ItemFormularioErrorCodes.FatoNaoColetavel,
            "O fato 'CERTIFICADO' não é coletável num campo do grupo: só o fato declarado de membro de grupo é respondido em cada ocorrência — fatos do candidato, derivados e calculados não."),
        ("grupos[0].subitens[1].apresentacao.tipoRenderizacao", ItemFormularioErrorCodes.TipoRenderizacaoIncoerenteComDominio,
            "O tipo de campo 'NUMERO' não é coerente com o domínio 'BOOLEANO', a cardinalidade 'ESCALAR' e a fonte de valores 'nenhuma' do fato 'MAIOR_IDADE'."),
    ];

    private static Recusa[] RecusasDosDocumentos() =>
    [
        FatoInexistenteNoPredicado("documentosExigidos[1].quando"),
        ("documentosExigidos[1].porOpcao.fatoCodigo", ModeloFormularioErrorCodes.ExigenciaPorOpcaoInvalida,
            "O documento só pode ser exigido uma vez por opção de fato cujas opções o processo declara. Informe um fato que atenda a essa condição."),
        ("documentosExigidos[2].porOpcao.fatoCodigo", ItemFormularioErrorCodes.FatoDesconhecido, "O fato informado não pertence ao catálogo de fatos do candidato."),
    ];

    private static Recusa[] RecusasDasEtapas() => [FatoInexistenteNoPredicado("etapas[0].exibicao")];

    private static Recusa[] RecusasDosTermos() =>
    [
        FatoInexistenteNoPredicado("termos[0].exibicao"),
        FatoInexistenteNoPredicado("termos[0].predicadoObrigatoriedade"),
        ("termos[0].versaoId", ModeloFormularioErrorCodes.TermoVersaoNaoEncontrada, "A versão informada não existe no catálogo de termos, ou pertence a outro termo."),
        FatoInexistenteNoPredicado("termos[1].exibicao"),
    ];

    private static Recusa[] RecusasDosPressupostos() =>
    [
        ("pressupostos[0]", ItemFormularioErrorCodes.FatoDesconhecido, "O fato 'NAO_EXISTE' não pertence ao catálogo de fatos do candidato."),
        ("pressupostos[3]", ItemFormularioErrorCodes.FatoNaoColetavel,
            "O fato 'MAIOR_IDADE' não é coletável num campo: só o fato declarado do próprio candidato é respondido no formulário — derivados, calculados e fatos de membro de grupo não."),
    ];

    private static Recusa[] RecusasDosVinculos() =>
    [
        (null, VinculoCatalogoErrorCodes.FatoDesativado, "O fato 'DESATIVADO' está desativado no catálogo e não aceita vínculo novo."),
        (null, VinculoCatalogoErrorCodes.FatoComProtecaoPendente, "O fato 'PENDENTE' aguarda a aprovação da proteção de dados e não aceita vínculo novo."),
        (null, VinculoCatalogoErrorCodes.ValorDesativado, "O valor 'B' do fato 'CONDICAO' está desativado no catálogo e não aceita vínculo novo."),
    ];

    private static Recusa FatoInexistenteNoPredicado(string campo) =>
        (campo, "PredicadoDnf.FatoDesconhecido", "O fato 'NAO_EXISTE' não pertence ao vocabulário fechado.");

    private static FatoColetadoInput Item(string fato, int ordem, string tipo = "BOOLEANO") =>
        EntradaDoFatoColetadoDeTeste.Coletando(fato, ordem).DoTipo(tipo).Construir();

    private static Condicoes Citando(string fato) =>
        [[new CondicaoPrecondicaoInput(fato, Operador.Igual.Codigo(), JsonSerializer.SerializeToElement(true))]];

    private static FatoCandidato Declarado(string codigo, EscopoFato escopo = EscopoFato.Candidato, bool desativado = false, bool aprovado = true)
    {
        FatoCandidato fato = CriacaoDoFato.CriarDoAdministrador(
            codigo, codigo, "Descrição do fato.", DominioFato.Booleano, CardinalidadeFato.Escalar, null, null, "INSCRICAO", escopo,
            ClassificacaoDeProtecao.Pessoal, "Verificação dos requisitos do processo seletivo.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        return Preparado(fato, desativado, aprovado);
    }

    private static FatoCandidato Categorico(string codigo, EscopoFato escopo = EscopoFato.Candidato)
    {
        FatoCandidato fato = CriacaoDoFato.CriarDoAdministrador(
            codigo, codigo, "Descrição do fato.", DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global, null, "INSCRICAO", escopo,
            ClassificacaoDeProtecao.Pessoal, "Verificação dos requisitos do processo seletivo.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        fato.AdicionarValorDominio("A", "Opção A.", 0, ativo: true).IsSuccess.Should().BeTrue();
        fato.AdicionarValorDominio("B", "Opção B.", 1, ativo: false).IsSuccess.Should().BeTrue();
        return Preparado(fato, desativado: false, aprovado: true);
    }

    private static FatoCandidato Preparado(FatoCandidato fato, bool desativado, bool aprovado)
    {
        if (aprovado)
        {
            fato.SolicitarAprovacaoDaProtecao("isabella.figueiredo", DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();
            fato.AprovarProtecao("caroline.lima", DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();
        }

        if (desativado)
        {
            fato.Desativar().IsSuccess.Should().BeTrue();
        }

        return fato;
    }
}
