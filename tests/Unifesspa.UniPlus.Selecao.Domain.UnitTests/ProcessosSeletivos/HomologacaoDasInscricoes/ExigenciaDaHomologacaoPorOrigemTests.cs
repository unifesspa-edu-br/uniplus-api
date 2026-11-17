namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

/// <summary>
/// O que cada origem de candidatos exige da homologação das inscrições. A fase que homologa é a que declara a regra: o código
/// canônico dela nunca é lido, e o que a leitura do documento recusou deixa a regra sem julgar.
/// </summary>
public sealed class ExigenciaDaHomologacaoPorOrigemTests
{
    private const string Resultado = "RESULTADO_HOMOLOGACAO";
    private const int ItemDaFase = 0;
    private const int ItemDoCiclo = 1;
    private const int ItemDoInep = 2;

    private static RegraDeHomologacaoDasInscricoes RegraComInep(string resultado = Resultado) =>
        RegraDeHomologacaoDasInscricoes.Declarar(resultado, [Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP"))]);

    private static RegraDeHomologacaoDasInscricoes RegraVazia(string? resultado = Resultado) =>
        RegraDeHomologacaoDasInscricoes.Declarar(resultado, []);

    private static ModeloDeFase Coleta(int ordem) => new("INSCRICAO", ordem) { ColetaInscricao = true };

    private static ModeloDeFase Homologa(int ordem, RegraDeHomologacaoDasInscricoes regra, string codigo = "HOMOLOGACAO_INSCRICOES") =>
        new ModeloDeFase(codigo, ordem).Homologando(regra);

    // O preliminar e o definitivo do resultado, com a janela de recurso ancorada no preliminar.
    private static ModeloDeFase Completa(int ordem, string resultado = Resultado)
    {
        ProdutoDaFase preliminar = ModeloDeFase.Preliminar(resultado);

        ModeloDeFase fase = Homologa(ordem, RegraComInep(resultado)).Publicando(preliminar, ModeloDeFase.Definitivo(resultado));

        return fase with { RegraRecurso = ModeloDeFase.RecursoAncoradoEm(preliminar.Id) };
    }

    private static CronogramaDoProcesso Cronograma(params ModeloDeFase[] fases) =>
        CronogramaDoProcesso.Do([.. fases.Select(static fase => fase.Adotada())]);

    private static CronogramaDoProcesso CronogramaLido(OrigemNoDocumento origem, params ModeloDeFase[] fases) =>
        CronogramaDoProcesso.Do([.. fases.Select(static fase => fase.Adotada())], origem);

    private static EtapasDoProcesso SemEtapas { get; } = new([]);

    private static EtapasDoProcesso ComNotaDoEnem { get; } = new([new ModeloDeEtapa("Nota do ENEM").DoEnem().Declarar()]);

    private static (string? Campo, string Codigo)[] Causas(OrigemCandidatos origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        CenarioDeProcesso.Causas(ExigenciaDaHomologacaoPorOrigem.Causas(origem, cronograma, etapas));

    private static bool[] Itens(OrigemCandidatos origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        [.. ExigenciaDaHomologacaoPorOrigem.Itens(origem, cronograma, etapas).Select(static item => item.Ok)];

    [Fact(DisplayName = "A origem ausente na leitura suspende as exigências que dependem dela e mantém os itens pendentes")]
    public void OrigemAusente_NaoPresumeInscricaoPropriaNemDispensaOCiclo()
    {
        CronogramaDoProcesso cronograma = Cronograma(Coleta(1));

        Causas(OrigemCandidatos.Nenhuma, cronograma, SemEtapas).Should().BeEmpty();
        Itens(OrigemCandidatos.Nenhuma, cronograma, SemEtapas).Should().Equal(false, false, true);
    }

    [Fact(DisplayName = "Inscrição própria sem fase que tem a regra é recusada em fases, mesmo com a fase de coleta")]
    public void InscricaoPropria_SemFaseComARegra_Recusa()
    {
        CronogramaDoProcesso cronograma = Cronograma(Coleta(1), new ModeloDeFase("CLASSIFICACAO", 2));

        Causas(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas)
            .Should().Equal(("fases", ProcessoSeletivoErrorCodes.InscricaoPropriaSemHomologacaoDasInscricoes));
    }

    [Fact(DisplayName = "Inscrição própria que usa a nota do ENEM e não tem fase com a regra sai com o código da importação de exame")]
    public void ImportacaoDeExame_SemFaseComARegra_RecusaComOCodigoDoExame()
    {
        Causas(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1)), ComNotaDoEnem)
            .Should().Equal(("fases", ProcessoSeletivoErrorCodes.ImportacaoDeExameSemHomologacaoDasInscricoes));
    }

    [Fact(DisplayName = "Importação externa sem fase com a regra é recusada sem exigir fase de coleta")]
    public void ImportacaoExterna_SemFaseComARegra_RecusaSemExigirColeta()
    {
        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(new ModeloDeFase("CLASSIFICACAO", 1)), SemEtapas)
            .Should().Equal(("fases", ProcessoSeletivoErrorCodes.ImportacaoExternaSemHomologacaoDasInscricoes));
    }

    [Theory(DisplayName = "A fase que declara a regra satisfaz a exigência, qualquer que seja o código canônico dela")]
    [InlineData("HOMOLOGACAO_INSCRICOES")]
    [InlineData("CLASSIFICACAO")]
    [InlineData("ENSALAMENTO")]
    public void InscricaoPropria_FaseComARegra_Satisfaz(string codigo)
    {
        CronogramaDoProcesso cronograma = Cronograma(Coleta(1), Homologa(2, RegraComInep(), codigo));

        Causas(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase HOMOLOGACAO_RESULTADO_FINAL sem a regra não satisfaz: o que vale é a regra, e não o nome")]
    public void InscricaoPropria_FaseDoResultadoFinalSemARegra_NaoSatisfaz()
    {
        CronogramaDoProcesso cronograma = Cronograma(Coleta(1), new ModeloDeFase("HOMOLOGACAO_RESULTADO_FINAL", 2));

        Causas(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas)
            .Should().Equal(("fases", ProcessoSeletivoErrorCodes.InscricaoPropriaSemHomologacaoDasInscricoes));
    }

    [Fact(DisplayName = "A fase que tem a regra sem declarar o tipo de decisão satisfaz: a falta do tipo é conferida na cobertura dos motivos")]
    public void InscricaoPropria_FaseComARegraSemOTipo_Satisfaz()
    {
        ModeloDeFase semOTipo = Homologa(2, RegraComInep()) with { TiposDeDecisao = null };

        Causas(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1), semOTipo), SemEtapas).Should().BeEmpty();
    }

    [Fact(DisplayName = "Importação externa com a fase, o preliminar, o definitivo e a janela de recurso é aceita sem fase de coleta")]
    public void ImportacaoExterna_Completa_Aceita()
    {
        CronogramaDoProcesso cronograma = Cronograma(Completa(1));

        Causas(OrigemCandidatos.ImportacaoExterna, cronograma, SemEtapas).Should().BeEmpty();
        Itens(OrigemCandidatos.ImportacaoExterna, cronograma, SemEtapas).Should().Equal(true, true, true);
    }

    [Fact(DisplayName = "Importação externa sem a janela de recurso entre o preliminar e o definitivo é recusada na regra da fase")]
    public void ImportacaoExterna_SemJanelaDeRecurso_Recusa()
    {
        ModeloDeFase semJanela = Homologa(1, RegraComInep()).Publicando(ModeloDeFase.Preliminar(Resultado), ModeloDeFase.Definitivo(Resultado));

        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(semJanela), SemEtapas)
            .Should().Equal(("fases[0].regraDeHomologacao", ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso));
    }

