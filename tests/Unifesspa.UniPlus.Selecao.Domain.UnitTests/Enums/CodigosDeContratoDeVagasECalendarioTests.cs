namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CodigosDeContratoDeVagasECalendarioTests
{
    [Fact]
    public void NaturezaLegalModalidade_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<NaturezaLegalModalidade>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (NaturezaLegalModalidade.CotaReservada, "COTA_RESERVADA", "Cota reservada"),
                (NaturezaLegalModalidade.Ampla, "AMPLA", "Ampla concorrência"),
                (NaturezaLegalModalidade.AcaoAfirmativa, "ACAO_AFIRMATIVA", "Ação afirmativa"));

    [Fact]
    public void ComposicaoVagasModalidade_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<ComposicaoVagasModalidade>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (ComposicaoVagasModalidade.RestanteDasVagasOfertadas, "RESTANTE_DAS_VAGAS_OFERTADAS", "Restante das vagas ofertadas"),
                (ComposicaoVagasModalidade.DentroDaReservaLegal, "DENTRO_DA_RESERVA_LEGAL", "Dentro da reserva legal"),
                (ComposicaoVagasModalidade.RetiradaDeOutraModalidade, "RETIRADA_DE_OUTRA_MODALIDADE", "Retirada de outra modalidade"),
                (ComposicaoVagasModalidade.SuplementarAoTotal, "SUPLEMENTAR_AO_TOTAL", "Suplementar ao total"));

    [Fact]
    public void RegraRemanejamentoModalidade_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<RegraRemanejamentoModalidade>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento, "SEGUE_ORDEM_DE_REMANEJAMENTO", "Segue a ordem de remanejamento"),
                (RegraRemanejamentoModalidade.DestinoUnico, "DESTINO_UNICO", "Destino único"),
                (RegraRemanejamentoModalidade.Cruzado, "CRUZADO", "Remanejamento cruzado"));
}
