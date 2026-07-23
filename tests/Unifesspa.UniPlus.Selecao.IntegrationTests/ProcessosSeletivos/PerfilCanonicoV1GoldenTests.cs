namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// Vetores fixos do perfil <c>canonical-json/sha256@v1</c> — a forma de bytes do envelope.
/// </summary>
/// <remarks>
/// <para>
/// As golden fixtures do envelope congelam o <b>resultado</b> de uma configuração inteira: elas
/// pegam a mudança, mas não dizem qual regra mudou, e só cobrem os caracteres que aquela
/// configuração por acaso contém. Estes vetores congelam as <b>regras</b>, uma a uma, incluindo
/// as que nenhuma configuração de teste exercita — controles, emoji fora do BMP, sensíveis a
/// HTML, número não-inteiro.
/// </para>
/// <para>
/// Enquanto não há produção, esta forma evolui livremente: mudar uma regra reescreve estes
/// vetores. O que eles garantem é que a mudança é <b>deliberada</b> — um byte diferente sem um
/// vetor reescrito quebra o build.
/// </para>
/// </remarks>
public sealed class PerfilCanonicoV1GoldenTests
{
    private const char Barra = '\\';

    private static string Serializar(JsonObject payload) =>
        Encoding.UTF8.GetString(PerfilCanonicoV1.Instancia.Serializar(payload));

    private static string Uni(string hex) => $"{Barra}u{hex}";

    [Fact(DisplayName = "Perfil v1 — o identificador do algoritmo é o que a coluna algoritmo_hash guarda")]
    public void Algoritmo_EOIdentificadorPersistido()
    {
        PerfilCanonicoV1.Instancia.Algoritmo.Should().Be("canonical-json/sha256@v1");
    }

    [Fact(DisplayName = "Perfil v1 — chaves reordenadas recursivamente por comparação ordinal")]
    public void Chaves_ReordenadasRecursivamente()
    {
        JsonObject payload = new()
        {
            ["zeta"] = 1,
            ["Alfa"] = new JsonObject { ["b"] = 2, ["a"] = 1 },
            ["beta"] = new JsonArray(new JsonObject { ["y"] = 1, ["x"] = 0 }),
        };

        // Maiúsculas antes de minúsculas: comparação ORDINAL (por code unit), não de cultura.
        Serializar(payload).Should().Be("""{"Alfa":{"a":1,"b":2},"beta":[{"x":0,"y":1}],"zeta":1}""");
    }

    [Fact(DisplayName = "Perfil v1 — a ordem dos arrays é preservada: ela é semântica")]
    public void Arrays_PreservamOrdem()
    {
        JsonObject payload = new() { ["itens"] = new JsonArray("c", "a", "b") };

        Serializar(payload).Should().Be("""{"itens":["c","a","b"]}""");
    }

    [Fact(DisplayName = "Perfil v1 — null explícito é preservado, nunca omitido")]
    public void NullExplicito_EPreservado()
    {
        JsonObject payload = new() { ["numero"] = null, ["inicio"] = "2026-01-01" };

        Serializar(payload).Should().Be("""{"inicio":"2026-01-01","numero":null}""");
    }

    /// <summary>
    /// Aspa, contrabarra e os cinco controles com forma curta — a aspa dupla vira <c>\"</c>,
    /// não <c>"</c>. Todo o resto do escape é mínimo.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — aspa, contrabarra e os cinco controles em forma curta")]
    public void Escapes_FormasCurtas()
    {
        JsonObject payload = new()
        {
            ["aspas"] = "a\"b",
            ["contrabarra"] = "a\\b",
            ["backspace"] = "a\bb",
            ["tab"] = "a\tb",
            ["novaLinha"] = "a\nb",
            ["formFeed"] = "a\fb",
            ["retorno"] = "a\rb",
        };

        Serializar(payload).Should().Be(
            $$"""
            {"aspas":"a{{Barra}}"b","backspace":"a{{Barra}}bb","contrabarra":"a{{Barra}}{{Barra}}b","formFeed":"a{{Barra}}fb","novaLinha":"a{{Barra}}nb","retorno":"a{{Barra}}rb","tab":"a{{Barra}}tb"}
            """);
    }

