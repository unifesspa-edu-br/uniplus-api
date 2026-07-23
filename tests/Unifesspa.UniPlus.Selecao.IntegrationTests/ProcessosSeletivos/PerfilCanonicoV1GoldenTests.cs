namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// Vetores fixos do perfil <c>canonical-json/sha256@v1</c> — as regras de bytes sob as quais
/// todo envelope das versões <c>1.0</c> a <c>1.4</c> foi congelado.
/// </summary>
/// <remarks>
/// <para>
/// As golden fixtures do envelope congelam o <b>resultado</b> de uma configuração inteira: elas
/// pegam a mudança, mas não dizem qual regra mudou, e só cobrem os caracteres que aquela
/// configuração por acaso contém. Estes vetores congelam as <b>regras</b>, uma a uma, incluindo
/// as que nenhuma configuração de teste exercita hoje — controles, emoji fora do BMP,
/// caracteres sensíveis a HTML.
/// </para>
/// <para>
/// Boa parte do que está congelado aqui nunca foi decisão deliberada: é o comportamento padrão
/// do <c>Utf8JsonWriter</c> sem encoder próprio. Escrito ou não, é o que os certames publicados
/// têm gravado — e o que um perfil posterior, com escapes próprios, não pode alterar
/// retroativamente. É por isso que estes vetores vêm antes de existir um segundo perfil, e não
/// depois.
/// </para>
/// <para>
/// Os literais esperados montam cada escape a partir de <see cref="Barra"/> em vez de o
/// digitarem: uma sequência de escape escrita à mão no meio de um literal é fácil de errar em
/// silêncio, e um vetor errado que passe verde não congela nada.
/// </para>
/// </remarks>
public sealed class PerfilCanonicoV1GoldenTests
{
    /// <summary>A contrabarra que abre toda sequência de escape JSON.</summary>
    private const char Barra = '\\';

    private static string Serializar(JsonObject payload) =>
        Encoding.UTF8.GetString(PerfilCanonicoV1.Instancia.Serializar(payload));

    /// <summary>Escape <c>\uXXXX</c> na forma que o perfil emite — hex de quatro dígitos.</summary>
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

