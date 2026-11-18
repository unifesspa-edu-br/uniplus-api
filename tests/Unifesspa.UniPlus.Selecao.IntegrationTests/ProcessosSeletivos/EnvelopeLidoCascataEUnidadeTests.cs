namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class EnvelopeLidoCascataEUnidadeTests
{
    [Fact]
    public void Reidratar_CascataCompleta_ConservaFallbackEDestinos()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo,
            CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);

        lido.Grafo.CascataRemanejamento.Should().NotBeNull();
        lido.Grafo.CascataRemanejamento!.FallbackCodigo.Should().Be("AC");
        lido.Grafo.CascataRemanejamento.Destinos.Should().HaveCount(56);
        lido.Grafo.CascataRemanejamento.Destinos.Select(d => (d.ModalidadeOrigemCodigo, d.Ordem, d.ModalidadeDestinoCodigo))
            .Should().BeEquivalentTo(processo.Cascata!.Destinos.Select(d => (d.ModalidadeOrigemCodigo, d.Ordem, d.ModalidadeDestinoCodigo)));
    }

    [Fact]
    public void Reidratar_CascataAusente_ConservaAAusencia()
    {
        (ProcessoSeletivo processo, _) = ApoioDoEnvelopeCodec.ProcessoDeInscricao("PS sem cascata");
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        ApoioDoEnvelopeCodec.Envelope(congelado)["cascataRemanejamento"]!["presente"]!.GetValue<bool>().Should().BeFalse();

        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo,
            CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);

        lido.Grafo.CascataRemanejamento.Should().BeNull();
    }

    [Fact]
    public void Recodificar_UnidadeAdministradora_ConservaIdentidadeESigla()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        ApoioDoEnvelopeCodec.Envelope(congelado)["identidadesUnidade"]!["administradora"]!["origemId"]!.GetValue<Guid>()
            .Should().Be(processo.UnidadeAdministradoraOrigemId);
        ApoioDoEnvelopeCodec.Envelope(congelado)["identidadesUnidade"]!["administradora"]!["sigla"]!.GetValue<string>()
            .Should().Be(processo.UnidadeAdministradora.Sigla);

        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo,
            CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        ProcessoSeletivo candidata = ProcessoDoGrafoLidoDeTeste.Criar(processo, lido.Grafo);
        candidata.UnidadeAdministradoraOrigemId.Should().Be(processo.UnidadeAdministradoraOrigemId);
        candidata.UnidadeAdministradora.Sigla.Should().Be(processo.UnidadeAdministradora.Sigla);
    }
}
