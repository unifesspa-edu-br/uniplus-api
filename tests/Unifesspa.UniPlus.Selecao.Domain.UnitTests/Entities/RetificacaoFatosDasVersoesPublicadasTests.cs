namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Na retificação, o formulário e as exigências de uma finalidade só usam, de outra finalidade, fato
/// que o formulário dela coletou em todas as versões publicadas, e a inscrição não deixa de coletar o
/// que já coletava (UNI-REQ-0144): o candidato pode ter preenchido cada finalidade em qualquer versão.
/// </summary>
/// <remarks>
/// O cenário publica duas versões: a inscrição coleta <c>TEM_RENDA</c> e o grupo <c>FAMILIA</c> nas
/// duas, e <see cref="Novo"/> só na segunda, que é a base da sessão; a habilitação, quando existe,
/// coleta <c>COMPROVANTE</c> nas duas.
/// </remarks>
public sealed class RetificacaoFatosDasVersoesPublicadasTests
{
    /// <summary>Coletado pela inscrição só na versão base.</summary>
    private const string Novo = "NOVO";

    private const string FatoAusente = "RascunhoRetificacao.FatoAusenteDeVersaoPublicada";

    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Dictionary<string, string> SemCatalogo = new(StringComparer.Ordinal);
    private static readonly PrecondicaoIfMatch Sessao = PrecondicaoIfMatch.Curinga;

    private sealed record Cenario(ProcessoSeletivo Processo, FaseCronograma Inscricao, FaseCronograma Habilitacao, FaseCronograma Matricula);

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-01 — a inscrição continua coletando o que já coletava, no mesmo papel
    // ══════════════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "A retificação recusa a inscrição que deixa de coletar, no mesmo papel, fato que a versão base coletava")]
    [InlineData("item retirado")]
    [InlineData("item passa a campo de grupo")]
    [InlineData("grupo retirado")]
    public void Inscricao_DeixaDeColetarFatoDaBase_Recusa(string caso)
    {
        ProcessoSeletivo processo = Publicado().Processo;

        Result resultado = caso switch
        {
            "item retirado" => processo.DefinirItens([Item(Novo, 1)], Sessao),
            "item passa a campo de grupo" => processo.DefinirItens([Item(Novo, 1)], Sessao, grupos: [Familia(2, "MEMBRO_RENDA", "TEM_RENDA")]),
            _ => processo.DefinirItens([Item("TEM_RENDA", 0), Item(Novo, 1)], Sessao, grupos: []),
        };

        resultado.Error!.Code.Should().Be("RascunhoRetificacao.FatoJaColetadoRemovido");
    }

