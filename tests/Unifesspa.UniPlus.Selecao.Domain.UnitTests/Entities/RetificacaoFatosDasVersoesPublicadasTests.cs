namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Ao congelar a retificação, o formulário e as exigências de uma finalidade só usam, de outra finalidade, fato
/// que o formulário dela coletou em todas as versões publicadas, e a inscrição não deixa de coletar o
/// que já coletava (UNI-REQ-0144): o candidato pode ter preenchido cada finalidade em qualquer versão.
/// </summary>
/// <remarks>
/// O cenário publica duas versões: a inscrição coleta <c>TEM_RENDA</c> e o grupo <c>FAMILIA</c> nas
/// duas, e <see cref="Novo"/> só na segunda, que é a base da retificação; a habilitação, quando existe,
/// coleta <c>COMPROVANTE</c> nas duas. As escritas valem em rascunho; a recusa é a da conferência do congelamento.
/// </remarks>
public sealed class RetificacaoFatosDasVersoesPublicadasTests
{
    /// <summary>Coletado pela inscrição só na versão base.</summary>
    private const string Novo = "NOVO";

    private const string FatoAusente = "Retificacao.FatoAusenteDeVersaoPublicada";

    private static readonly Dictionary<string, string> SemCatalogo = new(StringComparer.Ordinal);

