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
            ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
            SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo));
            FidelidadeDoEnvelopeDeTeste.Conferir(processo, ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public void Reidratar_IdentificadorLegivelNulo_PreservaAAusencia()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        IdentificadoresDeTeste.Retirar(processo);
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo));
        ApoioDoEnvelopeCodec.Envelope(congelado)["identificadorLegivel"].Should().BeNull();
        processo.DefinirIdentificadorLegivel(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.IdentificadorLegivel.Criar("processo-com-identificador").Value).IsSuccess.Should().BeTrue();
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo, ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        lido.Configuracao.IdentificadorLegivel.Should().BeNull();
    }
}