    [Fact(DisplayName = "A retificação acrescenta item antes dos que a inscrição já coletava, deslocando a ordem deles")]
    public void Inscricao_ItemNovoAntesDosDaBase_Aceita()
    {
        ProcessoSeletivo processo = Publicado().Processo;

        Result resultado = processo.DefinirItens(
            [Item("ANTES", 0), Item("TEM_RENDA", 1), Item(Novo, 2)], Sessao, grupos: [Familia(3, "MEMBRO_RENDA")]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-02 — formulário e exigência de outra finalidade
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Item da habilitação cita fato da inscrição coletado em todas as versões; o que só a base coleta é recusado")]
    public void Habilitacao_ItemCitaFatoDaInscricao_SoOGarantido()
    {
        ProcessoSeletivo processo = Publicado().Processo;

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE", 0, "TEM_RENDA")], Sessao)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE", 0, Novo)], Sessao)
            .Error!.Code.Should().Be(FatoAusente);
    }

    [Theory(DisplayName = "Exibição de seção, condição de termo e exibição de grupo da habilitação não citam fato que só a base da inscrição coleta")]
    [InlineData("secao", "etapas[1].exibicao")]
    [InlineData("termo", "termos[0]")]
    [InlineData("grupo", null)]
    public void Habilitacao_DemaisRegrasCitamFatoNaoGarantido_Recusa(string regra, string? campo)
    {
        Cenario cenario = Publicado();
        ProcessoSeletivo processo = cenario.Processo;

        Result resultado = regra switch
        {
            "secao" => processo.DefinirFormulario(
                FinalidadeFormulario.Habilitacao, cenario.Habilitacao.Id, null, EtapasDaHabilitacao(exibicaoDaSegunda: Novo), Sessao),
            "termo" => processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [TermoQueCita(Novo)], Sessao),
            _ => processo.DefinirFatosColetados(
                FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE", 0)], Sessao,
                [GrupoColetado.Criar("DEPENDENTES", 1, FormularioDeTeste.Secao, "Dependentes", 0, 5, Cita(Novo), Obrigatoriedade.Sempre, [Campo("DEPENDENTE_RENDA", 0)]).Value!]),
        };

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Field = campo, Error = new { Code = FatoAusente } });
    }

    [Fact(DisplayName = "O formulário de habilitação acrescentado na retificação também não cita fato que só a base da inscrição coleta")]
    public void HabilitacaoAcrescentadaNaSessao_CitaFatoNaoGarantido_Recusa()
    {
        Cenario cenario = Publicado(comHabilitacao: false);
        ProcessoSeletivo processo = cenario.Processo;
        processo.DefinirFormulario(
                FinalidadeFormulario.Habilitacao, cenario.Habilitacao.Id, null, FormularioDeTeste.Etapas(FinalidadeFormulario.Habilitacao), Sessao)
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE", 0, Novo)], Sessao)
            .Error!.Code.Should().Be(FatoAusente);
    }

    [Fact(DisplayName = "Exigência da habilitação não cita fato que só a base da inscrição coleta; a da inscrição, cita")]
    public void Exigencia_GatilhoEmFatoNaoGarantido_SoNaFaseDaPropriaFinalidade()
    {
        Cenario cenario = Publicado();

        cenario.Processo.RecusaDeFaseDoGatilho(Novo, cenario.Habilitacao.Id, FinalidadeFormulario.Habilitacao, SemCatalogo, SemCatalogo)!.Code.Should().Be(FatoAusente);
        cenario.Processo.RecusaDeFaseDoGatilho(Novo, cenario.Inscricao.Id, FinalidadeFormulario.Inscricao, SemCatalogo, SemCatalogo)
            .Should().BeNull("as exigências da inscrição seguem a versão do próprio formulário");
    }

    [Fact(DisplayName = "Exigência de fase posterior cita fato do formulário de habilitação acrescentado na retificação")]
    public void Exigencia_CitaFatoDeFormularioAcrescentadoNaSessao_Aceita()
    {
        Cenario cenario = Publicado(comHabilitacao: false);
        ProcessoSeletivo processo = cenario.Processo;
        processo.DefinirFormulario(
                FinalidadeFormulario.Habilitacao, cenario.Habilitacao.Id, null, FormularioDeTeste.Etapas(FinalidadeFormulario.Habilitacao), Sessao)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE", 0)], Sessao).IsSuccess.Should().BeTrue();

        processo.RecusaDeFaseDoGatilho("COMPROVANTE", cenario.Matricula.Id, finalidadeDaExigencia: null, SemCatalogo, SemCatalogo)
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
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(
                FinalidadeFormulario.Habilitacao, unica.Id, null, FormularioDeTeste.Etapas(FinalidadeFormulario.Habilitacao), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        GrafoConfiguracao primeira = VersoesPublicadasDeTeste.DoProcesso(processo);
        processo.DefinirItens([Item("TEM_RENDA", 0), Item(Novo, 1)]).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE", 0), Item("LAUDO", 1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        AbrirSessao(processo, [primeira, VersoesPublicadasDeTeste.DoProcesso(processo)]);

        DomainError? recusa = processo.RecusaDeFaseDoGatilho(fato, unica.Id, finalidade, SemCatalogo, SemCatalogo);

        (recusa is null).Should().Be(aceito);
        if (!aceito)
        {
            recusa!.Code.Should().Be(FatoAusente);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-03 — regra de derivação alterada, conferida no fechamento sobre as derivações vivas
    // ══════════════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "O fechamento recusa o derivado citado pela habilitação que a regra alterada faz depender de fato que só a base da inscrição coleta")]
    [InlineData(false)]
    [InlineData(true)]
    public void Fechamento_DerivadoCitadoPassaADependerDeFatoNaoGarantido_Recusa(bool indireto)
    {
        ProcessoSeletivo processo = Publicado().Processo;
        processo.DefinirRegrasDerivacao(Derivacoes(indireto, "TEM_RENDA"), Sessao).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE", 0, "RENDA_DECLARADA")], Sessao)
            .IsSuccess.Should().BeTrue();
        ItemDoChecklist(processo).Ok.Should().BeTrue();

        processo.DefinirRegrasDerivacao(Derivacoes(indireto, Novo), Sessao).IsSuccess.Should().BeTrue("a escrita da derivação não confere as citações");

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(FatoAusente);
        ItemDoChecklist(processo).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O gatilho da exigência da habilitação no derivado que a regra alterada faz depender de fato não garantido é recusado")]
    public void Exigencia_DerivadoPassaADependerDeFatoNaoGarantido_Recusa()
    {
        Cenario cenario = Publicado();
        cenario.Processo.DefinirRegrasDerivacao(Derivacoes(indireto: false, "TEM_RENDA"), Sessao).IsSuccess.Should().BeTrue();
        cenario.Processo.RecusaDeFaseDoGatilho("RENDA_DECLARADA", cenario.Habilitacao.Id, FinalidadeFormulario.Habilitacao, SemCatalogo, SemCatalogo).Should().BeNull();

        cenario.Processo.DefinirRegrasDerivacao(Derivacoes(indireto: false, Novo), Sessao).IsSuccess.Should().BeTrue();

        cenario.Processo.RecusaDeFaseDoGatilho("RENDA_DECLARADA", cenario.Habilitacao.Id, FinalidadeFormulario.Habilitacao, SemCatalogo, SemCatalogo)!.Code.Should().Be(FatoAusente);
    }

    [Fact(DisplayName = "O derivado usado só pela inscrição passa a depender de fato novo da inscrição sem recusa no fechamento")]
    public void Fechamento_DerivadoUsadoSoPelaInscricao_Aceita()
    {
        ProcessoSeletivo processo = Publicado().Processo;
        processo.DefinirRegrasDerivacao(Derivacoes(indireto: false, Novo), Sessao).IsSuccess.Should().BeTrue();

        Result itens = processo.DefinirItens([Item("TEM_RENDA", 0), Item(Novo, 1), ItemQueCita("USA_RENDA", 3, "RENDA_DECLARADA")], Sessao);

        itens.IsSuccess.Should().BeTrue(itens.Error?.Message);
        ItemDoChecklist(processo).Ok.Should().BeTrue("a inscrição segue a versão do próprio formulário");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Cenário
    // ══════════════════════════════════════════════════════════════════════════════

    private static Cenario Publicado(bool comHabilitacao = true)
    {
        FaseCronograma inscricao = Fase(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = Fase(2, FormularioProcesso.CodigoFaseHabilitacao);
        FaseCronograma matricula = Fase(3, "MATRICULA");
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: inscricao);
        Result cronograma = processo.DefinirCronogramaFases([inscricao, habilitacao, matricula], [], PrecondicaoIfMatch.Ausente);
        cronograma.IsSuccess.Should().BeTrue(cronograma.Error?.Message);
        processo.DefinirItens([Item("TEM_RENDA", 0)], grupos: [Familia(2, "MEMBRO_RENDA")]).IsSuccess.Should().BeTrue();
        if (comHabilitacao)
        {
            processo.DefinirFormulario(
                    FinalidadeFormulario.Habilitacao, habilitacao.Id, null, FormularioDeTeste.Etapas(FinalidadeFormulario.Habilitacao), PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();
            processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        GrafoConfiguracao primeira = VersoesPublicadasDeTeste.DoProcesso(processo);
        processo.DefinirItens([Item("TEM_RENDA", 0), Item(Novo, 1)]).IsSuccess.Should().BeTrue();
        AbrirSessao(processo, [primeira, VersoesPublicadasDeTeste.DoProcesso(processo)]);
        return new Cenario(processo, inscricao, habilitacao, matricula);
    }

    /// <summary>
    /// Abre a sessão sobre as versões dadas. A publicação de verdade não é o assunto: o que importa é
    /// o que as versões coletaram, e o processo passa a publicado sem passar pelos gates dela.
    /// </summary>
    private static void AbrirSessao(ProcessoSeletivo processo, IReadOnlyList<GrafoConfiguracao> versoes)
    {
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Status))!.SetValue(processo, StatusProcesso.Publicado);
        VersaoConfiguracao versaoBase = VersaoConfiguracao.Abrir(
            processo.Id, Encoding.UTF8.GetBytes("{}"), "1.1", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", Agora);
        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            "Correção da coleta", versaoBase, identificadorDaVersaoBase: null, FatosDasVersoesPublicadas.De(versoes), "user-sub-1", Agora);
        abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);
    }

    private static ItemConformidade ItemDoChecklist(ProcessoSeletivo processo) =>
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo)
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
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Propria,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, ordem + 1, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;

    private static FatoColetado Item(string codigo, int ordem) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao).Value!;

    private static FatoColetado Campo(string codigo, int ordem) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null).Value!;

    private static FatoColetado ItemQueCita(string codigo, int ordem, string citado) =>
        FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Nunca,
            [CondicaoPrecondicaoFato.Criar(0, citado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            etapaCodigo: FormularioDeTeste.Secao).Value!;

    private static GrupoColetado Familia(int ordem, params string[] campos) =>
        GrupoColetado.Criar(
            "FAMILIA", ordem, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null, Obrigatoriedade.Sempre,
            [.. campos.Select(static (campo, indice) => Campo(campo, indice))]).Value!;

    private static PredicadoDnf Cita(string fato) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

    private static TermoExigidoFormulario TermoQueCita(string fato) =>
        TermoExigidoFormulario.Criar(
            "TERMO_0", 0,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "Declaração", "Texto", "Base legal", "REGISTRO_DIGITAL_SEM_LOG_IP", new string('a', 64)),
            Cita(fato), Obrigatoriedade.Sempre).Value!;

    /// <summary>As etapas mínimas da habilitação com uma segunda seção, que a exibição dada condiciona.</summary>
    private static IReadOnlyList<EtapaFormulario> EtapasDaHabilitacao(string exibicaoDaSegunda) =>
    [
        EtapaFormulario.Criar(FormularioDeTeste.Secao, 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null).Value!,
        EtapaFormulario.Criar("RENDA", 1, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Renda", null, null, Cita(exibicaoDaSegunda)).Value!,
        EtapaFormulario.Criar("REVISAO", 2, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
    ];
}