    [Fact(DisplayName = "Perfil v1 — os demais controles saem em \\u00XX maiúsculo")]
    public void Escapes_ControlesRestantes()
    {
        JsonObject payload = new() { ["controles"] = "\u0001\u001F" };

        Serializar(payload).Should().Be($$"""{"controles":"{{Uni("0001")}}{{Uni("001F")}}"}""");
    }

    /// <summary>
    /// O envelope nunca é interpolado em HTML — os caracteres sensíveis a HTML saem
    /// <b>literais</b>, sem escape.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — caracteres sensíveis a HTML saem literais")]
    public void SensiveisAHtml_Literais()
    {
        JsonObject payload = new() { ["texto"] = "<a href='x'>1 & 2 + 3</a>" };

        Serializar(payload).Should().Be("""{"texto":"<a href='x'>1 & 2 + 3</a>"}""");
    }

    /// <summary>
    /// O não-ASCII do plano básico (acentuação, CJK) sai <b>literal</b> em UTF-8; o de fora do
    /// BMP (emoji) sai <b>escapado</b> como par substituto <c>\uXXXX\uXXXX</c> maiúsculo. Esse
    /// escape de caracteres astrais é uma particularidade do encoder relaxado do runtime — não
    /// uma escolha canônica —, e é por isso que fica congelado num vetor: um upgrade de runtime
    /// que o mudasse quebra o build, em vez de reescrever bytes em silêncio. Config de edital não
    /// carrega emoji; o vetor documenta a regra, não um caso real.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — não-ASCII do BMP literal; fora do BMP escapado como par substituto")]
    public void NaoAscii_BmpLiteral_AstralEscapado()
    {
        JsonObject payload = new()
        {
            ["acentuado"] = "residência",
            ["cjk"] = "文",
            ["foraDoBmp"] = "😀",
        };

        Serializar(payload).Should().Be(
            $$"""{"acentuado":"residência","cjk":"文","foraDoBmp":"{{Uni("D83D")}}{{Uni("DE00")}}"}""");
    }

    /// <summary>
    /// A normalização NFC é do <b>perfil</b>: a forma pré-composta e a sequência com acento
    /// combinante são o mesmo texto e viram os mesmos bytes.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — o perfil normaliza NFC: forma combinante e pré-composta batem")]
    public void Nfc_NormalizadoPeloPerfil()
    {
        const string PreComposto = "é";
        const string Combinante = "é";

        Serializar(new JsonObject { ["v"] = Combinante })
            .Should().Be(Serializar(new JsonObject { ["v"] = PreComposto }));
    }

    /// <summary>
    /// O perfil <b>preserva</b> o número — não o interpreta. A válvula de escape de
    /// obrigatoriedade (ADR-0058) carrega JSON arbitrário com decimais legítimos; impor "todo
    /// número é inteiro" recusaria configuração válida na emissão. O léxico do número parseado
    /// sobrevive; o número construído a partir de um valor CLR sai na sua forma canônica de
    /// runtime.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — número é preservado, não canonicalizado nem recusado")]
    public void Numeros_Preservados()
    {
        JsonObject payload = new()
        {
            ["criado"] = 42,
            ["grande"] = 9007199254740993L,
            ["decimalParseado"] = JsonNode.Parse("1.50"),
            ["expoenteParseado"] = JsonNode.Parse("1e2"),
            ["menosZeroParseado"] = JsonNode.Parse("-0"),
        };

        Serializar(payload).Should().Be(
            """{"criado":42,"decimalParseado":1.50,"expoenteParseado":1e2,"grande":9007199254740993,"menosZeroParseado":-0}""");
    }

    [Fact(DisplayName = "Perfil v1 — o digest é SHA-256 hex minúsculo sobre os bytes recebidos")]
    public void HashHex_ESha256DosBytes()
    {
        byte[] bytes = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["a"] = 1 });

        Encoding.UTF8.GetString(bytes).Should().Be("""{"a":1}""");
        PerfilCanonicoV1.Instancia.HashHex(bytes)
            .Should().Be("015abd7f5cc57a2dd94b7590f04ad8084273905ee33ec5cebeae62276a97f862");
    }
}
