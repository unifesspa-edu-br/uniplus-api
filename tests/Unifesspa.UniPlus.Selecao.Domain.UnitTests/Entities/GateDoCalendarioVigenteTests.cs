namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O gate do calendário de dias úteis (UNI-REQ-0116): contagem que distingue dia útil só é
/// publicável com calendário vigente cadastrado, e a recusa tem erro nomeado próprio, distinto
/// do da convenção de contagem.
/// </summary>
/// <remarks>
/// O gatilho é a existência de fase que aceita recurso, e não a unidade declarada nela: toda
/// regra de recurso carrega prazo de interposição, e as duas unidades declaráveis correm sobre
/// dia útil — dias úteis por definição, horas porque só as situadas em dia útil avançam o
/// relógio. É por isso que os cenários variam a unidade e esperam a mesma recusa.
/// </remarks>
public sealed class GateDoCalendarioVigenteTests
{
    private static readonly string HashFixo = new('a', 64);

    private static ContextoDeContagemDePrazos SemCalendario => ContextoDeContagemDePrazos.SemCalendario;

    private static ContextoDeContagemDePrazos ComCalendario() => new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucionalReconhecido: true);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.CreateVersion7()).Value!;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto) =>
        processo.Publicar(
            Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste",
            TimeProvider.System, contexto);

    private static RegraRecursoFase Recurso(UnidadePrazo unidade) => RegraRecursoFase.Criar(
        Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, 'd'),
        new ArgsRegraPrazoRecurso(
            PrazoValor: unidade == UnidadePrazo.Horas ? 48m : 2m,
            PrazoUnidade: unidade,
            AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null)).Value!;

    /// <summary>
    /// Cronograma mínimo que satisfaz os demais gates: coleta inscrição (a origem do corpus é
    /// inscrição própria), agrupa a etapa pontuada, produz resultado preliminar recorrível e
    /// fecha com o resultado definitivo.
    /// </summary>
    private static FaseCronograma[] CronogramaComRecurso(UnidadePrazo unidade) =>
    [
        FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false, produzResultado: false,
            resultadoDefinitivo: false, coletaInscricao: true, inicio: null, fim: null,
            atoProduzidoCodigo: null, atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: null).Value!,
        FaseCronograma.Criar(
            2, Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: true, permiteComplementacao: false, produzResultado: true,
            resultadoDefinitivo: false, coletaInscricao: false, inicio: null, fim: null,
            atoProduzidoCodigo: "RESULTADO_PRELIMINAR", atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: Recurso(unidade)).Value!,
        FaseCronograma.Criar(
            3, Guid.CreateVersion7(), "RESULTADO_FINAL", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false, produzResultado: true,
            resultadoDefinitivo: true, coletaInscricao: false, inicio: null, fim: null,
            atoProduzidoCodigo: "RESULTADO_FINAL", atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: null).Value!,
    ];

    /// <summary>Processo conforme, com uma fase que aceita recurso e a convenção já declarada.</summary>
    private static ProcessoSeletivo ProcessoComRecurso(UnidadePrazo unidade)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirCronogramaFases(
            CronogramaComRecurso(unidade), [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.DefinirAlgoritmoContagemPrazo(
            Regra(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, 'e'), PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        return processo;
    }

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
            "nenhuma versão é criada quando o gate recusa — nem o evento que a anunciaria");
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
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirCronogramaFases(
            CronogramaComRecurso(UnidadePrazo.DiasUteis), [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        // Sem declarar a convenção: as duas causas coexistem, e o checklist mostra as duas.
        string[] vermelhos = [.. processo.AvaliarConformidade(SemCalendario)
            .Where(static i => !i.Ok).Select(static i => i.Codigo)];
        vermelhos.Should().Contain("algoritmo_contagem_prazo_nao_declarado")
            .And.Contain("calendario_vigente_ausente",
                "faltar a convenção e faltar o calendário são duas correções diferentes, em lugares diferentes");

        // A publicação recusa pela primeira da precedência — nunca por uma recusa genérica que
        // não diga qual dos dois falta (UNI-REQ-0116).
        Publicar(processo, SemCalendario).Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado");

        processo.DefinirAlgoritmoContagemPrazo(
            Regra(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, 'e'), PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        Publicar(processo, SemCalendario).Error!.Code.Should().Be("ProcessoSeletivo.CalendarioVigenteAusente",
            "resolvida a convenção, a recusa passa a ser a do calendário — cada causa aparece por si");
    }

    /// <summary>
    /// Dataset vigente existe mas veio incoerente do cadastro. A causa é distinta da ausência, e
    /// só alcança quem precisa do calendário.
    /// </summary>
    /// <remarks>
    /// Tratar a falha como ausência deixaria o checklist verde para um processo sem contagem
    /// sobre dia útil — que de fato não usa o dado — enquanto a publicação recusava por causa
    /// dele. Abortar na tradução recusaria também esse processo, que nada tem a corrigir.
    /// </remarks>
    [Fact(DisplayName = "Calendário vigente inválido barra quem depende dele, com a causa da invalidez")]
    public void CalendarioInvalido_BarraQuemDepende()
    {
        var falha = new DomainError(
            "CalendarioVigente.FormaTerritorialInvalida",
            "O calendário vigente traz 2026-01-01 em forma incoerente: dia estadual não carrega município.");
        var contexto = new ContextoDeContagemDePrazos(null, FusoInstitucionalReconhecido: true, FalhaDoCalendarioVigente: falha);

        ProcessoSeletivo comRecurso = ProcessoComRecurso(UnidadePrazo.DiasUteis);

        Publicar(comRecurso, contexto).Error!.Code.Should().Be("CalendarioVigente.FormaTerritorialInvalida",
            "quem depende do calendário é barrado pela causa real — dizer que não há calendário mandaria cadastrar um que já está lá");

        comRecurso.AvaliarConformidade(contexto)
            .Should().ContainSingle(static i => i.Codigo == "calendario_vigente_ausente" && !i.Ok,
                "o checklist acusa a mesma pendência que bloqueia a publicação");
    }

    [Fact(DisplayName = "Calendário vigente inválido não impede quem não depende dele")]
    public void CalendarioInvalido_NaoImpedeQuemNaoDepende()
    {
        var contexto = new ContextoDeContagemDePrazos(
            null,
            FusoInstitucionalReconhecido: true,
            FalhaDoCalendarioVigente: new DomainError("CalendarioVigente.FormaTerritorialInvalida", "incoerente"));

        ProcessoSeletivo semRecurso = ProcessoConformeFactory.Criar();

        Publicar(semRecurso, contexto).IsSuccess.Should().BeTrue(
            "o processo não tem contagem que distinga dia útil — o dado quebrado não é dele");

        semRecurso.AvaliarConformidade(contexto)
            .Should().ContainSingle(static i => i.Codigo == "calendario_vigente_ausente" && i.Ok,
                "e o checklist concorda com a publicação, que é a bicondicionalidade");
    }

    [Fact(DisplayName = "O checklist projeta o calendário e o fuso, e é bicondicional com o gate")]
    public void Checklist_ProjetaCalendarioEFuso()
    {
        ProcessoSeletivo processo = ProcessoComRecurso(UnidadePrazo.DiasUteis);

        processo.AvaliarConformidade(SemCalendario)
            .Should().ContainSingle(static i => i.Codigo == "calendario_vigente_ausente" && !i.Ok)
            .Which.Dimensao.Should().Be(DimensaoConformidade.ContagemDePrazos);

        processo.AvaliarConformidade(ComCalendario())
            .Should().ContainSingle(static i => i.Codigo == "calendario_vigente_ausente" && i.Ok);

        // O fuso não é gate — é defeito de instalação, com recusa 500 no handler —, mas o
        // preflight o mostra: sem isso, o checklist diria "pronto" e a publicação devolveria
        // um erro que quem publica não teria como prever nem corrigir.
        var ambienteQuebrado = new ContextoDeContagemDePrazos(
            ComCalendario().CalendarioVigente, FusoInstitucionalReconhecido: false);

        processo.AvaliarConformidade(ambienteQuebrado)
            .Should().ContainSingle(static i => i.Codigo == "fuso_institucional_nao_reconhecido" && !i.Ok);

        Publicar(processo, ambienteQuebrado).IsSuccess.Should().BeTrue(
            "o fuso irresolvível é barrado no handler, que tem a leitura da base de fusos — a raiz não o reavalia");
    }
}
