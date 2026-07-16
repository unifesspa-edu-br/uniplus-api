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

    private static ReferenciaRegra RegraAjuste() =>
        ReferenciaRegra.Criar("RECONCILIACAO-VAGAS-ART11-PU", "v1", new string('c', 64)).Value!;

    private static ReferenciaReservaDemograficaSnapshot Demografica() =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!;

    private static ModalidadeSelecionada Modalidade(
        string codigo, NaturezaLegalModalidade natureza, ComposicaoVagasModalidade composicao, int? quantidadeDeclarada = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza, composicao,
            composicaoOrigemCodigo: null,
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

    [Fact(DisplayName = "Criar com RETIRA_DE apontando para código fora do conjunto selecionado falha (achado Codex)")]
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

    [Fact(DisplayName = "Criar com remanejamento CRUZADO apontando para par fora do conjunto falha (achado Codex)")]
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

    [Fact(DisplayName = "Criar Lei 12.711 com retirada de origem diferente de AC falha (achado Codex)")]
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
}
