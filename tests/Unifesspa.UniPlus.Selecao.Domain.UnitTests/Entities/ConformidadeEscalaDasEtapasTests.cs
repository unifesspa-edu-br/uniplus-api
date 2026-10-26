namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O gate da escala das etapas: a etapa com nota declara a nota máxima, e a que não tem nota e a
/// do ENEM não declaram escala nenhuma. É gate, e não recusa de gravação, porque depende do que
/// o grafo diz sobre a etapa (média, nota mínima, eliminação, desempate).
/// </summary>
public sealed class ConformidadeEscalaDasEtapasTests
{
    private static readonly string HashFixo = new('a', 64);

    private static TipoEtapaSnapshot Tipo(bool notaDoEnem = false) =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), notaDoEnem ? "NOTA_ENEM" : "PROVA_OBJETIVA", "Prova", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: notaDoEnem).Value!;

    private static EtapaProcesso Etapa(
        string nome, int ordem, CaraterEtapa carater = CaraterEtapa.Classificatoria, decimal? peso = null,
        decimal? notaMinima = null, decimal? notaMaxima = null, decimal? passo = null, bool notaDoEnem = false) =>
        EtapaProcesso.Criar(nome, carater, Tipo(notaDoEnem), peso, notaMinima, ordem, notaMaxima: notaMaxima, passoDoLancamento: passo).Value!;

    private static EtapaProcesso EtapaDaMedia(string nome = "Prova", int ordem = 1, decimal? notaMaxima = 10m) =>
        Etapa(nome, ordem, peso: 1m, notaMaxima: notaMaxima);

    private static ProcessoSeletivo ProcessoCom(params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas(etapas, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static ConfiguracaoClassificacao ClassificacaoComEliminacao(Guid etapaRef, decimal notaMinima) =>
        ConfiguracaoClassificacao.Criar(
            ProcessoConformeFactory.Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
            ProcessoConformeFactory.Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "c"),
            2,
            ProcessoConformeFactory.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "d"),
            [RegraEliminacao.Criar(
                ProcessoConformeFactory.Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "e"),
                new ArgsElimNotaMinimaEtapa(etapaRef, notaMinima)).Value!],
            baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!;

    private static ConfiguracaoClassificacao ClassificacaoSemEliminacao() =>
        ConfiguracaoClassificacao.Criar(
            ProcessoConformeFactory.Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
            ProcessoConformeFactory.Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "c"),
            2,
            ProcessoConformeFactory.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "d"),
            [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!;

    /// <summary>
    /// A etapa de nota do ENEM só convive com classificação baseada em ENEM pela média, e a
    /// classificação é definida antes das etapas para a gravação aceitá-la.
    /// </summary>
    private static ProcessoSeletivo ProcessoComEtapaDoEnem(EtapaProcesso enem)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                ProcessoConformeFactory.Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
                ProcessoConformeFactory.Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "c"),
                2,
                ProcessoConformeFactory.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "d"),
                [], baseadoEmEnem: true, QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo()).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([EtapaDaMedia(), enem], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static void CitarPorEliminacao(ProcessoSeletivo processo, Guid etapaId, decimal notaMinima) =>
        processo.DefinirClassificacao(ClassificacaoComEliminacao(etapaId, notaMinima), PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

    private static void CitarPorDesempate(ProcessoSeletivo processo, Guid etapaId)
    {
        processo.DefinirClassificacao(ClassificacaoSemEliminacao(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(
                1,
                ProcessoConformeFactory.Regra(CriterioDesempateCodigo.MaiorNotaEtapa, "f"),
                new ArgsDesempateMaiorNotaEtapa(etapaId)).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
    }

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        ProcessoConformeFactory.Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste",
        TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

    private static IEnumerable<ItemConformidade> Vermelhos(ProcessoSeletivo processo) =>
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum)
            .Where(static i => !i.Ok && i.Dimensao == DimensaoConformidade.Etapas);

    /// <summary>
    /// As quatro razões de a etapa ter nota, uma por vez: cada uma, sozinha, faz a nota máxima
    /// passar a ser exigida. É o teste que quebra se uma regra futura criar outra citação.
    /// </summary>
    public static TheoryData<string> RazoesDeTerNota => new() { "media", "nota minima", "eliminacao", "desempate" };

    private static ProcessoSeletivo ProcessoComUmaEtapaComNotaPor(string razao, decimal? notaMaxima)
    {
        EtapaProcesso base1 = EtapaDaMedia();
        EtapaProcesso alvo = razao switch
        {
            "media" => Etapa("Alvo", 2, peso: 1m, notaMaxima: notaMaxima),
            "nota minima" => Etapa("Alvo", 2, CaraterEtapa.Eliminatoria, notaMinima: 5m, notaMaxima: notaMaxima),
            _ => Etapa("Alvo", 2, CaraterEtapa.Eliminatoria, notaMaxima: notaMaxima),
        };

        ProcessoSeletivo processo = ProcessoCom(base1, alvo);
        if (razao == "eliminacao")
        {
            CitarPorEliminacao(processo, alvo.Id, 5m);
        }
        else if (razao == "desempate")
        {
            CitarPorDesempate(processo, alvo.Id);
        }
        else
        {
            processo.DefinirClassificacao(ClassificacaoSemEliminacao(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        }

        return processo;
    }

    [Theory(DisplayName = "Etapa com nota sem nota máxima recusa a publicação, qualquer que seja a razão de ter nota")]
    [MemberData(nameof(RazoesDeTerNota))]
    public void EtapaComNotaSemNotaMaxima_RecusaPublicacao(string razao)
    {
        ProcessoSeletivo processo = ProcessoComUmaEtapaComNotaPor(razao, notaMaxima: null);

        Vermelhos(processo).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_com_nota_sem_nota_maxima" && i.Mensagem.Contains("\"Alvo\"", StringComparison.Ordinal));

        Result<VersaoConfiguracao> publicar = Publicar(processo);
        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }

    [Theory(DisplayName = "A mesma configuração com a nota máxima declarada publica")]
    [MemberData(nameof(RazoesDeTerNota))]
    public void EtapaComNotaComNotaMaxima_Publica(string razao)
    {
        ProcessoSeletivo processo = ProcessoComUmaEtapaComNotaPor(razao, notaMaxima: 10m);

        Vermelhos(processo).Should().BeEmpty();
        Publicar(processo).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Em rascunho, a etapa com nota sem escala é gravada: a falta só recusa a publicação")]
    public void Rascunho_EtapaComNotaSemEscala_Grava()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Rascunho", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirEtapas([EtapaDaMedia(notaMaxima: null)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Etapa sem nota com nota máxima ou com passo recusa a publicação; sem escala publica")]
    public void EtapaSemNotaComEscala_Recusa()
    {
        EtapaProcesso comNotaMaxima = Etapa("Análise", 2, CaraterEtapa.Eliminatoria, notaMaxima: 10m);
        EtapaProcesso comPasso = Etapa("Entrevista", 3, CaraterEtapa.Eliminatoria, passo: 0.5m);
        ProcessoSeletivo processo = ProcessoCom(EtapaDaMedia(), comNotaMaxima, comPasso);

        Vermelhos(processo).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_sem_nota_com_escala"
                && i.Mensagem.Contains("\"Análise\"; \"Entrevista\"", StringComparison.Ordinal));
        Publicar(processo).IsFailure.Should().BeTrue();

        ProcessoSeletivo semEscala = ProcessoCom(EtapaDaMedia(), Etapa("Análise", 2, CaraterEtapa.Eliminatoria));
        Vermelhos(semEscala).Should().BeEmpty();
        Publicar(semEscala).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Etapa de nota do ENEM com nota máxima ou com passo recusa a publicação")]
    [InlineData(true)]
    [InlineData(false)]
    public void EtapaDoEnemComEscala_Recusa(bool comNotaMaxima)
    {
        EtapaProcesso enem = Etapa(
            "Nota do ENEM", 2, peso: 1m,
            notaMaxima: comNotaMaxima ? 1000m : null, passo: comNotaMaxima ? null : 1m, notaDoEnem: true);
        ProcessoSeletivo processo = ProcessoComEtapaDoEnem(enem);

        Vermelhos(processo).Should().ContainSingle().Which.Codigo.Should().Be("etapa_do_enem_com_escala");
    }

    [Fact(DisplayName = "Etapa de nota do ENEM sem escala publica sem nota máxima")]
    public void EtapaDoEnemSemEscala_NaoExigeNotaMaxima()
    {
        ProcessoSeletivo processo = ProcessoComEtapaDoEnem(Etapa("Nota do ENEM", 2, peso: 1m, notaDoEnem: true));

        Vermelhos(processo).Should().BeEmpty();
    }

    [Fact(DisplayName = "Nota mínima da regra acima da nota máxima da etapa recusa; igual aceita")]
    public void NotaMinimaDaRegraAcimaDaNotaMaxima_Recusa()
    {
        EtapaProcesso entrevista = Etapa("Entrevista", 2, CaraterEtapa.Eliminatoria, notaMaxima: 10m);
        ProcessoSeletivo acima = ProcessoCom(EtapaDaMedia(), entrevista);
        CitarPorEliminacao(acima, entrevista.Id, 10.5m);

        Vermelhos(acima).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "nota_minima_da_regra_acima_da_nota_maxima" && i.Mensagem.Contains("\"Entrevista\"", StringComparison.Ordinal));

        ProcessoSeletivo igual = ProcessoCom(EtapaDaMedia(), entrevista);
        CitarPorEliminacao(igual, entrevista.Id, 10m);
        Vermelhos(igual).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem nota máxima declarada, só o item da nota máxima reprova, não o da regra")]
    public void NotaMinimaDaRegra_SemNotaMaximaDeclarada_NaoReprovaOItemDaRegra()
    {
        EtapaProcesso entrevista = Etapa("Entrevista", 2, CaraterEtapa.Eliminatoria);
        ProcessoSeletivo processo = ProcessoCom(EtapaDaMedia(), entrevista);
        CitarPorEliminacao(processo, entrevista.Id, 99m);

        Vermelhos(processo).Should().ContainSingle().Which.Codigo.Should().Be("etapa_com_nota_sem_nota_maxima");
    }

    [Fact(DisplayName = "As recusas acumulam: uma só pendência lista as três etapas, e o checklist reprova os três itens")]
    public void RecusasDeEtapasDiferentes_AcumulamNaMesmaPendencia()
    {
        EtapaProcesso semNotaMaxima = Etapa("Redação", 2, peso: 1m);
        EtapaProcesso semNotaComEscala = Etapa("Análise", 3, CaraterEtapa.Eliminatoria, notaMaxima: 10m);
        EtapaProcesso acimaDaMaxima = Etapa("Entrevista", 4, CaraterEtapa.Eliminatoria, notaMaxima: 10m);
        ProcessoSeletivo processo = ProcessoCom(EtapaDaMedia(), semNotaMaxima, semNotaComEscala, acimaDaMaxima);
        CitarPorEliminacao(processo, acimaDaMaxima.Id, 12m);

        Vermelhos(processo).Select(static i => i.Codigo).Should().BeEquivalentTo(
            "etapa_com_nota_sem_nota_maxima", "etapa_sem_nota_com_escala", "nota_minima_da_regra_acima_da_nota_maxima");

        DomainError? pendencia = processo.PendenciaDeConformidade();
        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("\"Redação\"").And.Contain("\"Análise\"").And.Contain("\"Entrevista\"");
    }

    [Fact(DisplayName = "A retificação recusa a etapa com nota sem nota máxima, pelo mesmo gate da publicação")]
    public void Retificar_EtapaComNotaSemNotaMaxima_Recusa()
    {
        EtapaProcesso etapa = EtapaDaMedia();
        ProcessoSeletivo processo = ProcessoCom(etapa);
        VersaoConfiguracao versaoAtual = Publicar(processo).Value!;

        typeof(EtapaProcesso).GetProperty(nameof(EtapaProcesso.NotaMaxima))!.SetValue(etapa, null);

        Result<VersaoConfiguracao> retificar = processo.Retificar(
            ProcessoConformeFactory.Dados(), versaoAtual, "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo,
            "teste", "Correção do prazo", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario,
            FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        retificar.IsFailure.Should().BeTrue();
        retificar.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }
}
