namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.RecursosParaQuemJulga;

/// <summary>
/// As bancas que cada origem de candidatos exige: a de homologação das inscrições em cada fase que homologa, e as de importação
/// das notas do INEP e de candidatos. Quem decide é a cópia do tipo que a banca guarda, nunca o código do tipo, e o que a leitura
/// do documento recusou deixa a exigência sem julgar.
/// </summary>
public sealed class ExigenciaDasBancasDaHomologacaoTests
{
    private const string Fase = "HOMOLOGACAO_DAS_INSCRICOES";
    private const string CodigoDaHomologacao = "ProcessoSeletivo.HomologacaoDasInscricoesSemBanca";
    private const string CodigoDasNotas = "ProcessoSeletivo.NotaDoEnemSemBancaDeImportacaoDasNotas";
    private const string CodigoDosCandidatos = "ProcessoSeletivo.ImportacaoExternaSemBancaDeImportacaoDeCandidatos";

    private static readonly RegraDeHomologacaoDasInscricoes Regra = RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO", []);

    private static ModeloDeFase Homologa(int ordem, string codigo, params BancaRequerida[] bancas) =>
        new ModeloDeFase(codigo, ordem).Homologando(Regra) with { Bancas = bancas };

    private static ModeloDeFase Homologa(params BancaRequerida[] bancas) => Homologa(1, Fase, bancas);

    private static BancaRequerida DeHomologacao() => BancasDeTeste.DaHomologacaoDasInscricoesDaFase();

    private static BancaRequerida DeImportacaoDasNotas() => BancasDeTeste.DaFase(TipoDeBanca.ImportacaoDasNotasDoInep);

    private static BancaRequerida DeImportacaoDeCandidatos() => BancasDeTeste.DaFase(TipoDeBanca.ImportacaoDeCandidatos);

    private static ModeloDeEtapa NotaDoEnem(params BancaDaEtapa[] bancas) =>
        new ModeloDeEtapa("Nota do ENEM").DoEnem() with { FaseCodigo = Fase, Bancas = bancas };

    private static EtapasDoProcesso SemEtapas { get; } = EtapasDoProcesso.Das([]);

    private static EtapasDoProcesso ComNotaDoEnem { get; } = Etapas(NotaDoEnem());

    private static (string? Campo, string Codigo)[] Causas(ExigenciaDasBancasDaHomologacao.Veredicto veredicto) => CenarioDeProcesso.Causas(veredicto.Causas);

    private static void DeveEstarAtendido(ExigenciaDasBancasDaHomologacao.Veredicto veredicto)
    {
        veredicto.Causas.Should().BeEmpty();
        veredicto.Indeterminado.Should().BeFalse();
    }

    private static void DeveFicarSemJulgar(ExigenciaDasBancasDaHomologacao.Veredicto veredicto)
    {
        veredicto.Causas.Should().BeEmpty();
        veredicto.Indeterminado.Should().BeTrue();
    }