    private sealed record Cenario(
        ProcessoSeletivo Processo, FaseCronograma Inscricao, FaseCronograma Habilitacao, FaseCronograma Matricula, FatosDasVersoesPublicadas Publicadas)
    {
        public CadastrosDaConfiguracao Cadastros => CadastrosDeTeste.Com(fatosDasVersoesPublicadas: Publicadas);

        public DomainError? PrimeiraCausa() => CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(Processo, Cadastros);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-01 — a inscrição continua coletando o que já coletava, no mesmo papel
    // ══════════════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "O congelamento recusa a inscrição que deixa de coletar, no mesmo papel, fato que a versão base coletava")]
    [InlineData("item retirado")]
    [InlineData("item se torna campo de grupo")]
    [InlineData("grupo retirado")]
    public void Inscricao_DeixaDeColetarFatoDaBase_Recusa(string caso)
    {
        Cenario cenario = Publicado();
        ProcessoSeletivo processo = cenario.Processo;

        Result escrita = caso switch
        {
            "item retirado" => processo.DefinirItens([Item(Novo, 1)]),
            "item se torna campo de grupo" => processo.DefinirItens([Item(Novo, 1)], grupos: [Familia(2, "MEMBRO_RENDA", "TEM_RENDA")]),
            _ => processo.DefinirItens([Item("TEM_RENDA", 0), Item(Novo, 1)], grupos: []),
        };

        escrita.IsSuccess.Should().BeTrue(escrita.Error?.Message, "a escrita só atinge o rascunho");
        cenario.PrimeiraCausa()!.Code.Should().Be("Retificacao.FatoJaColetadoRemovido");
    }

    [Fact(DisplayName = "O congelamento aceita item acrescentado antes dos que a inscrição já coletava, deslocando a ordem deles")]
    public void Inscricao_ItemNovoAntesDosDaBase_Aceita()
    {
        Cenario cenario = Publicado();

        Result escrita = cenario.Processo.DefinirItens(
            [Item("ANTES", 0), Item("TEM_RENDA", 1), Item(Novo, 2)], grupos: [Familia(3, "MEMBRO_RENDA")]);

        escrita.IsSuccess.Should().BeTrue(escrita.Error?.Message);
        cenario.PrimeiraCausa().Should().BeNull();
    }

    [Fact(DisplayName = "Sem versão publicada, a inscrição não tem fato já coletado a preservar")]
    public void SemVersaoPublicada_NaoHaOQueConferir()
    {
        Cenario cenario = Publicado();
        cenario.Processo.DefinirItens([Item(Novo, 1)]).IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(cenario.Processo).Should().BeNull();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-02 — formulário e exigência de outra finalidade
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Item da habilitação cita fato da inscrição coletado em todas as versões; o que só a base coleta é recusado")]
    public void Habilitacao_ItemCitaFatoDaInscricao_SoOGarantido()
    {
        Cenario cenario = Publicado();
        ProcessoSeletivo processo = cenario.Processo;

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE", 0, "TEM_RENDA")])
            .IsSuccess.Should().BeTrue();
        cenario.PrimeiraCausa().Should().BeNull();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE", 0, Novo)])
            .IsSuccess.Should().BeTrue();
        cenario.PrimeiraCausa()!.Code.Should().Be(FatoAusente);
    }

    [Theory(DisplayName = "Exibição de seção, condição de termo e exibição de grupo da habilitação não citam fato que só a base da inscrição coleta")]
    [InlineData("secao")]
    [InlineData("termo")]
    [InlineData("grupo")]
    public void Habilitacao_DemaisRegrasCitamFatoNaoGarantido_Recusa(string regra)
    {
        Cenario cenario = Publicado();

        Result escrita = EscreverRegraQueCitaFatoNovo(cenario, regra);

        escrita.IsSuccess.Should().BeTrue(escrita.Error?.Message);
        cenario.PrimeiraCausa()!.Code.Should().Be(FatoAusente);
    }

    private static Result EscreverRegraQueCitaFatoNovo(Cenario cenario, string regra) => regra switch
    {
        "secao" => cenario.Processo.DefinirFormulario(
            FinalidadeFormulario.Habilitacao, cenario.Habilitacao.Id, null, PaginasDaHabilitacao(exibicaoDaSegunda: Novo)),
        "termo" => cenario.Processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [TermoQueCita(Novo)]),
        _ => cenario.Processo.DefinirFatosColetados(
            FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE", 0)],
            [GrupoColetadoDeTeste.Agrupando("DEPENDENTES", 1)
                .ComRotulo("Dependentes")
                .ExibidoQuando(Cita(Novo))
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .ComCampos(Campo("DEPENDENTE_RENDA", 0))
                .Construir()]),
    };

    [Fact(DisplayName = "O formulário de habilitação acrescentado depois da publicação também não cita fato que só a base da inscrição coleta")]
    public void HabilitacaoAcrescentadaNaRetificacao_CitaFatoNaoGarantido_Recusa()
    {
        Cenario cenario = Publicado(comHabilitacao: false);
        ProcessoSeletivo processo = cenario.Processo;
        processo.DefinirFormulario(
                FinalidadeFormulario.Habilitacao, cenario.Habilitacao.Id, null, FormularioDeTeste.Paginas(FinalidadeFormulario.Habilitacao))
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE", 0, Novo)])
            .IsSuccess.Should().BeTrue();

        cenario.PrimeiraCausa()!.Code.Should().Be(FatoAusente);
    }

    [Fact(DisplayName = "Exigência da habilitação não cita fato que só a base da inscrição coleta; a da inscrição, cita")]
    public void Exigencia_GatilhoEmFatoNaoGarantido_SoNaFaseDaPropriaFinalidade()
    {
        Cenario cenario = Publicado();
        ConfiguracaoDeclarada configuracao = cenario.Processo.Declaracao();

        FaseEfetivaDoFato.RecusaDeFaseDoGatilho(configuracao, Novo, cenario.Habilitacao.Id, FinalidadeFormulario.Habilitacao, SemCatalogo, SemCatalogo, cenario.Publicadas)!.Code.Should().Be(FatoAusente);
        FaseEfetivaDoFato.RecusaDeFaseDoGatilho(configuracao, Novo, cenario.Inscricao.Id, FinalidadeFormulario.Inscricao, SemCatalogo, SemCatalogo, cenario.Publicadas)
            .Should().BeNull("as exigências da inscrição seguem a versão do próprio formulário");
        FaseEfetivaDoFato.RecusaDeFaseDoGatilho(configuracao, Novo, cenario.Habilitacao.Id, FinalidadeFormulario.Habilitacao, SemCatalogo, SemCatalogo)
            .Should().BeNull("sem versão publicada, ninguém preencheu versão anterior");
    }

    [Fact(DisplayName = "Exigência de fase posterior cita fato do formulário de habilitação acrescentado depois da publicação")]
    public void Exigencia_CitaFatoDeFormularioAcrescentadoNaRetificacao_Aceita()
    {
        Cenario cenario = Publicado(comHabilitacao: false);
        ProcessoSeletivo processo = cenario.Processo;
        processo.DefinirFormulario(
                FinalidadeFormulario.Habilitacao, cenario.Habilitacao.Id, null, FormularioDeTeste.Paginas(FinalidadeFormulario.Habilitacao))
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE", 0)]).IsSuccess.Should().BeTrue();

        FaseEfetivaDoFato.RecusaDeFaseDoGatilho(processo.Declaracao(), "COMPROVANTE", cenario.Matricula.Id, finalidadeDaExigencia: null, SemCatalogo, SemCatalogo, cenario.Publicadas)
            .Should().BeNull("ninguém preencheu a habilitação antes desta retificação");
    }

    /// <summary>
    /// Com dois formulários na mesma fase, a exigência diz a que formulário pertence: o fato novo da
    /// própria finalidade segue a versão do formulário dela, e só o da outra precisa estar em todas as
    /// versões publicadas.
    /// </summary>
    [Theory(DisplayName = "Com dois formulários na fase da exigência, só o fato do outro formulário precisa estar em todas as versões")]
    [InlineData(FinalidadeFormulario.Inscricao, Novo, true)]
    [InlineData(FinalidadeFormulario.Inscricao, "LAUDO", false)]
    [InlineData(FinalidadeFormulario.Habilitacao, "LAUDO", true)]
    [InlineData(FinalidadeFormulario.Habilitacao, Novo, false)]
    public void Exigencia_FaseComDoisFormularios_SegueAPropriaFinalidade(FinalidadeFormulario finalidade, string fato, bool aceito)
    {
        FaseCronograma unica = Fase(1, FormularioProcesso.CodigoFaseHabilitacao, coletaInscricao: true);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: unica);
        FatosDasVersoesPublicadas publicadas = PublicarFormulariosNaMesmaFase(processo, unica);

        DomainError? recusa = FaseEfetivaDoFato.RecusaDeFaseDoGatilho(processo.Declaracao(), fato, unica.Id, finalidade, SemCatalogo, SemCatalogo, publicadas);

        (recusa is null).Should().Be(aceito);
        if (!aceito)
        {
            recusa!.Code.Should().Be(FatoAusente);
        }
    }

