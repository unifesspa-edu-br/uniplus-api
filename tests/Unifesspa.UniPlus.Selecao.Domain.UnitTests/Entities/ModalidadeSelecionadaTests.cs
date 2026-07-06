namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class ModalidadeSelecionadaTests
{
    private static Result<ModalidadeSelecionada> CriarAmpla() => ModalidadeSelecionada.Criar(
        Guid.CreateVersion7(), "AC", "Ampla concorrência",
        NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
        composicaoOrigemCodigo: null, RegraRemanejamentoModalidade.Nenhuma,
        remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
        criteriosCumulativos: [], acaoQuandoIndeferido: null, baseLegal: "Lei 12.711/2012");

    [Fact(DisplayName = "Criar modalidade ampla (residual, sem remanejamento) tem sucesso")]
    public void Criar_Ampla_Sucesso()
    {
        Result<ModalidadeSelecionada> resultado = CriarAmpla();

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.NaturezaLegal.Should().Be(NaturezaLegalModalidade.Ampla);
        resultado.Value.RegraRemanejamento.Should().Be(RegraRemanejamentoModalidade.Nenhuma);
    }

    [Fact(DisplayName = "Criar modalidade de cota reservada com SEGUE_CASCATA tem sucesso")]
    public void Criar_CotaReservadaComCascata_Sucesso()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "LB_PPI", "Baixa renda PPI",
            NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr,
            composicaoOrigemCodigo: null, RegraRemanejamentoModalidade.SegueCascata,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: ["RENDA_ATE_1SM_PER_CAPITA", "PPI"], acaoQuandoIndeferido: "RECLASSIFICA_AC",
            baseLegal: "Lei 12.711/2012 art. 3º");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar modalidade RETIRA_DE com origem tem sucesso")]
    public void Criar_RetiraDeComOrigem_Sucesso()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "V", "PcD retirada da AC",
            NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe,
            composicaoOrigemCodigo: "AC", RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: ["PCD"], acaoQuandoIndeferido: null, baseLegal: "Edital SiSU 4.1.2");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ComposicaoOrigemCodigo.Should().Be("AC");
    }

    [Fact(DisplayName = "Criar modalidade com remanejamento CRUZADO completo tem sucesso")]
    public void Criar_Cruzado_Sucesso()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "IND", "Indígena (PSIQ)",
            NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal,
            composicaoOrigemCodigo: null, RegraRemanejamentoModalidade.Cruzado,
            remanejamentoDestino: null, remanejamentoPar: "QUIL", remanejamentoFallback: "AC",
            criteriosCumulativos: ["INDIGENA"], acaoQuandoIndeferido: null, baseLegal: "Res. 532/2021");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com código vazio falha")]
    public void Criar_CodigoVazio_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.CodigoObrigatorio");
    }

    [Fact(DisplayName = "Criar com natureza legal Nenhuma falha (INV-2)")]
    public void Criar_NaturezaLegalNenhuma_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null,
            NaturezaLegalModalidade.Nenhuma, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.NaturezaLegalObrigatoria");
    }

    [Fact(DisplayName = "Criar com composição de vagas Nenhuma falha (INV-2)")]
    public void Criar_ComposicaoVagasNenhuma_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.Nenhuma,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.ComposicaoVagasObrigatoria");
    }

    [Fact(DisplayName = "Criar com base legal vazia falha (INV-2)")]
    public void Criar_BaseLegalVazia_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.BaseLegalObrigatoria");
    }

    [Fact(DisplayName = "Criar RETIRA_DE sem código de origem falha")]
    public void Criar_RetiraDeSemOrigem_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "V", null,
            NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe,
            composicaoOrigemCodigo: null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.ComposicaoOrigemObrigatoria");
    }

    [Fact(DisplayName = "Criar com origem informada fora de RETIRA_DE falha")]
    public void Criar_ComposicaoOrigemIndevida_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: "X", RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.ComposicaoOrigemIndevida");
    }

    [Fact(DisplayName = "Criar cota reservada sem SEGUE_CASCATA falha (INV-12)")]
    public void Criar_CotaReservadaSemCascata_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "LB_PPI", null,
            NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr,
            null, RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null, [], null, "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.CotaReservadaExigeCascata");
    }

    [Fact(DisplayName = "Criar DESTINO_UNICO sem destino falha")]
    public void Criar_DestinoUnicoSemDestino_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "X", null,
            NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal,
            null, RegraRemanejamentoModalidade.DestinoUnico, remanejamentoDestino: null, null, null, [], null, "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.RemanejamentoDestinoObrigatorio");
    }

    [Fact(DisplayName = "Criar CRUZADO incompleto (só par, sem fallback) falha")]
    public void Criar_CruzadoIncompleto_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "IND", null,
            NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal,
            null, RegraRemanejamentoModalidade.Cruzado, null, remanejamentoPar: "QUIL", remanejamentoFallback: null,
            criteriosCumulativos: [], acaoQuandoIndeferido: null, baseLegal: "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.RemanejamentoCruzadoIncompleto");
    }

    [Fact(DisplayName = "Criar com destino informado fora de DESTINO_UNICO falha")]
    public void Criar_RemanejamentoDestinoIndevido_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, remanejamentoDestino: "X", null, null, [], null, "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.RemanejamentoDestinoIndevido");
    }

    [Fact(DisplayName = "Criar com par/fallback informados fora de CRUZADO falha")]
    public void Criar_RemanejamentoCruzadoIndevido_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, remanejamentoPar: "X", remanejamentoFallback: "Y",
            criteriosCumulativos: [], acaoQuandoIndeferido: null, baseLegal: "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.RemanejamentoCruzadoIndevido");
    }
}
