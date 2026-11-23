namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class EnvelopeLidoFidelidadeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reidratar_ConfiguracaoRica_ReproduzTodosOsBytes(bool comRegras)
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico(comRegrasDeComprovacao: comRegras);
        SnapshotCanonico congelado = CorpusEnvelope.Canonicalizador.Canonicalizar(CorpusEnvelopeEntrada.Entrada(processo));
        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);

        FidelidadeDoEnvelopeDeTeste.Conferir(processo, versao, congelado);
    }
    [Fact]
    public void Reidratar_DecimaisDasConfiguracoes_PreservaValores()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Canonicalizador.Canonicalizar(CorpusEnvelopeEntrada.Entrada(processo));
        EnvelopeReidratado envelope = FidelidadeDoEnvelopeDeTeste.Conferir(processo,
            CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        ProcessoSeletivo lido = ProcessoDoGrafoLidoDeTeste.Criar(processo, envelope.Configuracao);

        lido.BonusRegional!.Fator.Should().Be(1.2000m);
        lido.BonusRegional.Teto.Should().Be(95.5000m);
        lido.ConfiguracaoTaxaInscricao!.Valor.Should().Be(150.00m);
        lido.DistribuicaoVagas.Select(d => d.PercentualDaReservaLegal).Should().Contain(0.7500m);
    }

}
