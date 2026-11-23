namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class EnvelopeLidoCulturaEIdentificadorTests
{
    [Fact]
    public void Reidratar_CulturaPtBr_ReproduzOsBytes()
    {
        CultureInfo anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator.Should().Be(",");
            ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
            SnapshotCanonico congelado = CorpusEnvelope.Canonicalizador.Canonicalizar(CorpusEnvelopeEntrada.Entrada(processo));
            FidelidadeDoEnvelopeDeTeste.Conferir(processo, CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public void Reidratar_IdentificadorLegivelNulo_PreservaAAusencia()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        IdentificadoresDeTeste.Retirar(processo);
        SnapshotCanonico congelado = CorpusEnvelope.Canonicalizador.Canonicalizar(CorpusEnvelopeEntrada.Entrada(processo));
        ApoioDoEnvelopeCodec.Envelope(congelado)["identificadorLegivel"].Should().BeNull();
        processo.DefinirIdentificadorLegivel(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.IdentificadorLegivel.Criar("processo-com-identificador").Value).IsSuccess.Should().BeTrue();
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo, CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        lido.Configuracao.IdentificadorLegivel.Should().BeNull();
    }
}
