namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Conferencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Conferência do registro de codecs (Story #859 CA-08; ADR-0110 D1).
/// </summary>
public sealed class RegistroCodecsEnvelopeTests
{
    private static readonly RegistroCodecsEnvelope Registro = new();

    [Fact(DisplayName = "CA-08 — a versão de emissão corrente tem codec COMPLETO (encoder e decoder)")]
    public void EmissaoCorrente_TemCodecCompleto()
    {
        CapacidadeCodec? corrente = Registro.Capacidades
            .SingleOrDefault(c => c.SchemaVersion == Registro.SchemaVersionDeEmissaoCorrente);

        corrente.Should().NotBeNull(
            $"a versão de emissão corrente ('{Registro.SchemaVersionDeEmissaoCorrente}') tem de estar no registro");

        corrente!.Reidratavel.Should().BeTrue(
            "a versão que o sistema emite tem de saber ser lida de volta e recanonicalizada: " +
            "sem decoder, o descarte de tudo o que ela congelar seria irreversível; " +
            "sem encoder, seria irreverificável.");
    }

    [Fact(DisplayName = "CA-08 — toda versão do registro declara as suas capacidades; a que não reidrata declara o MOTIVO")]
    public void TodaVersao_DeclaraCapacidades()
    {
        Registro.Capacidades.Should().NotBeEmpty();

        foreach (CapacidadeCodec capacidade in Registro.Capacidades)
        {
            if (capacidade.Reidratavel)
            {
                continue;
            }

            capacidade.MotivoDaRecusa.Should().NotBeNullOrWhiteSpace(
                $"a versão '{capacidade.SchemaVersion}' está no registro mas não reidrata — e uma versão conhecida " +
                "que falhasse sem dizer por quê seria indistinguível de uma desconhecida. O operador diante de um " +
                "descarte que falhou precisa saber qual das duas é.");
        }
    }

    [Fact(DisplayName = "CA-08 — todo codec com encoder emite a SUA versão, não a corrente")]
    public void CodecComEncoder_EmiteAPropriaVersao()
    {
        IReadOnlyList<CapacidadeCodec> comEncoder = [.. Registro.Capacidades.Where(static c => c.TemEncoder)];

        comEncoder.Should().NotBeEmpty();

        foreach (CapacidadeCodec capacidade in comEncoder)
        {
            SnapshotCanonico snapshot = Registro
                .Recodificar(capacidade.SchemaVersion, ExemploDeConfiguracao.Entrada())
                .Value!;

            snapshot.SchemaVersion.Should().Be(capacidade.SchemaVersion,
                $"o codec '{capacidade.SchemaVersion}' tem de emitir a forma dele — se ele delega ao canonicalizador " +
                "corrente e este mudou de versão, o encoder daquela forma precisa ser CONGELADO no codec.");
        }
    }

    /// <summary>
    /// O registro pode estar perfeitamente coerente consigo mesmo e ainda assim divergir do
    /// que a <b>produção grava</b>: <c>Publicar</c> e <c>Retificar</c> injetam o
    /// <see cref="ISnapshotPublicacaoCanonicalizer"/>, não o registro. Este teste amarra os
    /// dois — sem ele, uma mudança de versão no canonicalizador passaria a congelar versões que o registro
    /// declara não emitir, e ninguém veria até o primeiro descarte.
    /// </summary>
    [Fact(DisplayName = "CA-08 — o que a PRODUÇÃO emite é a versão de emissão corrente do registro")]
    public void EmissaoDeProducao_BateComORegistro()
    {
        SnapshotCanonico emitido = ((ISnapshotPublicacaoCanonicalizer)new SnapshotPublicacaoCanonicalizer())
            .Canonicalizar(ExemploDeConfiguracao.Entrada());

        emitido.SchemaVersion.Should().Be(Registro.SchemaVersionDeEmissaoCorrente,
            "o canonicalizador que Publicar/Retificar injetam é o que de fato congela as versões. Se ele emite uma " +
            "forma que o registro não declara como corrente, o registro está mentindo sobre a produção.");

        CapacidadeCodec corrente = Registro.Capacidades.Single(c => c.SchemaVersion == emitido.SchemaVersion);
        corrente.Reidratavel.Should().BeTrue();

        Registro.Recodificar(emitido.SchemaVersion, ExemploDeConfiguracao.Entrada()).Value!.AlgoritmoHash
            .Should().Be(emitido.AlgoritmoHash, "o algoritmo de hash também é parte da evidência, não detalhe");
    }
}
