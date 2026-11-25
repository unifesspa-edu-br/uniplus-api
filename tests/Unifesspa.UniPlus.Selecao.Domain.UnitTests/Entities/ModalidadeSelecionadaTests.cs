namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ModalidadeSelecionadaTests
{
    [Theory]
    [InlineData(NaturezaLegalModalidade.Ampla, null, true)]
    [InlineData(NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento, false)]
    [InlineData(NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.DestinoUnico, false)]
    [InlineData(NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Cruzado, false)]
    [InlineData(NaturezaLegalModalidade.AcaoAfirmativa, null, false)]
    [InlineData(NaturezaLegalModalidade.AcaoAfirmativa, RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento, false)]
    [InlineData(NaturezaLegalModalidade.AcaoAfirmativa, RegraRemanejamentoModalidade.DestinoUnico, true)]
    [InlineData(NaturezaLegalModalidade.AcaoAfirmativa, RegraRemanejamentoModalidade.Cruzado, true)]
    [InlineData(NaturezaLegalModalidade.CotaReservada, null, true)]
    [InlineData(NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento, true)]
    [InlineData(NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.DestinoUnico, true)]
    [InlineData(NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.Cruzado, true)]
    public void RemanejamentoCoerenteComANatureza_NaturezaERegra_ConfereCoerencia(
        NaturezaLegalModalidade natureza, RegraRemanejamentoModalidade? regra, bool esperado)
    {
        ModalidadeSelecionada.RemanejamentoCoerenteComANatureza(natureza, regra).Should().Be(esperado);
    }

    private static Result<ModalidadeSelecionada> CriarAmpla() => ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { Descricao = "Ampla concorrência" });

    [Fact(DisplayName = "Criar sem a identidade da modalidade de origem recusa")]
    public void Criar_SemIdentidadeDeOrigem_Recusa()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { Id = Guid.Empty, Descricao = "Ampla concorrência" });

        resultado.IsFailure.Should().BeTrue(
            "a identidade da origem é o que decide se esta oferta é a modalidade que uma regra legal exige");
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.ModalidadeOrigemIdObrigatorio");
        resultado.Error!.Message.Should().Be("Identidade da modalidade de origem é obrigatória.");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar modalidade ampla (residual, sem remanejamento) tem sucesso")]
    public void Criar_Ampla_Sucesso()
    {
        Result<ModalidadeSelecionada> resultado = CriarAmpla();

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.NaturezaLegal.Should().Be(NaturezaLegalModalidade.Ampla);
        resultado.Value.RegraRemanejamento.Should().Be(null);
    }

    [Fact(DisplayName = "Criar modalidade de cota reservada com SEGUE_ORDEM_DE_REMANEJAMENTO tem sucesso")]
    public void Criar_CotaReservadaComCascata_Sucesso()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal) with { Descricao = "Baixa renda PPI", AcaoQuandoIndeferido = "RECLASSIFICA_AC", BaseLegal = "Lei 12.711/2012 art. 3º" });

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar modalidade RETIRADA_DE_OUTRA_MODALIDADE com origem tem sucesso")]
    public void Criar_RetiradaDeOutraModalidadeComOrigem_Sucesso()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("V", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade) with { Descricao = "PcD retirada da AC", ComposicaoOrigemCodigo = "AC", BaseLegal = "Edital SiSU 4.1.2" });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ComposicaoOrigemCodigo.Should().Be("AC");
    }

    [Fact(DisplayName = "Criar modalidade com remanejamento CRUZADO completo tem sucesso")]
    public void Criar_Cruzado_Sucesso()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("IND", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal) with { Descricao = "Indígena (PSIQ)", RegraRemanejamento = RegraRemanejamentoModalidade.Cruzado, RemanejamentoPar = "QUIL", RemanejamentoFallback = "AC", BaseLegal = "Res. 532/2021" });

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com código vazio falha")]
    public void Criar_CodigoVazio_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { BaseLegal = "base" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.CodigoObrigatorio");
        resultado.Error!.Message.Should().Be("Código da modalidade é obrigatório.");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar com natureza legal Nenhuma falha")]
    public void Criar_NaturezaLegalNenhuma_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { NaturezaLegal = null, BaseLegal = "base" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.NaturezaLegalObrigatoria");
        resultado.Error!.Message.Should().Be("Natureza legal da modalidade é obrigatória.");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar com composição de vagas Nenhuma falha")]
    public void Criar_ComposicaoVagasNenhuma_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { ComposicaoVagas = null, BaseLegal = "base" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.ComposicaoVagasObrigatoria");
        resultado.Error!.Message.Should().Be("Composição de vagas da modalidade é obrigatória.");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar com base legal vazia falha")]
    public void Criar_BaseLegalVazia_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { BaseLegal = "" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.BaseLegalObrigatoria");
        resultado.Error!.Message.Should().Be("Base legal da modalidade é obrigatória.");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar RETIRADA_DE_OUTRA_MODALIDADE sem código de origem falha")]
    public void Criar_RetiradaDeOutraModalidadeSemOrigem_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("V", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade) with { BaseLegal = "base" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.ComposicaoOrigemObrigatoria");
        resultado.Error!.Message.Should().Be("Modalidade com composição RETIRADA_DE_OUTRA_MODALIDADE exige o código da modalidade de origem.");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar com origem informada fora de RETIRADA_DE_OUTRA_MODALIDADE falha")]
    public void Criar_ComposicaoOrigemIndevida_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { ComposicaoOrigemCodigo = "X", BaseLegal = "base" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.ComposicaoOrigemIndevida");
        resultado.Error!.Message.Should().Be("A modalidade só declara código de origem quando a composição de vagas é RETIRADA_DE_OUTRA_MODALIDADE.");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar cota reservada sem SEGUE_ORDEM_DE_REMANEJAMENTO falha")]
    public void Criar_CotaReservadaSemCascata_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal) with { RegraRemanejamento = RegraRemanejamentoModalidade.DestinoUnico, RemanejamentoDestino = "AC", BaseLegal = "base" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.CotaReservadaExigeCascata");
        resultado.Error!.Message.Should().Be("A modalidade é cota reservada da Lei 12.711 e deve seguir a ordem legal de remanejamento (SEGUE_ORDEM_DE_REMANEJAMENTO).");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar DESTINO_UNICO sem destino falha")]
    public void Criar_DestinoUnicoSemDestino_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("X", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal) with { RegraRemanejamento = RegraRemanejamentoModalidade.DestinoUnico, BaseLegal = "base" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.RemanejamentoDestinoObrigatorio");
        resultado.Error!.Message.Should().Be("Modalidade com remanejamento DESTINO_UNICO exige o destino.");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar CRUZADO sem par falha — o par é o que define o cruzamento")]
    public void Criar_CruzadoSemPar_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("IND", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal) with { RegraRemanejamento = RegraRemanejamentoModalidade.Cruzado, RemanejamentoFallback = "AC", BaseLegal = "base" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.RemanejamentoCruzadoIncompleto");
        resultado.Error!.Message.Should().Be("Modalidade com remanejamento CRUZADO exige a modalidade par.");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar CRUZADO com par e sem fallback tem sucesso — certame isolado não tem destino final")]
    public void Criar_CruzadoSemFallback_Sucesso()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC_I", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal) with { Descricao = "Vaga por acréscimo — candidato indígena (PSIQ)", RegraRemanejamento = RegraRemanejamentoModalidade.Cruzado, RemanejamentoPar = "AC_Q", BaseLegal = "Res. 532/2021" });

        resultado.IsSuccess.Should().BeTrue(
            "o PSIQ não oferta ampla concorrência, então a vaga que nem AC_I nem AC_Q preencherem "
            + "não tem destino final — e a ausência de fallback é como o snapshot diz isso");
        resultado.Value!.RemanejamentoFallback.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com destino informado fora de DESTINO_UNICO falha")]
    public void Criar_RemanejamentoDestinoIndevido_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { RemanejamentoDestino = "X", BaseLegal = "base" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.RemanejamentoDestinoIndevido");
        resultado.Error!.Message.Should().Be("A modalidade só declara destino de remanejamento quando a regra é DESTINO_UNICO.");
        resultado.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar com par ou destino final informados fora de CRUZADO falha")]
    public void Criar_RemanejamentoCruzadoIndevido_Falha()
    {
        Result<ModalidadeSelecionada> resultado = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { RemanejamentoPar = "X", RemanejamentoFallback = "Y", BaseLegal = "base" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.RemanejamentoCruzadoIndevido");
        resultado.Error!.Message.Should().Be("A modalidade só declara par ou destino final de remanejamento quando a regra é CRUZADO.");
        resultado.Errors.Should().ContainSingle();
    }

}
