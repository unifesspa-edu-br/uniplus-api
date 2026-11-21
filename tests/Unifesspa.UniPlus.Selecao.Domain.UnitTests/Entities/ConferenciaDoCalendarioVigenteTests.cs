namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A conferência do calendário de dias úteis (UNI-REQ-0116): contagem que distingue dia útil só é
/// publicável com calendário vigente cadastrado, e a recusa tem erro nomeado próprio, distinto
/// do da convenção de contagem.
/// </summary>
/// <remarks>
/// O gatilho é a existência de fase que aceita recurso, e não a unidade declarada nela: toda
/// regra de recurso carrega prazo de interposição, e as duas unidades declaráveis correm sobre
/// dia útil — dias úteis por definição, horas porque só as situadas em dia útil avançam o
/// relógio. É por isso que os cenários variam a unidade e esperam a mesma recusa.
/// </remarks>
public sealed class ConferenciaDoCalendarioVigenteTests
{
    private static readonly string HashFixo = new('a', 64);

    private static ContextoDeContagemDePrazos SemCalendario => ContextoDeContagemDePrazos.SemCalendario;

    private static ContextoDeContagemDePrazos ComCalendario() => new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    private static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto) =>
        processo.Publicar(
            new CenarioDeCongelamento { Dados = Dados(), Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "teste", Contagem = contexto });

    /// <summary>Produto preliminar da fase 2 — a âncora que a regra de recurso declara.</summary>
    private static readonly Guid ProdutoPreliminarId = new("77770000-0000-4000-8000-000000000002");

    private static RegraRecursoFase Recurso(UnidadePrazo unidade) => RegraRecursoFase.Criar(
        Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, 'd'),
        ParametrosDoRecursoDeTeste.Declarados(unidade == UnidadePrazo.Horas ? 48m : 2m, unidade),
        ProdutoPreliminarId).Value!;

    /// <summary>
    /// Cronograma mínimo que satisfaz os demais conferências: coleta inscrição (a origem do corpus é
    /// inscrição própria), abriga a etapa pontuada na fase do resultado final, produz resultado preliminar recorrível e
    /// fecha com o resultado definitivo.
    /// </summary>
    private static FaseCronograma[] CronogramaComRecurso(UnidadePrazo unidade) =>
        [FaseDeInscricao(), FasePreliminarComRecurso(unidade), FaseDoResultadoFinal()];

    // A fase que coleta inscrição precisa de janela para publicar, mesmo com origem
    // NA_EXECUCAO (issue #1350) — as demais fases deste cronograma seguem sem data.
    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), Fim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static FaseCronograma FasePreliminarComRecurso(UnidadePrazo unidade) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(2, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos:
            [
                ProdutoDaFase.Reidratar(ProdutoPreliminarId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
            ],
            EmiteParecerIndividual: false, RegraRecurso: Recurso(unidade)),
        bancasRequeridas: [BancasDeTeste.DeRecursosDaFase()]).Value!;

    private static FaseCronograma FaseDoResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(3, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    /// <summary>Processo conforme, com uma fase que aceita recurso e a convenção já declarada.</summary>
    private static ProcessoSeletivo ProcessoComRecurso(UnidadePrazo unidade)
    {
        ProcessoSeletivo processo = ProcessoSemConvencaoComRecurso(unidade);
        DeclararAConvencao(processo);

        return processo;
    }

    private static ProcessoSeletivo ProcessoSemConvencaoComRecurso(UnidadePrazo unidade)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        FormularioDeTeste.TrocarCronograma(processo, CronogramaComRecurso(unidade)).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static void DeclararAConvencao(ProcessoSeletivo processo) =>
        processo.DefinirAlgoritmoContagemPrazo(Regra(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, 'e'))
            .IsSuccess.Should().BeTrue();

    private static IReadOnlyList<ItemConformidade> Checklist(ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Com(contexto, motivosEscolhidos: MotivosDoProcessoDeTeste.Cobrindo(processo)));

    private static ItemConformidade ItemDoCalendario(ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto, string codigo = "calendario_vigente_ausente") =>
        Checklist(processo, contexto).Single(i => i.Codigo == codigo);

    [Theory(DisplayName = "Fase com recurso e sem calendário vigente: a publicação é recusada com erro próprio")]
    [InlineData(UnidadePrazo.DiasUteis)]
    [InlineData(UnidadePrazo.Horas)]
    public void SemCalendarioVigente_PublicacaoRecusada(UnidadePrazo unidade)
    {
        ProcessoSeletivo processo = ProcessoComRecurso(unidade);

        Result<VersaoConfiguracao> resultado = Publicar(processo, SemCalendario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CalendarioVigenteAusente",
            "a causa é a falta do dado que diz quais dias são úteis, não a da convenção que diz como contá-los");
        processo.DequeueDomainEvents().Should().BeEmpty(
            "nenhuma versão é criada quando a conferência recusa — nem o evento que a anunciaria");
    }

    [Theory(DisplayName = "Fase com recurso e calendário vigente: a publicação é aceita, nas duas unidades")]
    [InlineData(UnidadePrazo.DiasUteis)]
    [InlineData(UnidadePrazo.Horas)]
    public void ComCalendarioVigente_PublicacaoAceita(UnidadePrazo unidade)
    {
        ProcessoSeletivo processo = ProcessoComRecurso(unidade);

        Result<VersaoConfiguracao> resultado = Publicar(processo, ComCalendario());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Processo sem fase que aceite recurso publica sem calendário vigente")]
    public void SemRecurso_NaoExigeCalendario()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        Result<VersaoConfiguracao> resultado = Publicar(processo, SemCalendario);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Calendário ausente e convenção ausente são causas distintas — a convenção é recusada primeiro")]
    public void CalendarioEConvencao_SaoCausasDistintas()
    {
        ProcessoSeletivo processo = ProcessoSemConvencaoComRecurso(UnidadePrazo.DiasUteis);

        // Sem declarar a convenção: as duas causas coexistem, e o checklist mostra as duas.
        string[] vermelhos = [.. Checklist(processo, SemCalendario).Where(static i => !i.Ok).Select(static i => i.Codigo)];
        vermelhos.Should().Contain("algoritmo_contagem_prazo_nao_declarado")
            .And.Contain("calendario_vigente_ausente",
                "faltar a convenção e faltar o calendário são duas correções diferentes, em lugares diferentes");

        // A publicação recusa pela primeira da precedência, nunca por uma recusa genérica que não diga qual dos dois falta.
        Publicar(processo, SemCalendario).Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado");
        DeclararAConvencao(processo);

        Publicar(processo, SemCalendario).Error!.Code.Should().Be("ProcessoSeletivo.CalendarioVigenteAusente",
            "resolvida a convenção, a recusa passa a ser a do calendário — cada causa aparece por si");
    }

    /// <summary>
    /// Dataset vigente existe mas veio incoerente do cadastro. A causa é distinta da ausência, e
    /// só alcança quem precisa do calendário.
    /// </summary>
    /// <remarks>
    /// Tratar a falha como ausência deixaria o painel de conformidade aprovado para um processo sem contagem
    /// sobre dia útil — que de fato não usa o dado — enquanto a publicação recusava por causa
    /// dele. Abortar na tradução recusaria também esse processo, que nada tem a corrigir.
    /// </remarks>
    [Fact(DisplayName = "Calendário vigente inválido barra quem depende dele, com a causa da invalidez")]
    public void CalendarioInvalido_BarraQuemDepende()
    {
        var falha = new DomainError(
            "CalendarioVigente.FormaTerritorialInvalida",
            "O calendário vigente traz 2026-01-01 em forma incoerente: dia estadual não carrega município.");
        var contexto = new ContextoDeContagemDePrazos(null, FalhaDoCalendarioVigente: falha, FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));

        ProcessoSeletivo comRecurso = ProcessoComRecurso(UnidadePrazo.DiasUteis);

        Publicar(comRecurso, contexto).Error!.Code.Should().Be("CalendarioVigente.FormaTerritorialInvalida",
            "quem depende do calendário é barrado pela causa real — dizer que não há calendário mandaria cadastrar um que já está lá");

        ItemDoCalendario(comRecurso, contexto).Ok.Should().BeFalse("o checklist acusa a mesma pendência que bloqueia a publicação");
    }

    [Fact(DisplayName = "Calendário vigente inválido não impede quem não depende dele")]
    public void CalendarioInvalido_NaoImpedeQuemNaoDepende()
    {
        var contexto = new ContextoDeContagemDePrazos(
            null,

            FalhaDoCalendarioVigente: new DomainError("CalendarioVigente.FormaTerritorialInvalida", "incoerente"));

        ProcessoSeletivo semRecurso = ProcessoConformeFactory.Criar();

        Publicar(semRecurso, contexto).IsSuccess.Should().BeTrue(
            "o processo não tem contagem que distinga dia útil — o dado quebrado não é dele");

        ItemDoCalendario(semRecurso, contexto).Ok.Should().BeTrue("e o checklist concorda com a publicação");
    }

    [Fact(DisplayName = "O checklist projeta o calendário e o fuso, e coincide com a conferência")]
    public void Checklist_ProjetaCalendarioEFuso()
    {
        ProcessoSeletivo processo = ProcessoComRecurso(UnidadePrazo.DiasUteis);

        ItemDoCalendario(processo, SemCalendario).Should().Match<ItemConformidade>(static i => !i.Ok && i.Dimensao == DimensaoConformidade.ContagemDePrazos);
        ItemDoCalendario(processo, ComCalendario()).Ok.Should().BeTrue();

        // O fuso não é conferência, é defeito de instalação com recusa 500 no handler, mas o preflight o mostra: sem isso, o
        // checklist diria "pronto" e a publicação devolveria um erro que quem publica não teria como prever nem corrigir.
        var ambienteQuebrado = new ContextoDeContagemDePrazos(ComCalendario().CalendarioVigente, FusoInstitucional: null);

        ItemDoCalendario(processo, ambienteQuebrado, "fuso_institucional_nao_reconhecido").Ok.Should().BeFalse();
        Publicar(processo, ambienteQuebrado).IsSuccess.Should().BeTrue(
            "o fuso irresolvível é barrado no handler, que tem a leitura da base de fusos — a raiz não o reavalia");
    }
}
