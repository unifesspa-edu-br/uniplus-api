namespace Unifesspa.UniPlus.Governance.Contracts.UnitTests;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AreaCodigoTests
{
    // ─── Factory — caminhos felizes + normalização ─────────────────────────

    [Theory]
    [InlineData("CEPS", "CEPS")]
    [InlineData("ceps", "CEPS")]
    [InlineData("CePs", "CEPS")]
    [InlineData("  ceps  ", "CEPS")]
    [InlineData("CEPS_ADMIN", "CEPS_ADMIN")]
    [InlineData("_CEPS", "_CEPS")]
    [InlineData("AREA2", "AREA2")]
    public void From_DadoCodigoValido_DeveNormalizarParaUppercase(string entrada, string esperado)
    {
        Result<AreaCodigo> resultado = AreaCodigo.From(entrada);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Value.Should().Be(esperado);
    }

    [Theory]
    [InlineData("CE", "comprimento mínimo (2)")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ012345", "comprimento máximo (32)")]
    public void From_DadoCodigoNosLimitesDeComprimento_DeveRetornarSuccess(string entrada, string razao)
    {
        Result<AreaCodigo> resultado = AreaCodigo.From(entrada);

        resultado.IsSuccess.Should().BeTrue(razao);
    }

    // ─── Factory — validações ──────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void From_DadoCodigoVazioOuNulo_DeveRetornarFailure(string? entrada)
    {
        Result<AreaCodigo> resultado = AreaCodigo.From(entrada);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaCodigo.CodigoErroInvalido);
    }

    [Theory]
    [InlineData("A", "menos de 2 caracteres")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456", "mais de 32 caracteres")]
    [InlineData("1CEPS", "inicia por dígito")]
    [InlineData("9", "dígito único")]
    [InlineData("CEPS-ADMIN", "hífen não é permitido")]
    [InlineData("CEPS ADMIN", "espaço não é permitido")]
    [InlineData("CEPS.ADMIN", "ponto não é permitido")]
    [InlineData("ÁREA", "caractere não-ASCII")]
    public void From_DadoCodigoComFormatoInvalido_DeveRetornarFailure(string entrada, string razao)
    {
        Result<AreaCodigo> resultado = AreaCodigo.From(entrada);

        resultado.IsFailure.Should().BeTrue(razao);
        resultado.Error!.Code.Should().Be(AreaCodigo.CodigoErroInvalido, razao);
    }

    // ─── Igualdade de record struct ────────────────────────────────────────

    [Fact]
    public void From_DoisCodigosComMesmoValorAposNormalizacao_DevemSerIguais()
    {
        AreaCodigo a = AreaCodigo.From("CEPS").Value!;
        AreaCodigo b = AreaCodigo.From("ceps").Value!;

        a.Should().Be(b, "record struct compara por valor após normalização");
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    // ─── ToString + ordenação ──────────────────────────────────────────────

    [Fact]
    public void ToString_DeveRetornarOValorNormalizado()
    {
        AreaCodigo codigo = AreaCodigo.From("ceps").Value!;

        codigo.ToString().Should().Be("CEPS");
    }

    [Fact]
    public void CompareTo_DeveOrdenarOrdinalmentePorValor()
    {
        AreaCodigo ceps = AreaCodigo.From("CEPS").Value!;
        AreaCodigo crca = AreaCodigo.From("CRCA").Value!;

        ceps.CompareTo(crca).Should().BeNegative();
        crca.CompareTo(ceps).Should().BePositive();
        ceps.CompareTo(ceps).Should().Be(0);
    }

    [Fact]
    public void OperadoresDeComparacao_DevemRefletirOrdenacaoOrdinal()
    {
        AreaCodigo ceps = AreaCodigo.From("CEPS").Value!;
        AreaCodigo cepsBis = AreaCodigo.From("ceps").Value!;
        AreaCodigo crca = AreaCodigo.From("CRCA").Value!;

        (ceps < crca).Should().BeTrue();
        (ceps <= crca).Should().BeTrue();
        (crca > ceps).Should().BeTrue();
        (crca >= ceps).Should().BeTrue();
        (ceps <= cepsBis).Should().BeTrue("códigos iguais satisfazem <=");
        (ceps >= cepsBis).Should().BeTrue("códigos iguais satisfazem >=");
    }

    // ─── default(AreaCodigo) — estado inválido nunca produzido por From ────

    [Fact]
    public void Default_DeveTerValueNuloEToStringVazia()
    {
        AreaCodigo padrao = default;

        padrao.Value.Should().BeNull("default(AreaCodigo) nunca é produzido por From");
        padrao.ToString().Should().BeEmpty("ToString trata default graciosamente, sem NRE");
    }

    [Fact]
    public void Default_DeveOrdenarAntesDeQualquerCodigoValido()
    {
        AreaCodigo padrao = default;
        AreaCodigo ceps = AreaCodigo.From("CEPS").Value!;

        padrao.CompareTo(ceps).Should().BeNegative();
        padrao.CompareTo(default).Should().Be(0);
    }

    // ─── Serialização JSON — string plana ──────────────────────────────────

    [Fact]
    public void Json_DeveSerializarComoStringPlana()
    {
        AreaCodigo codigo = AreaCodigo.From("CEPS").Value!;

        string json = JsonSerializer.Serialize(codigo);

        json.Should().Be("\"CEPS\"");
    }

    [Fact]
    public void Json_RoundTrip_DevePreservarOValor()
    {
        AreaCodigo original = AreaCodigo.From("PLATAFORMA").Value!;

        string json = JsonSerializer.Serialize(original);
        AreaCodigo recuperado = JsonSerializer.Deserialize<AreaCodigo>(json);

        recuperado.Should().Be(original);
    }

    [Fact]
    public void Json_DeserializarStringInvalida_DeveLancarJsonException()
    {
        Action desserializar = () => JsonSerializer.Deserialize<AreaCodigo>("\"1invalido\"");

        desserializar.Should().Throw<JsonException>();
    }

    [Fact]
    public void Json_DeserializarTokenNaoString_DeveLancarJsonException()
    {
        Action desserializar = () => JsonSerializer.Deserialize<AreaCodigo>("123");

        desserializar.Should().Throw<JsonException>();
    }

    [Fact]
    public void Json_SerializarDefault_DeveLancarJsonException()
    {
        // default(AreaCodigo) tem Value null — estado inválido nunca produzido
        // por From. A escrita falha alto, simétrica com a leitura que rejeita
        // token null.
        Action serializar = () => JsonSerializer.Serialize(default(AreaCodigo));

        serializar.Should().Throw<JsonException>();
    }
}