    private static ExigenciaDasBancasDaHomologacao.Veredicto Homologacao(OrigemCandidatos origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ExigenciaDasBancasDaHomologacao.BancaDeHomologacaoDasInscricoes(origem, cronograma, etapas);

    private static ExigenciaDasBancasDaHomologacao.Veredicto Notas(OrigemCandidatos origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ExigenciaDasBancasDaHomologacao.BancaDeImportacaoDasNotasDoInep(origem, cronograma, etapas);

    private static ExigenciaDasBancasDaHomologacao.Veredicto Candidatos(OrigemCandidatos origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ExigenciaDasBancasDaHomologacao.BancaDeImportacaoDeCandidatos(origem, cronograma, etapas);

    [Theory(DisplayName = "A fase que homologa sem banca de homologação é recusada em qualquer origem, no campo das bancas da fase e com o detalhe da fase")]
    [InlineData(OrigemCandidatos.InscricaoPropria)]
    [InlineData(OrigemCandidatos.ImportacaoExterna)]
    public void FaseQueHomologa_SemBanca_Recusa(OrigemCandidatos origem)
    {
        ExigenciaDasBancasDaHomologacao.Veredicto veredicto = Homologacao(origem, Fases(Homologa()), SemEtapas);

        veredicto.Indeterminado.Should().BeFalse();
        veredicto.Causas.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "fases[0].bancasRequeridas",
            Error = new
            {
                Code = CodigoDaHomologacao,
                Message = "A fase 'HOMOLOGACAO_DAS_INSCRICOES' homologa as inscrições, e nem ela nem as etapas dela preveem banca que julgue a homologação das inscrições. Toda origem de candidatos exige essa banca. Acrescente-a à fase ou a uma etapa dela.",
            },
        });
    }

    [Fact(DisplayName = "A banca de homologação na fase, ou numa etapa dela, atende a exigência")]
    public void BancaDeHomologacao_NaFaseOuNaEtapaDaFase_Atende()
    {
        BancaDaEtapa daEtapa = BancasDeTeste.DaEtapa(TipoDeBanca.HomologacaoDasInscricoes);
        DeveEstarAtendido(Homologacao(OrigemCandidatos.InscricaoPropria, Fases(Homologa(DeHomologacao())), SemEtapas));
        DeveEstarAtendido(Homologacao(OrigemCandidatos.InscricaoPropria, Fases(Homologa()), Etapas(EtapaNaFase(Fase) with { Bancas = [daEtapa] })));
    }

    [Fact(DisplayName = "A banca de homologação na etapa de outra fase não atende a fase que homologa")]
    public void BancaDeHomologacao_NaEtapaDeOutraFase_NaoAtende()
    {
        ModeloDeEtapa deOutraFase = EtapaNaFase("AVALIACAO") with { Bancas = [BancasDeTeste.DaEtapa(TipoDeBanca.HomologacaoDasInscricoes)] };

        Causas(Homologacao(OrigemCandidatos.InscricaoPropria, Fases(Homologa(), new ModeloDeFase("AVALIACAO", 2)), Etapas(deOutraFase)))
            .Should().Equal(("fases[0].bancasRequeridas", CodigoDaHomologacao));
    }

    [Fact(DisplayName = "Cada fase que homologa é cobrada, na sua posição, e a que tem a banca não é recusada")]
    public void DuasFasesQueHomologam_CadaUmaECobrada()
    {
        ModeloDeFase semBanca = Homologa(3, "HOMOLOGACAO_FINAL");
        ModeloDeFase comBanca = Homologa(2, "HOMOLOGACAO_DAS_ISENCOES", DeHomologacao());
        ModeloDeFase primeiraSemBanca = Homologa(1, Fase);

        Causas(Homologacao(OrigemCandidatos.ImportacaoExterna, Fases(primeiraSemBanca, comBanca, semBanca), SemEtapas)).Should().Equal(
            ("fases[0].bancasRequeridas", CodigoDaHomologacao),
            ("fases[2].bancasRequeridas", CodigoDaHomologacao));
    }

    [Fact(DisplayName = "A banca reconhecida é a que a cópia do tipo diz que avalia o mérito e julga a homologação, e não a que tem o código do tipo")]
    public void BancaDeHomologacao_EReconhecidaPelaCopiaDoTipo_NaoPeloCodigo()
    {
        TipoDeBancaCopiado codigoDeHomologacaoSemPapel = TiposDeBancaCopiadosDeTeste.Com(TipoDeBanca.HomologacaoDasInscricoes, PapelDaBanca.JulgaAPrimeiraInstancia);
        TipoDeBancaCopiado codigoDeHomologacaoDeOutraDecisao = Avaliadora(TipoDeBanca.HomologacaoDasInscricoes, false, TipoMotivoDeDecisao.NotaDaEtapa);
        TipoDeBancaCopiado outroCodigoQueJulgaAHomologacao = Avaliadora(TipoDeBanca.Entrevista, false, TipoMotivoDeDecisao.HomologacaoDasInscricoes);

        Causas(Homologacao(OrigemCandidatos.InscricaoPropria, Fases(Homologa(NaFase(codigoDeHomologacaoSemPapel))), SemEtapas))
            .Should().ContainSingle();
        Causas(Homologacao(OrigemCandidatos.InscricaoPropria, Fases(Homologa(NaFase(codigoDeHomologacaoDeOutraDecisao))), SemEtapas))
            .Should().ContainSingle();
        DeveEstarAtendido(Homologacao(OrigemCandidatos.InscricaoPropria, Fases(Homologa(NaFase(outroCodigoQueJulgaAHomologacao))), SemEtapas));
    }

    [Fact(DisplayName = "A banca de homologação atende ao lado de outras bancas, e a de importação não a substitui")]
    public void BancaDeHomologacao_ComOutrasBancas_Atende()
    {
        DeveEstarAtendido(Homologacao(OrigemCandidatos.ImportacaoExterna, Fases(Homologa(DeImportacaoDeCandidatos(), DeHomologacao(), BancasDeTeste.DeRecursosDaFase())), SemEtapas));
        Causas(Homologacao(OrigemCandidatos.ImportacaoExterna, Fases(Homologa(DeImportacaoDeCandidatos(), BancasDeTeste.DeRecursosDaFase())), SemEtapas))
            .Should().ContainSingle();
    }

    [Fact(DisplayName = "Sem fase que homologa, ou com a origem por declarar, a banca de homologação não se julga")]
    public void BancaDeHomologacao_SemFaseQueHomologaOuSemOrigem_NaoJulga()
    {
        ExigenciaDasBancasDaHomologacao.Veredicto semFase = Homologacao(OrigemCandidatos.InscricaoPropria, Fases(new ModeloDeFase("AVALIACAO", 1)), SemEtapas);
        ExigenciaDasBancasDaHomologacao.Veredicto semOrigem = Homologacao(OrigemCandidatos.Nenhuma, Fases(Homologa()), SemEtapas);
        DeveFicarSemJulgar(semFase);
        DeveFicarSemJulgar(semOrigem);
    }

    [Theory(DisplayName = "A parte que a leitura recusou deixa a banca de homologação sem julgar, sem causa")]
    [InlineData("fases", null)]
    [InlineData("fases[0].regraDeHomologacao", null)]
    [InlineData("fases[0].bancasRequeridas", null)]
    [InlineData(null, "etapas[0].bancas")]
    [InlineData(null, "etapas")]
    public void BancaDeHomologacao_ComParteRecusada_NaoJulga(string? daFase, string? daEtapa)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([Homologa().Adotada()], daFase is null ? OrigemNoDocumento.Identidade : ComPartesRecusadas(daFase));
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([EtapaNaFase(Fase).Adotada()], daEtapa is null ? OrigemNoDocumento.Identidade : ComPartesRecusadas(daEtapa));
        DeveFicarSemJulgar(Homologacao(OrigemCandidatos.InscricaoPropria, cronograma, etapas));
    }

