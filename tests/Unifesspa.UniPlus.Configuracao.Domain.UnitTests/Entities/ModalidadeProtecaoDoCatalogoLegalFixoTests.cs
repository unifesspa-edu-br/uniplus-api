namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDaModalidade;

/// <summary>Atualização de modalidade do catálogo legal fixo: só descrição e base legal são editáveis.</summary>
public sealed class ModalidadeProtecaoDoCatalogoLegalFixoTests
{
    [Fact(DisplayName = "Legal fixa recusa alteração de natureza")]
    public void Atualizar_LegalFixaNatureza_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: "Cota", CodigoDaNaturezaLegal: "AMPLA", CodigoDaComposicaoDeVagas: "DENTRO_DA_RESERVA_LEGAL",
            ComposicaoOrigem: null, CodigoDaRegraDeRemanejamento: "SEGUE_ORDEM_DE_REMANEJAMENTO",
            RemanejamentoDestino: null, RemanejamentoPar: null, RemanejamentoFallback: null,
            RegraDeAplicabilidade: AplicavelA("AMPLA"), CodigoDaAcaoQuandoIndeferido: null, BaseLegal: "Lei 12.711/2012"));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.NaturezaLegal.Should().Be(NaturezaLegal.CotaReservada, "a recusa não muta a entidade");
    }

    [Fact(DisplayName = "Legal fixa recusa alteração de composição de vagas")]
    public void Atualizar_LegalFixaComposicao_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: "Cota", CodigoDaNaturezaLegal: "COTA_RESERVADA", CodigoDaComposicaoDeVagas: "SUPLEMENTAR_AO_TOTAL",
            ComposicaoOrigem: null, CodigoDaRegraDeRemanejamento: "SEGUE_ORDEM_DE_REMANEJAMENTO",
            RemanejamentoDestino: null, RemanejamentoPar: null, RemanejamentoFallback: null,
            RegraDeAplicabilidade: AplicavelA("COTA_RESERVADA"), CodigoDaAcaoQuandoIndeferido: null, BaseLegal: "Lei 12.711/2012"));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.ComposicaoVagas.Should().Be(ComposicaoVagas.DentroDaReservaLegal);
    }

    [Fact(DisplayName = "Legal fixa recusa alteração da origem da composição")]
    public void Atualizar_LegalFixaOrigem_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: "Cota", CodigoDaNaturezaLegal: "COTA_RESERVADA", CodigoDaComposicaoDeVagas: "DENTRO_DA_RESERVA_LEGAL",
            ComposicaoOrigem: "AC", CodigoDaRegraDeRemanejamento: "SEGUE_ORDEM_DE_REMANEJAMENTO",
            RemanejamentoDestino: null, RemanejamentoPar: null, RemanejamentoFallback: null,
            RegraDeAplicabilidade: AplicavelA("COTA_RESERVADA"), CodigoDaAcaoQuandoIndeferido: null, BaseLegal: "Lei 12.711/2012"));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.ComposicaoOrigem.Should().BeNull();
    }

    [Fact(DisplayName = "Legal fixa recusa alteração da regra de remanejamento")]
    public void Atualizar_LegalFixaRegra_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: "Cota", CodigoDaNaturezaLegal: "COTA_RESERVADA", CodigoDaComposicaoDeVagas: "DENTRO_DA_RESERVA_LEGAL",
            ComposicaoOrigem: null, CodigoDaRegraDeRemanejamento: null,
            RemanejamentoDestino: null, RemanejamentoPar: null, RemanejamentoFallback: null,
            RegraDeAplicabilidade: AplicavelA("COTA_RESERVADA"), CodigoDaAcaoQuandoIndeferido: null, BaseLegal: "Lei 12.711/2012"));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.RegraRemanejamento.Should().Be(RegraRemanejamento.SegueOrdemDeRemanejamento);
    }

    [Fact(DisplayName = "Legal fixa recusa alteração da regra de aplicabilidade")]
    public void Atualizar_LegalFixaRegraDeAplicabilidade_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: "Cota", CodigoDaNaturezaLegal: "COTA_RESERVADA", CodigoDaComposicaoDeVagas: "DENTRO_DA_RESERVA_LEGAL",
            ComposicaoOrigem: null, CodigoDaRegraDeRemanejamento: "SEGUE_ORDEM_DE_REMANEJAMENTO",
            RemanejamentoDestino: null, RemanejamentoPar: null, RemanejamentoFallback: null,
            RegraDeAplicabilidade: ComOpcaoPelasCotas(optou: true, ("BAIXA_RENDA", true)), CodigoDaAcaoQuandoIndeferido: null,
            BaseLegal: "Lei 12.711/2012"));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.RegraDeAplicabilidade.Clausulas.Should().ContainSingle()
            .Which.Condicoes.Should().ContainSingle("a recusa não muta a regra");
    }

    [Fact(DisplayName = "Legal fixa recusa alteração da ação quando indeferido")]
    public void Atualizar_LegalFixaAcaoIndeferimento_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: "Cota", CodigoDaNaturezaLegal: "COTA_RESERVADA", CodigoDaComposicaoDeVagas: "DENTRO_DA_RESERVA_LEGAL",
            ComposicaoOrigem: null, CodigoDaRegraDeRemanejamento: "SEGUE_ORDEM_DE_REMANEJAMENTO",
            RemanejamentoDestino: null, RemanejamentoPar: null, RemanejamentoFallback: null,
            RegraDeAplicabilidade: AplicavelA("COTA_RESERVADA"), CodigoDaAcaoQuandoIndeferido: "RETIRA_A_MODALIDADE",
            BaseLegal: "Lei 12.711/2012"));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.AcaoQuandoIndeferido.Should().BeNull();
    }

    [Theory(DisplayName = "Legal fixa recusa alteração de cada parâmetro de remanejamento")]
    [InlineData("LB_PPI", null, null)]
    [InlineData("AC", "AC", null)]
    [InlineData("AC", null, "AC")]
    public void Atualizar_LegalFixaParametros_Recusa(string? destino, string? par, string? destinoFinal)
    {
        Modalidade m = AcPcd();

        Result r = m.Atualizar(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: null, CodigoDaNaturezaLegal: "ACAO_AFIRMATIVA", CodigoDaComposicaoDeVagas: "RETIRADA_DE_OUTRA_MODALIDADE",
            ComposicaoOrigem: "AC", CodigoDaRegraDeRemanejamento: "DESTINO_UNICO",
            RemanejamentoDestino: destino, RemanejamentoPar: par, RemanejamentoFallback: destinoFinal,
            RegraDeAplicabilidade: RegraDaAcPcd(), CodigoDaAcaoQuandoIndeferido: null, BaseLegal: null));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.RemanejamentoParametros.Destino.Should().Be("AC");
        m.RemanejamentoParametros.Par.Should().BeNull();
        m.RemanejamentoParametros.DestinoFinal.Should().BeNull();
    }

    // A regra reenviada é outra instância com as mesmas condições: o teste distingue comparação por valor de
    // comparação por referência.
    [Fact(DisplayName = "Legal fixa aceita alteração de descrição e base legal com a estrutura reenviada idêntica")]
    public void Atualizar_LegalFixaSomenteRedacao_Aceita()
    {
        Modalidade m = CotaFederal(ComOpcaoPelasCotas(optou: true, ("BAIXA_RENDA", true)));

        Result r = m.Atualizar(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: "Cota — baixa renda, preto/pardo/indígena", CodigoDaNaturezaLegal: "COTA_RESERVADA",
            CodigoDaComposicaoDeVagas: "DENTRO_DA_RESERVA_LEGAL", ComposicaoOrigem: null, CodigoDaRegraDeRemanejamento: "SEGUE_ORDEM_DE_REMANEJAMENTO",
            RemanejamentoDestino: null, RemanejamentoPar: null, RemanejamentoFallback: null,
            RegraDeAplicabilidade: ComOpcaoPelasCotas(optou: true, ("BAIXA_RENDA", true)), CodigoDaAcaoQuandoIndeferido: null,
            BaseLegal: "Lei 12.711/2012 (red. Lei 14.723/2023)"));

        r.IsSuccess.Should().BeTrue();
        m.Descricao.Should().Be("Cota — baixa renda, preto/pardo/indígena");
        m.BaseLegal.Should().Be("Lei 12.711/2012 (red. Lei 14.723/2023)");
        m.NaturezaLegal.Should().Be(NaturezaLegal.CotaReservada);
    }

    [Fact(DisplayName = "Código inválido numa legal fixa reporta o erro de domínio, não a proteção")]
    public void Atualizar_LegalFixaCodigoInvalido_ReportaErroDeDominio()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: null, CodigoDaNaturezaLegal: "XPTO", CodigoDaComposicaoDeVagas: "DENTRO_DA_RESERVA_LEGAL",
            ComposicaoOrigem: null, CodigoDaRegraDeRemanejamento: "SEGUE_ORDEM_DE_REMANEJAMENTO",
            RemanejamentoDestino: null, RemanejamentoPar: null, RemanejamentoFallback: null,
            RegraDeAplicabilidade: AplicavelA("XPTO"), CodigoDaAcaoQuandoIndeferido: null, BaseLegal: null));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaInvalida,
            "não se compara com o cânone aquilo que sequer resolve para um valor de domínio");
    }
}