    [Theory(DisplayName = "Importação externa sem o preliminar ou sem o definitivo do resultado apontado é recusada")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ImportacaoExterna_SemPreliminarOuDefinitivo_Recusa(bool temPreliminar, bool temDefinitivo)
    {
        ModeloDeFase fase = Homologa(1, RegraComInep()).Publicando(ProdutosDoResultado(temPreliminar, temDefinitivo));

        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(fase), SemEtapas)
            .Should().Equal(("fases[0].regraDeHomologacao", ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso));
    }

    private static ProdutoDaFase[] ProdutosDoResultado(bool temPreliminar, bool temDefinitivo) =>
    [
        .. temPreliminar ? [ModeloDeFase.Preliminar(Resultado)] : Array.Empty<ProdutoDaFase>(),
        .. temDefinitivo ? [ModeloDeFase.Definitivo(Resultado)] : Array.Empty<ProdutoDaFase>(),
    ];

    [Fact(DisplayName = "O ciclo de outro resultado da fase não vale: o preliminar e o definitivo precisam ser os do resultado apontado")]
    public void ImportacaoExterna_CicloForaDoResultadoApontado_Recusa()
    {
        ProdutoDaFase preliminar = ModeloDeFase.Preliminar("RESULTADO_B");
        ModeloDeFase publicando = Homologa(1, RegraComInep()).Publicando(preliminar, ModeloDeFase.Definitivo("RESULTADO_B"), ModeloDeFase.Preliminar(Resultado));
        ModeloDeFase fase = publicando with { RegraRecurso = ModeloDeFase.RecursoAncoradoEm(preliminar.Id) };

        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(fase), SemEtapas)
            .Should().Equal(("fases[0].regraDeHomologacao", ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso));
    }

    [Fact(DisplayName = "A janela ancorada no preliminar de outro resultado, mesmo com o ciclo do apontado fechado, não basta")]
    public void ImportacaoExterna_JanelaAncoradaEmOutroPreliminar_Recusa()
    {
        ProdutoDaFase outro = ModeloDeFase.Preliminar("RESULTADO_B");
        ModeloDeFase publicando = Homologa(1, RegraComInep()).Publicando(outro, ModeloDeFase.Preliminar(Resultado), ModeloDeFase.Definitivo(Resultado));
        ModeloDeFase fase = publicando with { RegraRecurso = ModeloDeFase.RecursoAncoradoEm(outro.Id) };

        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(fase), SemEtapas)
            .Should().Equal(("fases[0].regraDeHomologacao", ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso));
    }

    [Fact(DisplayName = "O ciclo só se exige da importação externa: a inscrição própria sem janela de recurso é aceita")]
    public void InscricaoPropria_SemJanelaDeRecurso_NaoExigeOCiclo()
    {
        CronogramaDoProcesso cronograma = Cronograma(Coleta(1), Homologa(2, RegraComInep()));

        Causas(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas).Should().BeEmpty();
        Itens(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas).Should().Equal(true, true, true);
    }

    [Fact(DisplayName = "Com várias fases com a regra, basta uma completa: a ordem das fases não muda o veredicto")]
    public void ImportacaoExterna_VariasFases_BastaUmaCompleta()
    {
        ModeloDeFase incompleta = Homologa(1, RegraComInep()).Publicando(ModeloDeFase.Preliminar(Resultado));

        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(incompleta, Completa(2)), SemEtapas).Should().BeEmpty();
        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(Completa(2), incompleta), SemEtapas).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com várias fases com a regra e nenhuma completa, a recusa é a da primeira fase na ordem do cronograma")]
    public void ImportacaoExterna_NenhumaCompleta_RecusaPelaPrimeiraNaOrdem()
    {
        ModeloDeFase primeira = Homologa(1, RegraComInep()).Publicando(ModeloDeFase.Preliminar(Resultado));
        ModeloDeFase segunda = Homologa(2, RegraComInep()).Publicando(ModeloDeFase.Preliminar(Resultado), ModeloDeFase.Definitivo(Resultado));

        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(primeira, segunda), SemEtapas)
            .Should().Equal(("fases[0].regraDeHomologacao", ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso));
    }

    [Fact(DisplayName = "A fase cujo resultado apontado está ausente não é julgada no ciclo: a recusa dela é a da declaração, e o item fica pendente")]
    public void ImportacaoExterna_ResultadoApontadoAusente_NaoJulga()
    {
        CronogramaDoProcesso cronograma = Cronograma(Homologa(1, RegraVazia(resultado: null)));

        Causas(OrigemCandidatos.ImportacaoExterna, cronograma, SemEtapas).Should().BeEmpty();
        Itens(OrigemCandidatos.ImportacaoExterna, cronograma, SemEtapas)[ItemDoCiclo].Should().BeFalse();
    }

    [Fact(DisplayName = "A fase cujo resultado apontado não é produto dela não é julgada no ciclo, e a fase incompleta ao lado é")]
    public void ImportacaoExterna_ResultadoApontadoQueNaoEProduto_NaoJulgaEJulgaAsOutras()
    {
        ModeloDeFase foraDoProduto = Homologa(1, RegraComInep("RESULTADO_QUE_A_FASE_NAO_PRODUZ")).Publicando(ModeloDeFase.Preliminar(Resultado), ModeloDeFase.Definitivo(Resultado));
        ModeloDeFase incompleta = Homologa(2, RegraComInep()).Publicando(ModeloDeFase.Preliminar(Resultado));

        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(foraDoProduto), SemEtapas).Should().BeEmpty();
        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(foraDoProduto, incompleta), SemEtapas)
            .Should().Equal(("fases[1].regraDeHomologacao", ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso));
    }

    [Fact(DisplayName = "A fase cujos produtos a leitura recusou em parte não é julgada no ciclo, e o item fica pendente")]
    public void ImportacaoExterna_ProdutosComParteRecusada_NaoJulga()
    {
        ModeloDeFase fase = Homologa(1, RegraComInep()).Publicando(ModeloDeFase.Preliminar(Resultado));
        CronogramaDoProcesso cronograma = CronogramaLido(ComPartesRecusadas("fases[0].produtos"), fase);

        Causas(OrigemCandidatos.ImportacaoExterna, cronograma, SemEtapas).Should().BeEmpty();
        Itens(OrigemCandidatos.ImportacaoExterna, cronograma, SemEtapas)[ItemDoCiclo].Should().BeFalse();
    }

    [Fact(DisplayName = "Os produtos aceitos que já fecham o ciclo bastam, ainda que a leitura tenha recusado outros")]
    public void ImportacaoExterna_ProdutosAceitosFecham_AindaQueHajaParteRecusada()
    {
        CronogramaDoProcesso cronograma = CronogramaLido(ComPartesRecusadas("fases[0].produtos"), Completa(1));

        Causas(OrigemCandidatos.ImportacaoExterna, cronograma, SemEtapas).Should().BeEmpty();
        Itens(OrigemCandidatos.ImportacaoExterna, cronograma, SemEtapas)[ItemDoCiclo].Should().BeTrue();
    }

    [Fact(DisplayName = "A fase recusada inteira pela leitura impede concluir que nenhuma fase tem o ciclo, e o item fica pendente")]
    public void ImportacaoExterna_FaseRecusadaInteira_NaoJulgaOCiclo()
    {
        ModeloDeFase incompleta = Homologa(1, RegraComInep()).Publicando(ModeloDeFase.Preliminar(Resultado));
        CronogramaDoProcesso cronograma = CronogramaLido(ComPartesRecusadas("fases"), incompleta);

        Causas(OrigemCandidatos.ImportacaoExterna, cronograma, SemEtapas).Should().BeEmpty();
        Itens(OrigemCandidatos.ImportacaoExterna, cronograma, SemEtapas)[ItemDoCiclo].Should().BeFalse();
    }

    [Fact(DisplayName = "A leitura que recusou fase inteira e deixou nenhuma com a regra não recusa a fase ausente, e o item fica pendente")]
    public void FaseRecusadaInteira_NenhumaAceitaComARegra_NaoJulgaAFaseAusente()
    {
        CronogramaDoProcesso cronograma = CronogramaLido(ComPartesRecusadas("fases"), Coleta(1));

        Causas(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas).Should().BeEmpty();
        Itens(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas)[ItemDaFase].Should().BeFalse();
    }

    [Fact(DisplayName = "A leitura que recusou fase inteira não impede aceitar a regra que alguma fase aceita já tem")]
    public void FaseRecusadaInteira_OutraFaseTemARegra_Satisfaz()
    {
        CronogramaDoProcesso cronograma = CronogramaLido(ComPartesRecusadas("fases"), Coleta(1), Homologa(2, RegraComInep()));

        Itens(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas)[ItemDaFase].Should().BeTrue();
    }

    [Fact(DisplayName = "Com nota do ENEM e nenhuma condição de retorno do INEP, a regra é recusada em alternativas da fase")]
    public void NotaDoEnem_SemConfirmacaoDoInep_Recusa()
    {
        ModeloDeFase comInep = Homologa(1, RegraComInep());
        ModeloDeFase semInep = Homologa(2, RegraVazia());

        Causas(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1), comInep, semInep), ComNotaDoEnem)
            .Should().Equal(("fases[2].regraDeHomologacao.alternativas", HomologacaoDasInscricoesErrorCodes.ConfirmacaoDoInepAusente));
    }

    [Fact(DisplayName = "Com nota do ENEM, a confirmação do INEP basta, com ou sem enquadramento na alternativa")]
    public void NotaDoEnem_InepComOuSemEnquadramento_Aceita()
    {
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar(
            Resultado, [Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP")), Alternativa(ConfirmacaoDoInepSoParaAmpla(2025, "SEM_RESULTADO_INEP"))]);
        RegraDeHomologacaoDasInscricoes comEnquadramentoNasDuasAlternativas = RegraDeHomologacaoDasInscricoes.Declarar(Resultado, [Alternativa(ConfirmacaoDoInepSoParaAmpla(2025, "SEM_RESULTADO_INEP")), Alternativa(ConfirmacaoDoInepSoParaAmpla(2026, "SEM_RESULTADO_INEP"))]);

        Causas(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1), Homologa(2, regra)), ComNotaDoEnem).Should().BeEmpty();
        Causas(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1), Homologa(2, comEnquadramentoNasDuasAlternativas)), ComNotaDoEnem).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com nota do ENEM, a regra vazia é recusada por não ter a confirmação do INEP")]
    public void NotaDoEnem_RegraVazia_Recusa()
    {
        CronogramaDoProcesso cronograma = Cronograma(Coleta(1), Homologa(2, RegraVazia()));

        Causas(OrigemCandidatos.InscricaoPropria, cronograma, ComNotaDoEnem)
            .Should().Equal(("fases[1].regraDeHomologacao.alternativas", HomologacaoDasInscricoesErrorCodes.ConfirmacaoDoInepAusente));
    }

    [Fact(DisplayName = "Sem nota do ENEM o processo não é obrigado a ter a confirmação do INEP, nem com a regra vazia")]
    public void SemNotaDoEnem_NaoExigeOInep()
    {
        CronogramaDoProcesso cronograma = Cronograma(Coleta(1), Homologa(2, RegraVazia()));

        Causas(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas).Should().BeEmpty();
        Itens(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas).Should().Equal(true, true, true);
    }

    [Fact(DisplayName = "A alternativa ou a condição recusada pela leitura deixa a fase sem julgar o INEP, e o item fica pendente")]
    public void NotaDoEnem_AlternativasComParteRecusada_NaoJulgaOInep()
    {
        CronogramaDoProcesso cronograma = CronogramaLido(ComPartesRecusadas("fases[1].regraDeHomologacao"), Coleta(1), new ModeloDeFase("HOMOLOGACAO_INSCRICOES", 2));

        Causas(OrigemCandidatos.InscricaoPropria, cronograma, ComNotaDoEnem).Should().BeEmpty();
        Itens(OrigemCandidatos.InscricaoPropria, cronograma, ComNotaDoEnem)[ItemDoInep].Should().BeFalse();
    }

    [Fact(DisplayName = "A regra descartada inteira deixa o painel pendente mesmo que uma condição fosse aceita antes")]
    public void NotaDoEnem_RegraDescartadaInteira_NaoAprova()
    {
        CronogramaDoProcesso cronograma = CronogramaLido(ComPartesRecusadas("fases[1].regraDeHomologacao"), Coleta(1), new ModeloDeFase("HOMOLOGACAO_INSCRICOES", 2));

        Causas(OrigemCandidatos.ImportacaoExterna, cronograma, ComNotaDoEnem).Should().BeEmpty();
        Itens(OrigemCandidatos.ImportacaoExterna, cronograma, ComNotaDoEnem).Should().Equal(false, false, false);
    }

    [Fact(DisplayName = "A etapa recusada pela leitura, sem etapa aceita com a nota do ENEM, deixa a fase ausente e o INEP sem julgar")]
    public void EtapaRecusadaInteira_SemEtapaAceitaComEnem_NaoJulgaAFaseAusenteNemOInep()
    {
        EtapasDoProcesso etapas = new([], leituraRecusouEtapa: true);

        Causas(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1)), etapas).Should().BeEmpty();
        Itens(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1)), etapas)[ItemDaFase].Should().BeFalse();
        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(Coleta(1)), etapas)
            .Should().Equal(("fases", ProcessoSeletivoErrorCodes.ImportacaoExternaSemHomologacaoDasInscricoes));
        Itens(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1), Homologa(2, RegraVazia())), etapas)[ItemDoInep].Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa recusada pela leitura não impede julgar o INEP quando uma etapa aceita já usa a nota do ENEM")]
    public void EtapaRecusadaInteira_ComEtapaAceitaComEnem_JulgaOInep()
    {
        EtapasDoProcesso etapas = new([new ModeloDeEtapa("Nota do ENEM").DoEnem().Declarar()], leituraRecusouEtapa: true);

        Causas(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1), Homologa(2, RegraVazia())), etapas)
            .Should().Equal(("fases[1].regraDeHomologacao.alternativas", HomologacaoDasInscricoesErrorCodes.ConfirmacaoDoInepAusente));
        Causas(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1)), etapas)
            .Should().Equal(("fases", ProcessoSeletivoErrorCodes.ImportacaoDeExameSemHomologacaoDasInscricoes));
    }

    [Fact(DisplayName = "As causas saem na ordem do ciclo da importação externa e depois da confirmação do INEP")]
    public void Causas_SaemNaOrdemDoCicloEDoInep()
    {
        ModeloDeFase incompletaSemInep = Homologa(1, RegraVazia()).Publicando(ModeloDeFase.Preliminar(Resultado));

        Causas(OrigemCandidatos.ImportacaoExterna, Cronograma(incompletaSemInep), ComNotaDoEnem).Should().Equal(
            ("fases[0].regraDeHomologacao", ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso),
            ("fases[0].regraDeHomologacao.alternativas", HomologacaoDasInscricoesErrorCodes.ConfirmacaoDoInepAusente));
    }

    [Fact(DisplayName = "Os itens do painel saem na ordem da fase, do ciclo e do INEP, com os códigos de item que a conferência deriva")]
    public void Itens_SaemNaOrdemDaFaseDoCicloEDoInep()
    {
        IReadOnlyList<ItemConformidade> itens = ExigenciaDaHomologacaoPorOrigem.Itens(OrigemCandidatos.InscricaoPropria, Cronograma(Coleta(1)), SemEtapas);

        itens.Select(static item => item.Codigo).Should().Equal(
            "cronograma_homologacao_das_inscricoes_exigida",
            "cronograma_homologacao_das_inscricoes_com_ciclo_recursal",
            "cronograma_homologacao_das_inscricoes_com_confirmacao_do_inep");
        itens.Select(static item => item.Ok).Should().Equal(false, true, true);
        itens.Should().OnlyContain(static item => item.Dimensao == DimensaoConformidade.Cronograma);
    }
}