    [Fact(DisplayName = "A parte recusada nas bancas de uma fase não esconde a falta na outra fase que homologa")]
    public void BancaDeHomologacao_ParteRecusadaEmUmaFase_AindaCobraAOutra()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(
            [Homologa(1, Fase).Adotada(), Homologa(2, "HOMOLOGACAO_FINAL").Adotada()], ComPartesRecusadas("fases[1].bancasRequeridas"));

        ExigenciaDasBancasDaHomologacao.Veredicto veredicto = Homologacao(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas);

        Causas(veredicto).Should().Equal(("fases[0].bancasRequeridas", CodigoDaHomologacao));
        veredicto.Indeterminado.Should().BeTrue();
    }

    [Fact(DisplayName = "O processo que usa a nota do ENEM com inscrição própria sem banca de importação das notas é recusado em fases, com o detalhe do processo")]
    public void NotaDoEnem_SemBancaDeImportacao_Recusa()
    {
        ExigenciaDasBancasDaHomologacao.Veredicto veredicto = Notas(OrigemCandidatos.InscricaoPropria, Fases(Homologa(DeHomologacao())), ComNotaDoEnem);

        veredicto.Indeterminado.Should().BeFalse();
        veredicto.Causas.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "fases",
            Error = new
            {
                Code = CodigoDasNotas,
                Message = "O processo usa a nota do ENEM com inscrição própria e não prevê a banca de importação das notas do INEP em nenhuma fase ou etapa. Acrescente essa banca onde as notas são importadas.",
            },
        });
    }

    [Fact(DisplayName = "A banca de importação das notas na fase, ou na etapa, atende a exigência, de qualquer fase")]
    public void NotaDoEnem_ComBancaDeImportacao_Atende()
    {
        CronogramaDoProcesso comNaFase = Fases(Homologa(DeHomologacao()), new ModeloDeFase("INSCRICAO", 2) with { Bancas = [DeImportacaoDasNotas()] });
        EtapasDoProcesso comNaEtapa = Etapas(NotaDoEnem(BancasDeTeste.DaEtapa(TipoDeBanca.ImportacaoDasNotasDoInep)));
        DeveEstarAtendido(Notas(OrigemCandidatos.InscricaoPropria, comNaFase, ComNotaDoEnem));
        DeveEstarAtendido(Notas(OrigemCandidatos.InscricaoPropria, Fases(Homologa(DeHomologacao())), comNaEtapa));
    }

    [Fact(DisplayName = "A banca de importação é reconhecida pelo papel que a cópia guarda, e não pelo código do tipo")]
    public void BancaDeImportacao_EReconhecidaPeloPapel_NaoPeloCodigo()
    {
        BancaRequerida codigoDeImportacaoComOutroPapel = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
            TiposDeBancaCopiadosDeTeste.Com(TipoDeBanca.ImportacaoDasNotasDoInep, PapelDaBanca.AvaliaOMerito), [], BancasDeTeste.Regra());
        BancaRequerida outroCodigoComPapelDeImportacao = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
            TiposDeBancaCopiadosDeTeste.Com(TipoDeBanca.CorrecaoDeRedacoes, PapelDaBanca.ImportaAsNotasDoInep), [], BancasDeTeste.Regra());

        Causas(Notas(OrigemCandidatos.InscricaoPropria, Fases(Homologa(codigoDeImportacaoComOutroPapel)), ComNotaDoEnem)).Should().ContainSingle();
        DeveEstarAtendido(Notas(OrigemCandidatos.InscricaoPropria, Fases(Homologa(outroCodigoComPapelDeImportacao)), ComNotaDoEnem));
    }

    [Fact(DisplayName = "A banca de importação das notas não substitui a de importação de candidatos, e o papel de uma não vale para a outra")]
    public void BancaDeImportacao_PapelDeUmaNaoVale_ParaOutra()
    {
        CronogramaDoProcesso comImportacaoDeCandidatos = Fases(Homologa(DeHomologacao(), DeImportacaoDeCandidatos()));

        Causas(Notas(OrigemCandidatos.InscricaoPropria, comImportacaoDeCandidatos, ComNotaDoEnem)).Should().ContainSingle();
        Causas(Candidatos(OrigemCandidatos.ImportacaoExterna, Fases(Homologa(DeHomologacao(), DeImportacaoDasNotas())), SemEtapas)).Should().ContainSingle();
    }

    [Fact(DisplayName = "Sem a nota do ENEM, ou com a importação externa, a banca de importação das notas não é exigida")]
    public void NotaDoEnem_NaoSeAplica_NaoExige()
    {
        CronogramaDoProcesso cronograma = Fases(Homologa(DeHomologacao()));
        DeveEstarAtendido(Notas(OrigemCandidatos.InscricaoPropria, cronograma, SemEtapas));
        DeveEstarAtendido(Notas(OrigemCandidatos.ImportacaoExterna, cronograma, ComNotaDoEnem));
    }

    [Theory(DisplayName = "A banca de importação das notas não se julga com a origem por declarar, com a etapa recusada ou com bancas recusadas")]
    [InlineData(OrigemCandidatos.Nenhuma, null, null)]
    [InlineData(OrigemCandidatos.InscricaoPropria, "fases", null)]
    [InlineData(OrigemCandidatos.InscricaoPropria, "fases[0].bancasRequeridas", null)]
    [InlineData(OrigemCandidatos.InscricaoPropria, null, "etapas")]
    [InlineData(OrigemCandidatos.InscricaoPropria, null, "etapas[0].bancas")]
    public void NotaDoEnem_Indeterminada_NaoJulga(OrigemCandidatos origem, string? daFase, string? daEtapa)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([Homologa(DeHomologacao()).Adotada()], daFase is null ? OrigemNoDocumento.Identidade : ComPartesRecusadas(daFase));
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([NotaDoEnem().Adotada()], daEtapa is null ? OrigemNoDocumento.Identidade : ComPartesRecusadas(daEtapa));
        DeveFicarSemJulgar(Notas(origem, cronograma, etapas));
    }

    [Fact(DisplayName = "A importação externa sem banca de importação de candidatos é recusada em fases, com o detalhe do processo")]
    public void ImportacaoExterna_SemBancaDeImportacaoDeCandidatos_Recusa()
    {
        ExigenciaDasBancasDaHomologacao.Veredicto veredicto = Candidatos(OrigemCandidatos.ImportacaoExterna, Fases(Homologa(DeHomologacao())), SemEtapas);

        veredicto.Indeterminado.Should().BeFalse();
        veredicto.Causas.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "fases",
            Error = new
            {
                Code = CodigoDosCandidatos,
                Message = "O processo importa candidatos de fora e não prevê a banca de importação de candidatos em nenhuma fase ou etapa. Acrescente essa banca onde os candidatos são importados.",
            },
        });
    }

    [Fact(DisplayName = "A banca de importação de candidatos na fase, ou na etapa, atende, e a inscrição própria não a exige")]
    public void ImportacaoExterna_ComBancaDeImportacaoDeCandidatos_Atende()
    {
        EtapasDoProcesso comNaEtapa = Etapas(EtapaNaFase(Fase) with { Bancas = [BancasDeTeste.DaEtapa(TipoDeBanca.ImportacaoDeCandidatos)] });
        DeveEstarAtendido(Candidatos(OrigemCandidatos.ImportacaoExterna, Fases(Homologa(DeHomologacao(), DeImportacaoDeCandidatos())), SemEtapas));
        DeveEstarAtendido(Candidatos(OrigemCandidatos.ImportacaoExterna, Fases(Homologa(DeHomologacao())), comNaEtapa));
        DeveEstarAtendido(Candidatos(OrigemCandidatos.InscricaoPropria, Fases(Homologa(DeHomologacao())), SemEtapas));
    }

    [Theory(DisplayName = "A banca de importação de candidatos não se julga com a origem por declarar, com a lista de fases recusada ou com bancas recusadas")]
    [InlineData(OrigemCandidatos.Nenhuma, null, null)]
    [InlineData(OrigemCandidatos.ImportacaoExterna, "fases", null)]
    [InlineData(OrigemCandidatos.ImportacaoExterna, "fases[0].bancasRequeridas", null)]
    [InlineData(OrigemCandidatos.ImportacaoExterna, null, "etapas")]
    [InlineData(OrigemCandidatos.ImportacaoExterna, null, "etapas[0].bancas")]
    public void ImportacaoExterna_Indeterminada_NaoJulga(OrigemCandidatos origem, string? daFase, string? daEtapa)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([Homologa(DeHomologacao()).Adotada()], daFase is null ? OrigemNoDocumento.Identidade : ComPartesRecusadas(daFase));
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([EtapaNaFase(Fase).Adotada()], daEtapa is null ? OrigemNoDocumento.Identidade : ComPartesRecusadas(daEtapa));
        DeveFicarSemJulgar(Candidatos(origem, cronograma, etapas));
    }

    [Fact(DisplayName = "As causas saem na ordem da homologação, das notas e dos candidatos, e a origem declarada nunca dispara as duas últimas juntas")]
    public void Causas_SaemNaOrdem_ENuncaAsDuasImportacoesJuntas()
    {
        CronogramaDoProcesso cronograma = Fases(Homologa());

        CenarioDeProcesso.Causas(ExigenciaDasBancasDaHomologacao.Causas(OrigemCandidatos.InscricaoPropria, cronograma, ComNotaDoEnem))
            .Should().Equal(("fases[0].bancasRequeridas", CodigoDaHomologacao), ("fases", CodigoDasNotas));
        CenarioDeProcesso.Causas(ExigenciaDasBancasDaHomologacao.Causas(OrigemCandidatos.ImportacaoExterna, cronograma, ComNotaDoEnem))
            .Should().Equal(("fases[0].bancasRequeridas", CodigoDaHomologacao), ("fases", CodigoDosCandidatos));
        ExigenciaDasBancasDaHomologacao.Causas(OrigemCandidatos.Nenhuma, cronograma, ComNotaDoEnem).Should().BeEmpty();
    }

}
