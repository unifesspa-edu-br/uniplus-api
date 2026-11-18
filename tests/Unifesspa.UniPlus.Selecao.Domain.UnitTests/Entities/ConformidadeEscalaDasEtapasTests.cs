namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A conferência da escala das etapas: a etapa com nota declara a nota máxima, e a que não tem nota e a
/// do ENEM não declaram escala nenhuma. É conferência, e não recusa de gravação, porque depende do que
/// o grafo diz sobre a etapa (média, nota mínima, eliminação, desempate).
/// </summary>
public sealed class ConformidadeEscalaDasEtapasTests
{
    private static readonly string HashFixo = new('a', 64);

    private static TipoEtapaSnapshot Tipo(bool notaDoEnem = false) =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), notaDoEnem ? "NOTA_ENEM" : "PROVA_OBJETIVA", "Prova", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: notaDoEnem).Value!;

    private static EtapaProcesso EtapaDaMedia(string nome = "Prova", int ordem = 1, decimal? notaMaxima = 10m) =>
        new ModeloDeEtapa(nome) { Ordem = ordem, FaseCodigo = "RESULTADO_FINAL", Peso = 1m, NotaMaxima = notaMaxima }.Criar();

    private static ProcessoSeletivo ProcessoCom(params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas(etapas).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static ConfiguracaoClassificacao ClassificacaoComEliminacao(Guid etapaRef, decimal notaMinima) =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ProcessoConformeFactory.Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
            RegraArredondamento = ProcessoConformeFactory.Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "c"),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ProcessoConformeFactory.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "d"),
            RegrasEliminacao = [RegraEliminacao.Criar(
                ProcessoConformeFactory.Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "e"),
                new ParametrosElimNotaMinimaEtapa(etapaRef, notaMinima)).Value!],
            EscalaNotaFinal = 100m,
        }).Value!;

    private static ConfiguracaoClassificacao ClassificacaoSemEliminacao() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ProcessoConformeFactory.Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
            RegraArredondamento = ProcessoConformeFactory.Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "c"),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ProcessoConformeFactory.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "d"),
            EscalaNotaFinal = 100m,
        }).Value!;

    /// <summary>
    /// A etapa de nota do ENEM só convive com classificação baseada em ENEM pela média, e a
    /// classificação é definida antes das etapas para a gravação aceitá-la.
    /// </summary>
    private static ProcessoSeletivo ProcessoComEtapaDoEnem(EtapaProcesso enem)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirClassificacao(ClassificacaoDoEnem()).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([EtapaDaMedia(), enem]).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static void CitarPorEliminacao(ProcessoSeletivo processo, Guid etapaId, decimal notaMinima) =>
        processo.DefinirClassificacao(ClassificacaoComEliminacao(etapaId, notaMinima))
            .IsSuccess.Should().BeTrue();

    private static void CitarPorDesempate(ProcessoSeletivo processo, Guid etapaId)
    {
        processo.DefinirClassificacao(ClassificacaoSemEliminacao()).IsSuccess.Should().BeTrue();
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(
                1,
                ProcessoConformeFactory.Regra(CriterioDesempateCodigo.MaiorNotaEtapa, "f"),
                new ParametrosDesempateMaiorNotaEtapa(etapaId)).Value!]).IsSuccess.Should().BeTrue();
    }

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        new CenarioDeCongelamento { Dados = ProcessoConformeFactory.Dados(), Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "teste" });

    private static IEnumerable<ItemConformidade> Vermelhos(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
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
            "media" => new ModeloDeEtapa("Alvo") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Peso = 1m, NotaMaxima = notaMaxima }.Criar(),
            "nota minima" => new ModeloDeEtapa("Alvo") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = notaMaxima, NotaMinima = 5m }.Criar(),
            _ => new ModeloDeEtapa("Alvo") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = notaMaxima }.Criar(),
        };

        ProcessoSeletivo processo = ProcessoCom(base1, alvo);
        DefinirUsoDaNota(processo, alvo.Id, razao);

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
            "PS Rascunho", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        EtapaProcesso etapaSemFaseDeclarada = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova", Carater: CaraterEtapa.Classificatoria, TipoEtapa: Tipo(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

        processo.DefinirEtapas([etapaSemFaseDeclarada]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Etapa sem nota com nota máxima ou com passo recusa a publicação; sem escala publica")]
    public void EtapaSemNotaComEscala_Recusa()
    {
        EtapaProcesso comNotaMaxima = new ModeloDeEtapa("Análise") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = 10m, Forma = null }.Criar();
        EtapaProcesso comPasso = new ModeloDeEtapa("Entrevista") { Ordem = 3, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = null, Passo = 0.5m, Forma = null }.Criar();
        ProcessoSeletivo processo = ProcessoCom(EtapaDaMedia(), comNotaMaxima, comPasso);

        Vermelhos(processo).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_sem_nota_com_escala"
                && i.Mensagem.Contains("\"Análise\"; \"Entrevista\"", StringComparison.Ordinal));
        Publicar(processo).IsFailure.Should().BeTrue();

        ProcessoSeletivo semEscala = ProcessoCom(EtapaDaMedia(), new ModeloDeEtapa("Análise") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = null, Forma = null }.Criar());
        Vermelhos(semEscala).Should().BeEmpty();
        Publicar(semEscala).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Etapa de nota do ENEM com nota máxima ou com passo recusa a publicação")]
    [InlineData(true)]
    [InlineData(false)]
    public void EtapaDoEnemComEscala_Recusa(bool comNotaMaxima)
    {
        EtapaProcesso enem = new ModeloDeEtapa("Nota do ENEM") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Peso = 1m, NotaMaxima = comNotaMaxima ? 1000m : null, Passo = comNotaMaxima ? null : 1m, NotaDoEnem = true, Forma = null }.Criar();
        ProcessoSeletivo processo = ProcessoComEtapaDoEnem(enem);

        Vermelhos(processo).Should().ContainSingle().Which.Codigo.Should().Be("etapa_do_enem_com_escala");
    }

    [Fact(DisplayName = "Etapa de nota do ENEM sem escala publica sem nota máxima")]
    public void EtapaDoEnemSemEscala_NaoExigeNotaMaxima()
    {
        ProcessoSeletivo processo = ProcessoComEtapaDoEnem(new ModeloDeEtapa("Nota do ENEM") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Peso = 1m, NotaMaxima = null, NotaDoEnem = true, Forma = null }.Criar());

        Vermelhos(processo).Should().BeEmpty();
    }

    [Fact(DisplayName = "Nota mínima da regra acima da nota máxima da etapa recusa; igual aceita")]
    public void NotaMinimaDaRegraAcimaDaNotaMaxima_Recusa()
    {
        EtapaProcesso entrevista = new ModeloDeEtapa("Entrevista") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = 10m }.Criar();
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
        EtapaProcesso entrevista = new ModeloDeEtapa("Entrevista") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = null }.Criar();
        ProcessoSeletivo processo = ProcessoCom(EtapaDaMedia(), entrevista);
        CitarPorEliminacao(processo, entrevista.Id, 99m);

        Vermelhos(processo).Should().ContainSingle().Which.Codigo.Should().Be("etapa_com_nota_sem_nota_maxima");
    }

    private static readonly string[] ItensDasTresEtapasRecusadas =
        ["etapa_com_nota_sem_nota_maxima", "etapa_sem_nota_com_escala", "nota_minima_da_regra_acima_da_nota_maxima"];

    private static ProcessoSeletivo ProcessoComTresEtapasRecusadas()
    {
        EtapaProcesso semNotaMaxima = new ModeloDeEtapa("Redação") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Peso = 1m, NotaMaxima = null }.Criar();
        EtapaProcesso semNotaComEscala = new ModeloDeEtapa("Análise") { Ordem = 3, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = 10m, Forma = null }.Criar();
        EtapaProcesso acimaDaMaxima = new ModeloDeEtapa("Entrevista") { Ordem = 4, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = 10m }.Criar();
        ProcessoSeletivo processo = ProcessoCom(EtapaDaMedia(), semNotaMaxima, semNotaComEscala, acimaDaMaxima);
        CitarPorEliminacao(processo, acimaDaMaxima.Id, 12m);

        return processo;
    }

    [Fact(DisplayName = "As recusas acumulam: cada item reprovado é uma causa, e o checklist reprova os três itens")]
    public void RecusasDeEtapasDiferentes_AcumulamUmaCausaPorItem()
    {
        ProcessoSeletivo processo = ProcessoComTresEtapasRecusadas();

        Vermelhos(processo).Select(static i => i.Codigo).Should().BeEquivalentTo(ItensDasTresEtapasRecusadas);

        IReadOnlyList<FieldError> causas = CausasDeConformidadeDeTeste.De(processo);
        causas.Select(static causa => causa.Origem).Should().BeEquivalentTo(ItensDasTresEtapasRecusadas);
        causas.Should().OnlyContain(static causa => causa.Field == "etapas");
        causas.Single(static causa => causa.Origem == "etapa_com_nota_sem_nota_maxima").Error.Message.Should().Contain("\"Redação\"");
        causas.Single(static causa => causa.Origem == "etapa_sem_nota_com_escala").Error.Message.Should().Contain("\"Análise\"");
        causas.Single(static causa => causa.Origem == "nota_minima_da_regra_acima_da_nota_maxima").Error.Message.Should().Contain("\"Entrevista\"");
    }

    [Fact(DisplayName = "A retificação recusa a etapa com nota sem nota máxima, pela mesma conferência da publicação")]
    public void Retificar_EtapaComNotaSemNotaMaxima_Recusa()
    {
        EtapaProcesso etapa = EtapaDaMedia();
        ProcessoSeletivo processo = ProcessoCom(etapa);
        VersaoConfiguracao versaoAtual = Publicar(processo).Value!;

        typeof(EtapaProcesso).GetProperty(nameof(EtapaProcesso.NotaMaxima))!.SetValue(etapa, null);

        Result<VersaoConfiguracao> retificar = processo.Retificar(
            new CenarioDeCongelamento { Dados = ProcessoConformeFactory.Dados(), Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "teste" }, versaoAtual, "Correção do prazo");

        retificar.IsFailure.Should().BeTrue();
        retificar.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }

    private static ConfiguracaoClassificacao ClassificacaoDoEnem() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ProcessoConformeFactory.Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
            RegraArredondamento = ProcessoConformeFactory.Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "c"),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ProcessoConformeFactory.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "d"),
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
            EscalaNotaFinal = 100m,
        }).Value!;

    private static void DefinirUsoDaNota(ProcessoSeletivo processo, Guid alvoId, string razao)
    {
        if (razao == "eliminacao")
        {
            CitarPorEliminacao(processo, alvoId, 5m);
        }
        else if (razao == "desempate")
        {
            CitarPorDesempate(processo, alvoId);
        }
        else
        {
            processo.DefinirClassificacao(ClassificacaoSemEliminacao()).IsSuccess.Should().BeTrue();
        }
    }

}
