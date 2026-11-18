namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class EstadoDaInscricaoTests
{
    [Fact]
    public void Inicial_MesmosValores_PossuiIgualdadePorValor()
    {
        EstadoDaInscricao primeiro = EstadoDaInscricao.Inicial;
        EstadoDaInscricao segundo = EstadoDaInscricao.Inicial;
        primeiro.Should().Be(segundo);
        primeiro.GetHashCode().Should().Be(segundo.GetHashCode());
        typeof(EstadoDaInscricao).GetConstructors().Should().BeEmpty();
        typeof(EstadoDaInscricao).GetProperties().Should().OnlyContain(propriedade => !propriedade.CanWrite);
        typeof(EstadoDaInscricao).GetMethod("SubmeterAposConferencia").Should().BeNull();
        typeof(EstadoDaInscricao).GetMethod("CancelarAposConferencia").Should().BeNull();
    }

    [Fact]
    public void Submeter_Rascunho_PreservaOriginalEAtribuiNumero()
    {
        EstadoDaInscricao inicial = EstadoDaInscricao.Inicial;
        EstadoDaInscricao enviada = inicial.SubmeterAposConferencia("123");
        inicial.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        enviada.Situacao.Should().Be(SituacaoInscricao.Submetida);
        enviada.NumeroInscricao.Should().Be("123");
    }

    [Fact]
    public void Submeter_InscricaoEnviada_PreservaNumeroOriginal()
    {
        EstadoDaInscricao enviada = EstadoDaInscricao.Inicial.SubmeterAposConferencia("123");
        enviada.SubmeterAposConferencia("456").NumeroInscricao.Should().Be("123");
    }

    [Fact]
    public void Cancelar_InscricaoEnviada_PreservaNumeroEOriginal()
    {
        EstadoDaInscricao enviada = EstadoDaInscricao.Inicial.SubmeterAposConferencia("123");
        CancelamentoDaInscricao cancelamento = CancelamentoDaInscricao.Registrar("motivo", "conta", DateTimeOffset.UnixEpoch).Value!;
        EstadoDaInscricao cancelada = enviada.CancelarAposConferencia(cancelamento);
        enviada.Cancelamento.Should().BeNull();
        cancelada.Situacao.Should().Be(SituacaoInscricao.Cancelada);
        cancelada.NumeroInscricao.Should().Be("123");
        cancelada.Cancelamento.Should().Be(cancelamento);
    }
}
