namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

internal static class FidelidadeDoEnvelopeDeTeste
{
    internal static EnvelopeReidratado Conferir(ProcessoSeletivo processo, VersaoConfiguracao versao, SnapshotCanonico congelado)
    {
        EnvelopeReidratado envelope = ApoioDoEnvelopeCodec.Reidratado(versao);
        ProcessoSeletivo lido = ProcessoDoGrafoLidoDeTeste.Criar(processo, envelope.Configuracao);
        SnapshotCanonico recodificado = Recodificar(lido, versao, envelope);
        recodificado.Bytes.Should().Equal(congelado.Bytes);
        recodificado.SchemaVersion.Should().Be(congelado.SchemaVersion);
        recodificado.AlgoritmoHash.Should().Be(congelado.AlgoritmoHash);
        return envelope;
    }

    internal static SnapshotCanonico Recodificar(ProcessoSeletivo processo, VersaoConfiguracao versao, EnvelopeReidratado envelope)
    {
        Result<SnapshotCanonico> recodificado = CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(processo, envelope.Dados, envelope.HashDocumento, envelope.FusoHorario,
                envelope.Retificacao, envelope.Conformidade, envelope.DadosDeCatalogo));
        recodificado.IsSuccess.Should().BeTrue(recodificado.Error?.Message);
        return recodificado.Value!;
    }

    internal static void ConferirMutacao(string caminho, string valor, bool ordena)
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico original = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        byte[] alterados = Alterar(original, caminho, valor);
        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, alterados);
        EnvelopeReidratado envelope = ApoioDoEnvelopeCodec.Reidratado(versao);
        ProcessoSeletivo lido = ProcessoDoGrafoLidoDeTeste.Criar(processo, envelope.Configuracao);
        byte[] recodificados = Recodificar(lido, versao, envelope).Bytes;
        recodificados.Should().NotEqual(original.Bytes);
        if (!ordena)
        {
            recodificados.Should().Equal(alterados);
        }
    }

    private static byte[] Alterar(SnapshotCanonico original, string caminho, string valor)
    {
        JsonObject alterado = ApoioDoEnvelopeCodec.Envelope(original);
        foreach (string ocorrencia in caminho.Split('|'))
        {
            ApoioDoEnvelopeCodec.Mutar(alterado, ocorrencia, valor);
        }

        byte[] bytes = PerfilCanonicoV1.Instancia.Serializar(alterado);
        bytes.Should().NotEqual(original.Bytes);
        return bytes;
    }
}
