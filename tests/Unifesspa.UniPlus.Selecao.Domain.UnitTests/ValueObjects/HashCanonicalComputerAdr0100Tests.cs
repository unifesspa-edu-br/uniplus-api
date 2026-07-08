namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Extensões do <see cref="HashCanonicalComputer"/> para o contrato de
/// canonicalização do snapshot de publicação (ADR-0100, Story #759 T4 #785):
/// normalização NFC, decimais com escala declarada e arredondamento
/// half-even, instantes RFC 3339 sem fração, e a persistência dos bytes
/// canônicos como base do hash (<c>Canonicalizacao_HashReproduzivel</c> do
/// mapa de testes de #759).
/// </summary>
public sealed class HashCanonicalComputerAdr0100Tests
{
    [Fact(DisplayName = "NormalizeNfc: strings em NFD e NFC produzem o mesmo hash sobre os bytes persistidos")]
    public void NormalizeNfc_NfdVsNfc_ProduzMesmoHash()
    {
        // "café" com 'é' pré-composto (NFC) vs. 'e' + combining acute (NFD) —
        // bytes UTF-8 diferentes, mesmo texto visualmente.
        string nfc = "café";
        string nfd = "café";
        nfc.Should().NotBe(nfd, "pré-condição: os dois literais têm bytes distintos antes da normalização");

        JsonObject payloadNfc = new() { ["nome"] = HashCanonicalComputer.NormalizeNfc(nfc) };
        JsonObject payloadNfd = new() { ["nome"] = HashCanonicalComputer.NormalizeNfc(nfd) };

        byte[] bytesNfc = HashCanonicalComputer.ComputeSnapshotBytes(payloadNfc);
        byte[] bytesNfd = HashCanonicalComputer.ComputeSnapshotBytes(payloadNfd);

        HashCanonicalComputer.ComputeSha256Hex(bytesNfc).Should().Be(HashCanonicalComputer.ComputeSha256Hex(bytesNfd));
    }

    [Fact(DisplayName = "SerializeDecimalCanonical: representações decimais equivalentes na mesma escala produzem o mesmo hash")]
    public void SerializeDecimalCanonical_RepresentacoesEquivalentes_ProduzMesmoHash()
    {
        // 10.50 e 10.5 são o MESMO valor lógico na escala 2 — half-even não
        // deveria distingui-los uma vez serializados como string de largura fixa.
        string serializado1 = HashCanonicalComputer.SerializeDecimalCanonical(10.50m, escala: 2);
        string serializado2 = HashCanonicalComputer.SerializeDecimalCanonical(10.5m, escala: 2);

        serializado1.Should().Be(serializado2);
        serializado1.Should().Be("10.50");

        byte[] bytes1 = HashCanonicalComputer.ComputeSnapshotBytes(new JsonObject { ["valor"] = serializado1 });
        byte[] bytes2 = HashCanonicalComputer.ComputeSnapshotBytes(new JsonObject { ["valor"] = serializado2 });
        HashCanonicalComputer.ComputeSha256Hex(bytes1).Should().Be(HashCanonicalComputer.ComputeSha256Hex(bytes2));
    }

    [Fact(DisplayName = "SerializeDecimalCanonical: arredondamento half-even em valores de meio de intervalo exato")]
    public void SerializeDecimalCanonical_HalfEven_Arredonda()
    {
        // Literais decimal exatos (não double) — half-even arredonda para o
        // dígito par mais próximo em empates exatos: 2.5 → 2, 3.5 → 4.
        HashCanonicalComputer.SerializeDecimalCanonical(2.5m, escala: 0).Should().Be("2");
        HashCanonicalComputer.SerializeDecimalCanonical(3.5m, escala: 0).Should().Be("4");
        HashCanonicalComputer.SerializeDecimalCanonical(1.005m, escala: 2).Should().Be("1.00");
        HashCanonicalComputer.SerializeDecimalCanonical(1.015m, escala: 2).Should().Be("1.02");
    }

