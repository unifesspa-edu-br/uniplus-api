namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ConfiguracaoDistribuicaoVagasTests
{
    private static ReferenciaRegra RegraLei12711() =>
        ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Lei12711, "v1", new string('a', 64)).Value!;

    private static ReferenciaRegra RegraInstitucional() =>
        ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('b', 64)).Value!;

    private static ReferenciaRegra RegraPsiq() =>
        ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Psiq, "v1", new string('d', 64)).Value!;

    private static ReferenciaRegra RegraEduCampo() =>
        ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.EduCampo, "v1", new string('e', 64)).Value!;

    private static ReferenciaRegra RegraAjuste() =>
        ReferenciaRegra.Criar("RECONCILIACAO-VAGAS-ART11-PU", "v1", new string('c', 64)).Value!;

    private static ReferenciaReservaDemograficaSnapshot Demografica() =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!;

    private static ModalidadeSelecionada Modalidade(
        string codigo,
        NaturezaLegalModalidade natureza,
        ComposicaoVagasModalidade composicao,
        int? quantidadeDeclarada = null,
        string? composicaoOrigemCodigo = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza, composicao,
            composicaoOrigemCodigo,
            natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, [], null, "base legal", quantidadeDeclarada).Value!;

    private static List<ModalidadeSelecionada> AsOitoFederaisMaisAc() =>
    [
        Modalidade(ModalidadesFederaisLei12711.LbPpi, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LbQ, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LbPcd, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LbEp, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LiPpi, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LiQ, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LiPcd, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LiEp, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo),
    ];

    [Fact(DisplayName = "Criar Lei 12.711 com as 8 federais + AC e referência demográfica tem sucesso")]
    public void Criar_Lei12711Completa_Sucesso()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 0.5m, RegraLei12711(), RegraAjuste(), Demografica(), AsOitoFederaisMaisAc());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Modalidades.Should().HaveCount(9);
    }

    [Fact(DisplayName = "Criar institucional sem referência demográfica tem sucesso (quadro fixo, sem Censo)")]
    public void Criar_Institucional_SemDemografica_Sucesso()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("IND", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 30),
            Modalidade("QUIL", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 30),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 60, pr: 1m, RegraInstitucional(), regraAjuste: null, referenciaDemografica: null, modalidades);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.VagasOfertadas.Should().HaveCount(2);
        resultado.Value!.TotalPublicado.Should().Be(60);
    }

    [Fact(DisplayName = "Quadro institucional que soma acima do VO_base é recusado")]
    public void Criar_Institucional_QuadroAcimaDoVoBase_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, quantidadeDeclarada: 40),
            Modalidade("AC_PCD", NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe, quantidadeDeclarada: 2, composicaoOrigemCodigo: "AC"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 1m, RegraInstitucional(), regraAjuste: null, referenciaDemografica: null, modalidades);

        resultado.IsFailure.Should().BeTrue(
            "as quantidades dividem o total da oferta; publicar 42 numa oferta de 40 contradiz o VO_base declarado");
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroExcedeVoBase");
        resultado.Error.Message.Should().Contain("42").And.Contain("40");
    }

    [Fact(DisplayName = "Quadro institucional que soma abaixo do VO_base é recusado")]
    public void Criar_Institucional_QuadroAbaixoDoVoBase_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, quantidadeDeclarada: 4),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 1m, RegraInstitucional(), regraAjuste: null, referenciaDemografica: null, modalidades);

        resultado.IsFailure.Should().BeTrue("a oferta tem 40 vagas e o quadro distribuiu 4");
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroNaoCompletaVoBase");
    }

    [Fact(DisplayName = "Suplementar institucional entra na soma do quadro — não é dispensada do total")]
    public void Criar_Institucional_SuplementaresAcimaDoVoBase_Falha()
    {
        // Dispensar a suplementar da soma neste ramo aceitaria um certame inteiro de
        // suplementares como se distribuísse zero: aqui não há conjunto calculado ao lado
        // ao qual elas pudessem acrescer, e o total publicado é a soma do quadro.
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC_I", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 30),
            Modalidade("AC_Q", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 30),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 1m, RegraInstitucional(), regraAjuste: null, referenciaDemografica: null, modalidades);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroExcedeVoBase");
    }

    [Fact(DisplayName = "Suplementar na Lei 12.711 continua acrescendo ao total, sem cair na regra do quadro institucional")]
    public void Criar_Lei12711_ComSuplementar_AcresceAoTotal()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            .. AsOitoFederaisMaisAc(),
            Modalidade("AC_I", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 5),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 0.5m, RegraLei12711(), RegraAjuste(), Demografica(), modalidades);

        resultado.IsSuccess.Should().BeTrue("no ramo federal a suplementar acresce ao VO_base, e a igualdade do quadro já vem da calculadora");
        resultado.Value!.TotalPublicado.Should().Be(55);
    }

    [Fact(DisplayName = "VO_base acima das vagas anuais autorizadas da oferta é recusado")]
    public void Criar_VoBaseAcimaDoTetoDaOferta_Falha()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 99999, pr: 0.5m, RegraLei12711(), RegraAjuste(), Demografica(),
            AsOitoFederaisMaisAc(), vagasAnuaisAutorizadas: 40);

        resultado.IsFailure.Should().BeTrue(
            "o certame que ultrapassa o teto publica mais vagas do que o e-MEC autoriza para a oferta");
        resultado.Errors.Should().Contain(e => e.Error.Code == "ConfiguracaoDistribuicaoVagas.VoBaseAcimaDasVagasAutorizadas");
    }

    [Fact(DisplayName = "VO_base igual às vagas anuais autorizadas é aceito")]
    public void Criar_VoBaseIgualAoTetoDaOferta_Sucesso()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 0.5m, RegraLei12711(), RegraAjuste(), Demografica(),
            AsOitoFederaisMaisAc(), vagasAnuaisAutorizadas: 40);

        resultado.IsSuccess.Should().BeTrue("o teto é o limite, e alcançá-lo é legítimo");
    }

    [Fact(DisplayName = "Oferta sem vagas anuais declaradas não impõe teto")]
    public void Criar_OfertaSemTetoDeclarado_NaoImpoeLimite()
    {
        // Ausência do dado não é permissão nem proibição: sem o teto declarado não há contra
        // o que confrontar, e recusar equivaleria a tratar a lacuna como zero.
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 99999, pr: 0.5m, RegraLei12711(), RegraAjuste(), Demografica(),
            AsOitoFederaisMaisAc(), vagasAnuaisAutorizadas: null);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Modalidade fora do rol que a regra admite é recusada, nomeando regra e modalidade")]
    public void Criar_ModalidadeForaDoRolDaRegra_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC_I", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2),
            Modalidade("AC_Q", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2),
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, quantidadeDeclarada: 6),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10, pr: 1m, RegraPsiq(), regraAjuste: null, referenciaDemografica: null,
            modalidades, vagasAnuaisAutorizadas: null, modalidadesAdmitidas: ["AC_I", "AC_Q"]);

        resultado.IsFailure.Should().BeTrue("o PSIQ é certame exclusivo — não oferta ampla concorrência");
        resultado.Errors.Should().Contain(e =>
            e.Error.Code == "ConfiguracaoDistribuicaoVagas.ModalidadeNaoAdmitidaPelaRegra"
            && e.Error.Message.Contains("AC", StringComparison.Ordinal)
            && e.Error.Message.Contains(RegraDistribuicaoVagasCodigo.Psiq, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "PSIQ com o rol que a regra admite é aceito, e a soma das suplementares é o total")]
    public void Criar_Psiq_ComRolAdmitido_Sucesso()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC_I", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2),
            Modalidade("AC_Q", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 4, pr: 1m, RegraPsiq(), regraAjuste: null, referenciaDemografica: null,
            modalidades, vagasAnuaisAutorizadas: null, modalidadesAdmitidas: ["AC_I", "AC_Q"]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.TotalPublicado.Should().Be(4,
            "sem outro conjunto ao qual se somem, as vagas por acréscimo são o total publicado");
    }

    [Fact(DisplayName = "Educação do Campo aceita AC e PCD_PURO, com a reserva retirando da ampla")]
    public void Criar_EduCampo_ComRolAdmitido_Sucesso()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, quantidadeDeclarada: 38),
            Modalidade("PCD_PURO", NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe, quantidadeDeclarada: 2, composicaoOrigemCodigo: "AC"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 1m, RegraEduCampo(), regraAjuste: null, referenciaDemografica: null,
            modalidades, vagasAnuaisAutorizadas: null, modalidadesAdmitidas: ["AC", "PCD_PURO"]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.TotalPublicado.Should().Be(40, "PCD_PURO retira de AC — o par fecha no VO_base");
    }

    [Fact(DisplayName = "Regra sem rol declarado não restringe as modalidades")]
    public void Criar_SemRolDeclarado_NaoRestringe()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("QUALQUER_CODIGO", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 10),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10, pr: 1m, RegraInstitucional(), regraAjuste: null, referenciaDemografica: null,
            modalidades, vagasAnuaisAutorizadas: null, modalidadesAdmitidas: null);

        resultado.IsSuccess.Should().BeTrue(
            "rol aberto atende o certame institucional que ainda não tem regra própria");
    }

    [Fact(DisplayName = "Com regra de ajuste, o quadro que excede é reconciliado em vez de recusado")]
    public void Criar_QuadroExcedente_ComAjuste_Reconcilia()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, quantidadeDeclarada: 40),
            Modalidade("PCD_PURO", NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe, quantidadeDeclarada: 4, composicaoOrigemCodigo: "AC"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 1m, RegraEduCampo(), RegraAjuste(), referenciaDemografica: null,
            modalidades, vagasAnuaisAutorizadas: null, modalidadesAdmitidas: null,
            argsAjuste: new ArgsReduzirDe("AC"));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.TotalPublicado.Should().Be(40);
        resultado.Value.Estouro.Should().Be(4, "o excesso absorvido precisa aparecer, não desaparecer");
        resultado.Value.CapadoEmVo.Should().BeTrue();
        resultado.Value.VagasOfertadas.Single(v => v.ModalidadeCodigo == "AC").Quantidade.Should().Be(36);
        resultado.Value.VagasOfertadas.Single(v => v.ModalidadeCodigo == "PCD_PURO").Quantidade.Should().Be(4);
    }

    [Fact(DisplayName = "Sem regra de ajuste, o quadro que excede continua sendo recusado")]
    public void Criar_QuadroExcedente_SemAjuste_Recusa()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, quantidadeDeclarada: 40),
            Modalidade("PCD_PURO", NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe, quantidadeDeclarada: 4, composicaoOrigemCodigo: "AC"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 1m, RegraEduCampo(), regraAjuste: null, referenciaDemografica: null,
            modalidades, vagasAnuaisAutorizadas: null, modalidadesAdmitidas: null, argsAjuste: null);

        resultado.IsFailure.Should().BeTrue("sem motor declarado não há de onde tirar");
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroExcedeVoBase");
    }

    [Fact(DisplayName = "A sobra continua recusada mesmo com regra de ajuste — os motores só reduzem")]
    public void Criar_QuadroComSobra_ComAjuste_ContinuaRecusado()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, quantidadeDeclarada: 4),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 1m, RegraInstitucional(), RegraAjuste(), referenciaDemografica: null,
            modalidades, vagasAnuaisAutorizadas: null, modalidadesAdmitidas: null,
            argsAjuste: new ArgsReduzirDe("AC"));

        resultado.IsFailure.Should().BeTrue(
            "acrescer seria o sistema criar vaga que ninguém autorizou — quem configura fecha a conta");
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroNaoCompletaVoBase");
    }

    [Theory(DisplayName = "Criar com VO_base não positivo falha")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_VoBaseInvalido_Falha(int voBase)
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase, pr: 0.5m, RegraLei12711(), RegraAjuste(), Demografica(), AsOitoFederaisMaisAc());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.VoBaseInvalido");
    }

    [Theory(DisplayName = "Criar com PR fora de [0,5; 1] falha (INV-1)")]
    [InlineData(0.49)]
    [InlineData(1.01)]
    public void Criar_PrForaDoLimite_Falha(double pr)
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, (decimal)pr, RegraLei12711(), RegraAjuste(), Demografica(), AsOitoFederaisMaisAc());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.PrForaDoLimite");
    }

    [Fact(DisplayName = "Criar sem modalidades falha")]
    public void Criar_ModalidadesVazias_Falha()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 0.5m, RegraLei12711(), RegraAjuste(), Demografica(), []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ModalidadesVazias");
    }

    [Fact(DisplayName = "Criar com modalidade duplicada falha")]
    public void Criar_ModalidadeDuplicada_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo),
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 1m, RegraInstitucional(), null, null, modalidades);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada");
    }

    [Fact(DisplayName = "Criar com RETIRA_DE apontando para código fora do conjunto selecionado falha")]
    public void Criar_ComposicaoOrigemForaDoConjunto_Falha()
    {
        ModalidadeSelecionada retiraDeSemOrigemSelecionada = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "V", null, NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe,
            composicaoOrigemCodigo: "AC", RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base legal").Value!;

        // "AC" não está selecionado nesta oferta — a origem do RETIRA_DE
        // aponta para uma modalidade ausente do conjunto.
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 1m, RegraInstitucional(), null, null, [retiraDeSemOrigemSelecionada]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ComposicaoOrigemNaoSelecionada");
    }

    [Fact(DisplayName = "Criar com remanejamento CRUZADO apontando para par fora do conjunto falha")]
    public void Criar_RemanejamentoParForaDoConjunto_Falha()
    {
        ModalidadeSelecionada indSemParSelecionado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "IND", null, NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal,
            null, RegraRemanejamentoModalidade.Cruzado, null, remanejamentoPar: "QUIL", remanejamentoFallback: "AC",
            criteriosCumulativos: [], acaoQuandoIndeferido: null, baseLegal: "base legal").Value!;

        ModalidadeSelecionada ac = Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo);

        // "QUIL" (o par) não está selecionado — só "IND" e "AC" (o fallback).
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 1m, RegraInstitucional(), null, null, [indSemParSelecionado, ac]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.RemanejamentoParNaoSelecionado");
    }

    [Fact(DisplayName = "Criar Lei 12.711 sem referência demográfica falha (INV-5)")]
    public void Criar_Lei12711SemDemografica_Falha()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 0.5m, RegraLei12711(), RegraAjuste(), referenciaDemografica: null, AsOitoFederaisMaisAc());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaObrigatoria");
    }

    [Fact(DisplayName = "Criar Lei 12.711 faltando uma modalidade federal falha (INV-6)")]
    public void Criar_Lei12711FaltandoFederal_Falha()
    {
        List<ModalidadeSelecionada> modalidades = [.. AsOitoFederaisMaisAc().Where(m => m.Codigo != ModalidadesFederaisLei12711.LiEp)];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 0.5m, RegraLei12711(), RegraAjuste(), Demografica(), modalidades);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ModalidadesFederaisIncompletas");
    }

    [Fact(DisplayName = "Criar Lei 12.711 com retirada de origem diferente de AC falha")]
    public void Criar_Lei12711RetiradaForaDeAc_Falha()
    {
        ModalidadeSelecionada retiradaDeSubReserva = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "V", null, NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe,
            composicaoOrigemCodigo: ModalidadesFederaisLei12711.LbPpi, RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, [], null, "base legal", quantidadeDeclarada: 1).Value!;

        List<ModalidadeSelecionada> modalidades = [.. AsOitoFederaisMaisAc(), retiradaDeSubReserva];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 0.5m, RegraLei12711(), RegraAjuste(), Demografica(), modalidades);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.RetiradaFederalDeveSerDeAmplaConcorrencia");
    }

    [Fact(DisplayName = "Criar institucional com referência demográfica indevida falha")]
    public void Criar_InstitucionalComDemografica_Falha()
    {
        List<ModalidadeSelecionada> modalidades = [Modalidade("IND", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal)];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 60, pr: 1m, RegraInstitucional(), null, Demografica(), modalidades);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaIndevida");
    }

    [Fact(DisplayName = "ADR-0125: VO_base inválido e PR fora do limite acumulam no mesmo lote")]
    public void Criar_VoBaseInvalidoEPrForaDoLimite_AcumulaAsDuasViolacoes()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 0, pr: 1.5m, RegraInstitucional(), null, null, []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoDistribuicaoVagas.VoBaseInvalido",
            "ConfiguracaoDistribuicaoVagas.PrForaDoLimite",
            "ConfiguracaoDistribuicaoVagas.ModalidadesVazias",
        ]);
    }

    [Fact(DisplayName = "ValidarFormaBasica sem violação retorna lote vazio")]
    public void ValidarFormaBasica_SemViolacao_Vazio()
    {
        List<FieldError> erros = ConfiguracaoDistribuicaoVagas.ValidarFormaBasica(voBase: 50, pr: 0.75m, quantidadeModalidades: 1);

        erros.Should().BeEmpty();
    }

    [Fact(DisplayName = "ValidarFormaBasica com zero modalidades reporta ModalidadesVazias sem precisar de nenhuma resolvida")]
    public void ValidarFormaBasica_ZeroModalidades_ReportaModalidadesVazias()
    {
        List<FieldError> erros = ConfiguracaoDistribuicaoVagas.ValidarFormaBasica(voBase: 50, pr: 0.75m, quantidadeModalidades: 0);

        erros.Select(e => e.Error.Code).Should().BeEquivalentTo(["ConfiguracaoDistribuicaoVagas.ModalidadesVazias"]);
    }
}
