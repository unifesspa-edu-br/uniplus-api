namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConfiguracaoDeVagas;

public sealed class ConfiguracaoDistribuicaoVagasModalidadesTests
{
    [Fact(DisplayName = "Modalidade fora do rol que a regra admite é recusada, nomeando regra e modalidade")]
    public void Criar_ModalidadeForaDoRolDaRegra_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC_I", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2),
            Modalidade("AC_Q", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2),
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 6),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(10, RegraPsiq(), modalidades, ["AC_I", "AC_Q"]);

        resultado.IsFailure.Should().BeTrue("o PSIQ é certame exclusivo — não oferta ampla concorrência");
        resultado.Errors.Should().Contain(e =>
            e.Error.Code == "ConfiguracaoDistribuicaoVagas.ModalidadeNaoAdmitidaPelaRegra"
            && e.Error.Message.Contains("AC", StringComparison.Ordinal)
            && e.Error.Message.Contains(RegraDistribuicaoVagasCodigo.Psiq, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Rol fechado com modalidade faltando é recusado, não só o excedente")]
    public void Criar_Psiq_ComRolIncompleto_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC_I", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(2, RegraPsiq(), modalidades, ["AC_I", "AC_Q"]);

        resultado.IsFailure.Should().BeTrue("o rol do PSIQ é fechado nos dois sentidos — AC_Q ausente não fecha o quadro que a regra declara");
        resultado.Errors.Should().Contain(e =>
            e.Error.Code == "ConfiguracaoDistribuicaoVagas.ModalidadeDoRolAusente"
            && e.Error.Message.Contains("AC_Q", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "PSIQ com o rol que a regra admite é aceito, e a soma das suplementares é o total")]
    public void Criar_Psiq_ComRolAdmitido_Sucesso()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC_I", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2),
            Modalidade("AC_Q", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(4, RegraPsiq(), modalidades, ["AC_I", "AC_Q"]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.TotalDeVagasDoEdital.Should().Be(4,
            "sem outro conjunto ao qual se somem, as vagas por acréscimo são o total publicado");
    }

    [Fact(DisplayName = "Com AC_PCD sem cotas aceita AC e AC_PCD, com a reserva retirando da ampla")]
    public void Criar_ComAcPcd_ComRolAdmitido_Sucesso()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 38),
            Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, quantidadeDeclarada: 2, composicaoOrigemCodigo: "AC"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(40, RegraComAcPcd(), modalidades, ["AC", "AC_PCD"]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.TotalDeVagasDoEdital.Should().Be(40, "AC_PCD retira de AC — o par fecha no VO_base");
    }

    [Fact(DisplayName = "Regra sem rol declarado não restringe as modalidades")]
    public void Criar_SemRolDeclarado_NaoRestringe()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("QUALQUER_CODIGO", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 10),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(10, RegraInstitucional(), modalidades);

        resultado.IsSuccess.Should().BeTrue(
            "rol aberto atende o certame institucional que ainda não tem regra própria");
    }

    /// <remarks>
    /// O formato do PSIQ: certame exclusivo de vagas por acréscimo, sem ampla concorrência.
    /// Sem esta recusa o destino congela no envelope, que é imutável, e sobrevive à
    /// retificação — o motor de classificação encontraria instrução de reclassificar para
    /// uma modalidade que o processo nunca ofertou.
    /// </remarks>
    [Fact(DisplayName = "Modalidade que reclassifica na ampla é recusada quando a ampla não é ofertada")]
    public void Criar_ReclassificarAcSemAcOfertada_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            ModalidadeComAcao("AC_I", "RECLASSIFICAR_AC", quantidadeDeclarada: 2),
            ModalidadeComAcao("AC_Q", acaoQuandoIndeferido: null, quantidadeDeclarada: 2),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(4, RegraPsiq(), modalidades);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().Contain(e =>
            e.Error.Code == "ConfiguracaoDistribuicaoVagas.ReclassificacaoParaAmplaSemAmplaOfertada"
            && e.Error.Message.Contains("AC_I", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Reclassificar na ampla é aceito quando a ampla está entre as ofertadas")]
    public void Criar_ReclassificarAcComAcOfertada_Sucesso()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            ModalidadeComAcao("AC", acaoQuandoIndeferido: null, quantidadeDeclarada: 38),
            ModalidadeComAcao("AC_PCD", "RECLASSIFICAR_AC", quantidadeDeclarada: 2, composicaoOrigemCodigo: "AC"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(40, RegraComAcPcd(), modalidades);

        resultado.IsSuccess.Should().BeTrue("a ampla está ofertada — o destino da reclassificação existe");
    }

    [Fact(DisplayName = "RECLASSIFICAR_REGRA_EDITAL não exige a ampla ofertada — o destino vem da regra, não de um código")]
    public void Criar_ReclassificarRegraEditalSemAc_Sucesso()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            ModalidadeComAcao("AC_I", "RECLASSIFICAR_REGRA_EDITAL", quantidadeDeclarada: 2),
            ModalidadeComAcao("AC_Q", acaoQuandoIndeferido: null, quantidadeDeclarada: 2),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(4, RegraPsiq(), modalidades);

        resultado.IsSuccess.Should().BeTrue(
            "o token delega o destino à regra do edital e não nomeia modalidade nenhuma");
    }

    private static ModalidadeSelecionada ModalidadeComAcao(
        string codigo,
        string? acaoQuandoIndeferido,
        int quantidadeDeclarada,
        string? composicaoOrigemCodigo = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null,
            NaturezaLegalModalidade.AcaoAfirmativa,
            composicaoOrigemCodigo is null ? ComposicaoVagasModalidade.SuplementarAoTotal : ComposicaoVagasModalidade.RetiradaDeOutraModalidade,
            composicaoOrigemCodigo,
            RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), acaoQuandoIndeferido, "base legal", quantidadeDeclarada).Value!;

    [Fact(DisplayName = "Par cruzado recíproco é aceito")]
    public void Criar_ParCruzadoReciproco_Sucesso()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            ModalidadeCruzada("AC_I", par: "AC_Q"),
            ModalidadeCruzada("AC_Q", par: "AC_I"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(4, RegraPsiq(), modalidades);

        resultado.IsSuccess.Should().BeTrue("é o par do PSIQ, recíproco desde o seed");
    }

    [Fact(DisplayName = "Par cruzado unilateral é recusado — a vaga migraria num sentido e nunca no outro")]
    public void Criar_ParCruzadoUnilateral_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            ModalidadeCruzada("AC_I", par: "AC_Q"),
            ModalidadeCruzada("AC_Q", par: "OUTRA"),
            ModalidadeCruzada("OUTRA", par: "AC_Q"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(6, RegraInstitucional(), modalidades);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().Contain(e =>
            e.Error.Code == "ConfiguracaoDistribuicaoVagas.ParCruzadoNaoReciproco"
            && e.Error.Message.Contains("AC_I", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Par cruzado apontando para modalidade que não é cruzada é recusado")]
    public void Criar_ParCruzadoComContraparteNaoCruzada_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            ModalidadeCruzada("AC_I", par: "AC"),
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 2),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(4, RegraInstitucional(), modalidades);

        resultado.IsFailure.Should().BeTrue("o cruzamento exige que os dois lados o declarem");
        resultado.Errors.Should().Contain(e => e.Error.Code == "ConfiguracaoDistribuicaoVagas.ParCruzadoNaoReciproco");
    }

    [Fact(DisplayName = "Modalidade cruzada consigo mesma é recusada — um par de um nó não remaneja")]
    public void Criar_ParCruzadoConsigoMesma_Falha()
    {
        List<ModalidadeSelecionada> modalidades = [ModalidadeCruzada("AC_I", par: "AC_I")];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(2, RegraInstitucional(), modalidades);

        resultado.IsFailure.Should().BeTrue(
            "o auto-laço satisfaz a comparação recíproca mas não tem para onde remanejar");
        resultado.Errors.Should().Contain(e => e.Error.Code == "ConfiguracaoDistribuicaoVagas.ParCruzadoNaoReciproco");
    }

    private static ModalidadeSelecionada ModalidadeCruzada(string codigo, string par) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null,
            NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal,
            composicaoOrigemCodigo: null,
            RegraRemanejamentoModalidade.Cruzado,
            remanejamentoDestino: null, remanejamentoPar: par, remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), acaoQuandoIndeferido: null, baseLegal: "base legal",
            quantidadeDeclarada: 2).Value!;

    [Theory(DisplayName = "Criar com VO_base não positivo falha")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_VoBaseInvalido_Falha(int voBase)
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = voBase, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = AsOitoFederaisMaisAc() },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.VoBaseInvalido");
    }
}
