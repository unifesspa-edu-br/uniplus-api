namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

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
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);

        FidelidadeDoEnvelopeDeTeste.Conferir(processo, versao, congelado);
    }
}
