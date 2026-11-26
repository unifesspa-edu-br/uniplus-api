namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura de datas, instantes, booleanos, inteiros e decimais do envelope, na forma canônica exata.
/// </summary>
internal static class LeituraDeDatasENumeros
{
    /// <summary>
    /// Data no formato canônico — e <b>nunca</b> o <c>default(DateOnly)</c>.
    /// </summary>
    /// <remarks>
    /// <c>0001-01-01</c> é como um campo de data <b>omitido</b> se materializa, e é por isso
    /// que os validators de publicar e de retificar o recusam explicitamente
    /// (<c>NotEqual(default(DateOnly))</c>). <c>DadosEdital.Criar</c>, porém, só checa
    /// <c>fim &gt;= inicio</c> — e <c>0001-01-01</c> satisfaz isso. Como o encoder reemite a
    /// data tal qual, um envelope com o período zerado faria round-trip <b>perfeito</b> e
    /// reidrataria um certame cujo período de inscrição é impossível.
    /// </remarks>
    public static DateOnly Data(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou)
        {
            return default;
        }

        _ = leitor.Texto(pai, chave, path);
        return leitor.Falhou ? default : leitor.DataOpcional(pai, chave, path) ?? default;
    }

    /// <summary>
    /// Instante no formato canônico e <b>obrigatório</b> — o período de inscrição do Edital,
    /// que existe em toda versão publicada.
    /// </summary>
    public static DateTimeOffset Instante(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou)
        {
            return default;
        }

        // Texto exige a chave presente e não-nula; o formato canônico é conferido uma vez só,
        // em InstanteOpcional — duas cópias divergiriam na próxima mudança de formato.
        _ = leitor.Texto(pai, chave, path);

        return leitor.Falhou ? default : leitor.InstanteOpcional(pai, chave, path) ?? default;
    }

    /// <summary>
    /// Data opcional no formato canônico (<c>ObrigatoriedadeLegal.VigenciaFim</c>
    /// — vigência aberta é estado válido). <see langword="null"/> quando a chave é
    /// <c>null</c>; do contrário, mesma validação estrita de <see cref="Data"/> (nunca o
    /// valor default).
    /// </summary>
    public static DateOnly? DataOpcional(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        string? texto = leitor.TextoOpcional(pai, chave, path);
        if (leitor.Falhou || texto is null)
        {
            return null;
        }

        string? erro = ConferirData(texto, out DateOnly valor);
        return erro is null ? valor : leitor.Malformado<DateOnly?>($"{path}.{chave}", erro);
    }

    private static string? ConferirData(string texto, out DateOnly valor)
    {
        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out valor))
        {
            return "esperada uma data 'yyyy-MM-dd'.";
        }

        return valor == default
            ? "a data não pode ser o valor padrão — é assim que uma data omitida se materializa."
            : null;
    }

    /// <summary>
    /// Instante opcional na forma canônica do envelope (<see cref="HashCanonicalComputer.CanonicalInstantFormat"/>),
    /// usado pela janela da fase e da etapa. <see langword="null"/> quando a chave é <c>null</c> — "sem data" é
    /// estado válido para fase cuja data é definida na execução.
    /// </summary>
    public static DateTimeOffset? InstanteOpcional(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        string? texto = leitor.TextoOpcional(pai, chave, path);
        if (leitor.Falhou || texto is null)
        {
            return null;
        }

        return HashCanonicalComputer.TryParseInstantCanonical(texto, out DateTimeOffset valor)
            ? valor
            : leitor.Malformado<DateTimeOffset?>(
                $"{path}.{chave}",
                $"esperado um instante canônico ('{HashCanonicalComputer.CanonicalInstantFormat}').");
    }

    public static bool Booleano(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou)
        {
            return default;
        }

        JsonNode? node = pai[chave];
        System.Text.Json.JsonValueKind? kind = node?.GetValueKind();

        return kind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
            ? node!.GetValue<bool>()
            : leitor.Malformado<bool>($"{path}.{chave}", "esperado um booleano.");
    }

    public static int Inteiro(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou)
        {
            return default;
        }

        int? valor = leitor.InteiroOpcional(pai, chave, path);
        if (leitor.Falhou)
        {
            return default;
        }

        return valor ?? leitor.Malformado<int>($"{path}.{chave}", "esperado um inteiro, encontrado null.");
    }

    public static int? InteiroOpcional(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou || pai[chave] is not JsonNode node)
        {
            return null;
        }

        if (node.GetValueKind() != System.Text.Json.JsonValueKind.Number || !node.AsValue().TryGetValue(out int valor))
        {
            return leitor.Malformado<int?>($"{path}.{chave}", "esperado um inteiro.");
        }

        return valor;
    }

    /// <summary>
    /// Decimal de negócio — escrito pelo encoder como <b>string</b> com escala declarada
    /// (ADR-0100 item 2), nunca como número JSON. A leitura exige que a reserialização
    /// com a <b>mesma escala</b> reproduza a string lida: <c>"1.00000"</c> ou
    /// <c>"1.0"</c> não são a mesma coisa que <c>"1.0000"</c>, e aceitá-los deixaria a
    /// escala do envelope à mercê de quem o escreveu por fora.
    /// </summary>
    public static decimal Decimal(this LeitorEnvelope leitor, JsonObject pai, string chave, int escala, string path, int precisao = 0)
    {
        if (leitor.Falhou)
        {
            return default;
        }

        decimal? valor = leitor.DecimalOpcional(pai, chave, escala, path, precisao);
        if (leitor.Falhou)
        {
            return default;
        }

        return valor ?? leitor.Malformado<decimal>($"{path}.{chave}", "esperado um decimal, encontrado null.");
    }

    /// <param name="precisao">
    /// Total de dígitos que a <b>coluna</b> comporta (o <c>p</c> de <c>numeric(p,s)</c> —
    /// ver <see cref="LimitesDoEnvelope"/>). Passe <c>0</c> quando o destino não for uma
    /// coluna com precisão declarada (os decimais que vivem dentro de um <c>json</c>).
    /// </param>
    public static decimal? DecimalOpcional(this LeitorEnvelope leitor, JsonObject pai, string chave, int escala, string path, int precisao = 0)
    {
        if (leitor.Falhou || pai[chave] is null)
        {
            return null;
        }

        string? texto = leitor.TextoOpcional(pai, chave, path);
        if (leitor.Falhou)
        {
            return null;
        }

        string? erro = ConferirDecimal(texto, escala, precisao, out decimal valor);
        return erro is null ? valor : leitor.Malformado<decimal?>($"{path}.{chave}", erro);
    }

    private static string? ConferirDecimal(string? texto, int escala, int precisao, out decimal valor)
    {
        if (!decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor)
            || !string.Equals(HashCanonicalComputer.SerializeDecimalCanonical(valor, escala), texto, StringComparison.Ordinal))
        {
            return $"esperado um decimal canônico com {escala} casas.";
        }

        // A precisão limita o total de dígitos que cabe em numeric(p,s), além da escala canônica.
        return precisao > 0 && DigitosSignificativos(valor) > precisao
            ? $"o decimal tem mais dígitos do que a coluna comporta (numeric({precisao},{escala}))."
            : null;
    }

    /// <summary>Dígitos totais de um decimal já arredondado à escala do campo — o <c>p</c> de <c>numeric(p,s)</c>.</summary>
    private static int DigitosSignificativos(decimal valor)
    {
        string digitos = Math.Abs(valor).ToString(CultureInfo.InvariantCulture).Replace(".", string.Empty, StringComparison.Ordinal);
        string semZerosAEsquerda = digitos.TrimStart('0');

        return semZerosAEsquerda.Length == 0 ? 1 : semZerosAEsquerda.Length;
    }
}