    private static FatosDasVersoesPublicadas PublicarFormulariosNaMesmaFase(ProcessoSeletivo processo, FaseCronograma unica)
    {
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(
                FinalidadeFormulario.Habilitacao, unica.Id, null, FormularioDeTeste.Paginas(FinalidadeFormulario.Habilitacao))
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE", 0)]).IsSuccess.Should().BeTrue();
        ConfiguracaoPublicada primeira = VersoesPublicadasDeTeste.DoProcesso(processo);
        processo.DefinirItens([Item("TEM_RENDA", 0), Item(Novo, 1)]).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE", 0), Item("LAUDO", 1)])
            .IsSuccess.Should().BeTrue();
        return VersoesPublicadasDeTeste.SemEixosCongelados([primeira, VersoesPublicadasDeTeste.DoProcesso(processo)]);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-03 — regra de derivação alterada, conferida no congelamento sobre as derivações do documento
    // ══════════════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "O congelamento recusa o derivado citado pela habilitação que a regra alterada faz depender de fato que só a base da inscrição coleta")]
    [InlineData(false)]
    [InlineData(true)]
    public void Congelamento_DerivadoCitadoPassaADependerDeFatoNaoGarantido_Recusa(bool indireto)
    {
        Cenario cenario = Publicado();
        ProcessoSeletivo processo = cenario.Processo;
        processo.DefinirRegrasDerivacao(Derivacoes(indireto, "TEM_RENDA")).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE", 0, "RENDA_DECLARADA")])
            .IsSuccess.Should().BeTrue();
        ItemDoChecklist(cenario).Ok.Should().BeTrue();

        processo.DefinirRegrasDerivacao(Derivacoes(indireto, Novo)).IsSuccess.Should().BeTrue("a escrita da derivação não confere as citações");

        cenario.PrimeiraCausa()!.Code.Should().Be(FatoAusente);
        ItemDoChecklist(cenario).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O gatilho da exigência da habilitação no derivado que a regra alterada faz depender de fato não garantido é recusado")]
    public void Exigencia_DerivadoPassaADependerDeFatoNaoGarantido_Recusa()
    {
        Cenario cenario = Publicado();
        cenario.Processo.DefinirRegrasDerivacao(Derivacoes(indireto: false, "TEM_RENDA")).IsSuccess.Should().BeTrue();
        FaseEfetivaDoFato.RecusaDeFaseDoGatilho(cenario.Processo.Declaracao(), "RENDA_DECLARADA", cenario.Habilitacao.Id, FinalidadeFormulario.Habilitacao, SemCatalogo, SemCatalogo, cenario.Publicadas).Should().BeNull();

        cenario.Processo.DefinirRegrasDerivacao(Derivacoes(indireto: false, Novo)).IsSuccess.Should().BeTrue();

        FaseEfetivaDoFato.RecusaDeFaseDoGatilho(cenario.Processo.Declaracao(), "RENDA_DECLARADA", cenario.Habilitacao.Id, FinalidadeFormulario.Habilitacao, SemCatalogo, SemCatalogo, cenario.Publicadas)!.Code.Should().Be(FatoAusente);
    }

    [Fact(DisplayName = "O derivado usado só pela inscrição depende de fato novo da inscrição sem recusa no congelamento")]
    public void Congelamento_DerivadoUsadoSoPelaInscricao_Aceita()
    {
        Cenario cenario = Publicado();
        cenario.Processo.DefinirRegrasDerivacao(Derivacoes(indireto: false, Novo)).IsSuccess.Should().BeTrue();

        Result itens = cenario.Processo.DefinirItens([Item("TEM_RENDA", 0), Item(Novo, 1), ItemQueCita("USA_RENDA", 3, "RENDA_DECLARADA")]);

        itens.IsSuccess.Should().BeTrue(itens.Error?.Message);
        ItemDoChecklist(cenario).Ok.Should().BeTrue("a inscrição segue a versão do próprio formulário");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Cenário
    // ══════════════════════════════════════════════════════════════════════════════

    private static Cenario Publicado(bool comHabilitacao = true)
    {
        FaseCronograma inscricao = Fase(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = Fase(2, FormularioProcesso.CodigoFaseHabilitacao);
        FaseCronograma matricula = Fase(3, "MATRICULA");
        ProcessoSeletivo processo = ProcessoComFases(inscricao, habilitacao, matricula);
        processo.DefinirItens([Item("TEM_RENDA", 0)], grupos: [Familia(2, "MEMBRO_RENDA")]).IsSuccess.Should().BeTrue();
        if (comHabilitacao)
        {
            DefinirHabilitacao(processo, habilitacao);
        }

        ConfiguracaoPublicada primeira = VersoesPublicadasDeTeste.DoProcesso(processo);
        processo.DefinirItens([Item("TEM_RENDA", 0), Item(Novo, 1)]).IsSuccess.Should().BeTrue();
        FatosDasVersoesPublicadas publicadas = VersoesPublicadasDeTeste.SemEixosCongelados([primeira, VersoesPublicadasDeTeste.DoProcesso(processo)]);
        return new Cenario(processo, inscricao, habilitacao, matricula, publicadas);
    }

    private static ProcessoSeletivo ProcessoComFases(FaseCronograma inscricao, FaseCronograma habilitacao, FaseCronograma matricula)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: inscricao);
        Result cronograma = processo.DefinirCronogramaFases([inscricao, habilitacao, matricula], []);
        cronograma.IsSuccess.Should().BeTrue(cronograma.Error?.Message);
        return processo;
    }

    private static void DefinirHabilitacao(ProcessoSeletivo processo, FaseCronograma habilitacao)
    {
        processo.DefinirFormulario(
                FinalidadeFormulario.Habilitacao, habilitacao.Id, null, FormularioDeTeste.Paginas(FinalidadeFormulario.Habilitacao))
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE", 0)]).IsSuccess.Should().BeTrue();
    }

    private static ItemConformidade ItemDoChecklist(Cenario cenario) =>
        ChecklistDeConformidade.AvaliarConformidade(cenario.Processo.Declaracao(), cenario.Cadastros)
            .Single(static i => i.Codigo == "formulario_fato_ausente_de_versao_publicada");

    /// <summary><c>RENDA_DECLARADA</c> depende da dependência dada, direto ou por um derivado intermediário.</summary>
    private static IReadOnlyList<ConfiguracaoDerivacaoFato> Derivacoes(bool indireto, string dependencia) =>
        indireto
            ? [Derivado("RENDA_DECLARADA", "RENDA_INTERMEDIARIA"), Derivado("RENDA_INTERMEDIARIA", dependencia)]
            : [Derivado("RENDA_DECLARADA", dependencia)];

    private static ConfiguracaoDerivacaoFato Derivado(string derivado, string dependencia) =>
        ConfiguracaoDerivacaoFato.Criar(
            derivado,
            [RegraDerivacaoConfigurada.Criar(0, "SIM", [CondicaoRegraDerivacao.Criar(0, dependencia, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!])
            .Value!;

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao = false) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: coletaInscricao, ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(
                ordem, Inicio: new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, ordem + 1, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar(codigo, PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static FatoColetado Item(string codigo, int ordem) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem).ComObrigatoriedade(Obrigatoriedade.Sempre).NaSecao(FormularioDeTeste.Secao).Construir();

    private static FatoColetado Campo(string codigo, int ordem) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem).ComObrigatoriedade(Obrigatoriedade.Sempre).Construir();

    private static FatoColetado ItemQueCita(string codigo, int ordem, string citado) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem)
            .ExibidoQuando(ExibicaoDeTeste.Quando(citado, Operador.Igual, true))
            .NaSecao(FormularioDeTeste.Secao)
            .Construir();

    private static GrupoColetado Familia(int ordem, params string[] campos) =>
        GrupoColetadoDeTeste.Agrupando("FAMILIA", ordem)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComCampos([.. campos.Select(static (campo, indice) => Campo(campo, indice))])
            .Construir();

    private static PredicadoDnf Cita(string fato) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

    private static TermoExigidoFormulario TermoQueCita(string fato) =>
        TermoExigidoFormulario.Criar(
            "TERMO_0", 0,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Declaração", "Texto", "Base legal", FormaAceite.RegistroSemEnderecoIp, new string('a', 64)),
            Cita(fato), Obrigatoriedade.Sempre).Value!;

    /// <summary>As páginas mínimas da habilitação com uma segunda seção, que a exibição dada condiciona.</summary>
    private static IReadOnlyList<PaginaDoFormulario> PaginasDaHabilitacao(string exibicaoDaSegunda) =>
    [
        PaginaDoFormulario.Criar(new PaginaEstrutura(FormularioDeTeste.Secao, 0, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!,
        PaginaDoFormulario.Criar(new PaginaEstrutura("RENDA", 1, TipoPaginaFormulario.Secao, null), "Renda", null, null, Cita(exibicaoDaSegunda)).Value!,
        PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 2, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
    ];
}