    [Fact(DisplayName = "SerializeInstantCanonical: instante com e sem fração de segundo produzem o mesmo hash")]
    public void SerializeInstantCanonical_ComESemFracao_ProduzMesmoHash()
    {
        DateTimeOffset comFracao = new(2026, 7, 8, 10, 30, 45, 123, TimeSpan.Zero);
        DateTimeOffset semFracao = new(2026, 7, 8, 10, 30, 45, 0, TimeSpan.Zero);

        string serializadoComFracao = HashCanonicalComputer.SerializeInstantCanonical(comFracao);
        string serializadoSemFracao = HashCanonicalComputer.SerializeInstantCanonical(semFracao);

        serializadoComFracao.Should().Be(serializadoSemFracao,
            "granularidade de segundo — a fração de milissegundo é truncada, não arredondada para o segundo seguinte");
        serializadoComFracao.Should().Be("2026-07-08T10:30:45Z");
    }

    [Fact(DisplayName = "ComputeSnapshotBytes + ComputeSha256Hex: mesma configuração produz sempre o mesmo hash")]
    public void ComputeSnapshotBytesEComputeSha256Hex_MesmaConfiguracao_HashReproduzivel()
    {
        JsonObject payload = new()
        {
            ["etapas"] = new JsonArray(new JsonObject { ["nome"] = "Prova Objetiva", ["peso"] = "1.00" }),
            ["bonusRegional"] = new JsonObject { ["presente"] = false },
        };

        byte[] bytes1 = HashCanonicalComputer.ComputeSnapshotBytes(payload.DeepClone().AsObject());
        byte[] bytes2 = HashCanonicalComputer.ComputeSnapshotBytes(payload.DeepClone().AsObject());

        string hash1 = HashCanonicalComputer.ComputeSha256Hex(bytes1);
        string hash2 = HashCanonicalComputer.ComputeSha256Hex(bytes2);

        hash1.Should().Be(hash2);
        HashCanonicalComputer.IsValidHashShape(hash1).Should().BeTrue();
    }

    [Fact(DisplayName = "ComputeSnapshotBytes: alterar qualquer valor de negócio muda o hash")]
    public void ComputeSnapshotBytes_MudancaDeValor_MudaHash()
    {
        JsonObject payloadBase = new() { ["etapas"] = new JsonArray(new JsonObject { ["nome"] = "Prova Objetiva" }) };
        JsonObject payloadMudado = new() { ["etapas"] = new JsonArray(new JsonObject { ["nome"] = "Prova Discursiva" }) };

        string hashBase = HashCanonicalComputer.ComputeSha256Hex(HashCanonicalComputer.ComputeSnapshotBytes(payloadBase));
        string hashMudado = HashCanonicalComputer.ComputeSha256Hex(HashCanonicalComputer.ComputeSnapshotBytes(payloadMudado));

        hashMudado.Should().NotBe(hashBase);
    }

    [Fact(DisplayName = "ComputeSnapshotBytes: bytes canônicos são JSON UTF-8 válido, re-hasheável de volta")]
    public void ComputeSnapshotBytes_BytesSaoJsonUtf8Valido()
    {
        JsonObject payload = new() { ["chave"] = "valor" };
        byte[] bytes = HashCanonicalComputer.ComputeSnapshotBytes(payload);

        string texto = Encoding.UTF8.GetString(bytes);
        JsonNode? reparsed = JsonNode.Parse(texto);

        reparsed.Should().NotBeNull();
        reparsed!["chave"]!.GetValue<string>().Should().Be("valor");

        // Re-hashear os bytes lidos "de volta" bate com o hash original —
        // ADR-0100 §Confirmação (base do teste de integração Snapshot_HashConfereAppEBanco).
        byte[] bytesRelidos = Encoding.UTF8.GetBytes(texto);
        HashCanonicalComputer.ComputeSha256Hex(bytesRelidos).Should().Be(HashCanonicalComputer.ComputeSha256Hex(bytes));
    }
}