        // Maiúsculas antes de minúsculas: é comparação ORDINAL (por code unit), não alfabética
        // de cultura. 'A' (0x41) vem antes de 'b' (0x62) e de 'z' (0x7A).
        Serializar(payload).Should().Be("""{"Alfa":{"a":1,"b":2},"beta":[{"x":0,"y":1}],"zeta":1}""");
    }

    [Fact(DisplayName = "Perfil v1 — a ordem dos arrays é preservada: ela é semântica")]
    public void Arrays_PreservamOrdem()
    {
        JsonObject payload = new() { ["itens"] = new JsonArray("c", "a", "b") };

        Serializar(payload).Should().Be("""{"itens":["c","a","b"]}""");
    }

    /// <summary>
    /// O envelope distingue "campo ausente" de "campo presente e vazio" — o número do ato ainda
    /// não atribuído é <c>null</c> explícito em <c>periodo.numero</c>. Um perfil que omitisse a
    /// chave faria dois estados de negócio distintos colapsarem nos mesmos bytes.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — null explícito é preservado, nunca omitido")]
    public void NullExplicito_EPreservado()
    {
        JsonObject payload = new() { ["numero"] = null, ["inicio"] = "2026-01-01" };

        Serializar(payload).Should().Be("""{"inicio":"2026-01-01","numero":null}""");
    }

    /// <summary>
    /// Contrabarra e os cinco controles com forma curta em JSON saem curtos; a <b>aspa dupla,
    /// não</b> — vira <c>"</c>. É assimétrico e contraintuitivo o bastante para que só um
    /// vetor fixo o mantenha honesto: um perfil posterior que emitisse <c>\"</c>, a forma que
    /// qualquer leitor esperaria, produziria bytes distintos para o mesmo texto.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — contrabarra e controles em forma curta, mas a aspa dupla vira \\u0022")]
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
            {"aspas":"a{{Uni("0022")}}b","backspace":"a{{Barra}}bb","contrabarra":"a{{Barra}}{{Barra}}b","formFeed":"a{{Barra}}fb","novaLinha":"a{{Barra}}nb","retorno":"a{{Barra}}rb","tab":"a{{Barra}}tb"}
            """);
    }

    [Fact(DisplayName = "Perfil v1 — os demais controles saem em \\uXXXX com hex MAIÚSCULO")]
    public void Escapes_ControlesRestantes()
    {
        JsonObject payload = new() { ["controles"] = "\u0001\u001F" };

        Serializar(payload).Should().Be($$"""{"controles":"{{Uni("0001")}}{{Uni("001F")}}"}""");
    }

    /// <summary>
    /// O encoder padrão do runtime escapa o que seria perigoso interpolar em HTML. O envelope
    /// nunca é interpolado em HTML — mas os bytes publicados são estes, e é isso que conta.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — caracteres sensíveis a HTML são escapados")]
    public void Escapes_SensiveisAHtml()
    {
        JsonObject payload = new() { ["texto"] = "<a href='x'>1 & 2 + 3</a>" };

        Serializar(payload).Should().Be(
            $$"""
            {"texto":"{{Uni("003C")}}a href={{Uni("0027")}}x{{Uni("0027")}}{{Uni("003E")}}1 {{Uni("0026")}} 2 {{Uni("002B")}} 3{{Uni("003C")}}/a{{Uni("003E")}}"}
            """);
    }

    /// <summary>
    /// Tudo fora do latim básico sai escapado — inclusive toda letra acentuada do português.
    /// A fixture <c>envelope-1.4.json</c> carrega exatamente isto.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — não-ASCII escapado em \\uXXXX maiúsculo, inclusive par substituto")]
    public void Escapes_NaoAscii()
    {
        JsonObject payload = new()
        {
            ["acentuado"] = "residência",
            ["cjk"] = "文",
            ["foraDoBmp"] = "😀",
        };

        Serializar(payload).Should().Be(
            $$"""
            {"acentuado":"resid{{Uni("00EA")}}ncia","cjk":"{{Uni("6587")}}","foraDoBmp":"{{Uni("D83D")}}{{Uni("DE00")}}"}
            """);
    }

    /// <summary>
    /// Normalizar é do <b>caller</b>, não do perfil: o perfil serializa o nó que recebe. Sem
    /// <see cref="HashCanonicalComputer.NormalizeNfc"/> antes, a forma pré-composta e a
    /// sequência com acento combinante são o mesmo texto com bytes — e hashes — diferentes.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — NFC é responsabilidade do caller: sequência combinante produz bytes distintos")]
    public void Nfc_ENoCaller_NaoNoPerfil()
    {
        const string PreComposto = "é";          // é
        const string Combinante = "e\u0301";          // e + acento agudo combinante

        string semNormalizar = Serializar(new JsonObject { ["v"] = Combinante });
        string preComposto = Serializar(new JsonObject { ["v"] = PreComposto });

        semNormalizar.Should().NotBe(preComposto,
            "o perfil não normaliza — quem monta o payload é que chama NormalizeNfc");

        string normalizadoPeloCaller = Serializar(
            new JsonObject { ["v"] = HashCanonicalComputer.NormalizeNfc(Combinante) });

        normalizadoPeloCaller.Should().Be(preComposto);
    }

    /// <summary>
    /// Os números do envelope são, por decisão do encoder, quase todos <b>strings</b> — decimal
    /// de negócio passa por <see cref="HashCanonicalComputer.SerializeDecimalCanonical"/> com
    /// escala declarada. Os que sobram como <c>number</c> JSON (ordem, quantidade, contagens)
    /// entram aqui: a forma com que o perfil v1 os escreve também é contrato.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — forma dos números que ficam como number JSON")]
    public void Numeros_FormaCongelada()
    {
        JsonObject payload = new()
        {
            ["inteiro"] = 42,
            ["negativo"] = -7,
            ["zero"] = 0,
            ["decimalComZeroFinal"] = JsonValue.Create(1.50m),
            ["doubleInteiro"] = JsonValue.Create(1.0d),
        };

        Serializar(payload).Should().Be(
            """{"decimalComZeroFinal":1.50,"doubleInteiro":1,"inteiro":42,"negativo":-7,"zero":0}""");
    }

    /// <summary>
    /// Número que veio de <c>JsonNode.Parse</c> conserva o <b>texto original</b>, não o valor.
    /// É a diferença entre reserializar o que se leu e reserializar o que se entendeu — e o
    /// gate de forma depende dela: os bytes lidos do banco chegam por este caminho.
    /// </summary>
    [Fact(DisplayName = "Perfil v1 — número parseado conserva o texto original, não o valor")]
    public void Numeros_ParseadosConservamOTexto()
    {
        JsonObject payload = JsonNode.Parse("""{"a":1.0,"b":1e2,"c":-0.0}""")!.AsObject();

        Serializar(payload).Should().Be("""{"a":1.0,"b":1e2,"c":-0.0}""");
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
