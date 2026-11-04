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
    /// restauraria um certame cujo período de inscrição é impossível.
    /// </remarks>
    public static DateOnly Data(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou)
        {
            return default;
        }

        string texto = leitor.Texto(pai, chave, path);
        if (leitor.Falhou)
        {
            return default;
        }

        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly valor))
        {
            return leitor.Malformado<DateOnly>($"{path}.{chave}", $"esperada uma data 'yyyy-MM-dd', encontrado '{texto}'.");
        }

        return valor == default
            ? leitor.Malformado<DateOnly>($"{path}.{chave}", "a data não pode ser o valor default (0001-01-01) — é assim que uma data omitida se materializa.")
            : valor;
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

        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly valor))
        {
            return leitor.Malformado<DateOnly?>($"{path}.{chave}", $"esperada uma data 'yyyy-MM-dd', encontrado '{texto}'.");
        }

        return valor == default
            ? leitor.Malformado<DateOnly?>($"{path}.{chave}", "a data não pode ser o valor default (0001-01-01) — é assim que uma data omitida se materializa.")
            : valor;
    }

    /// <summary>
    /// Instante opcional na forma canônica (RFC 3339, UTC, sem fração —
    /// <see cref="HashCanonicalComputer.SerializeInstantCanonical"/>), usado pela janela da
    /// <c>FaseCronograma</c>. <see langword="null"/> quando a chave é
    /// <c>null</c> — "sem data" é estado válido para fase cuja data é definida na execução.
    /// </summary>
    public static DateTimeOffset? InstanteOpcional(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
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

        if (!DateTimeOffset.TryParseExact(
                texto, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset valor))
        {
            return leitor.Malformado<DateTimeOffset?>(
                $"{path}.{chave}", $"esperado um instante canônico ('yyyy-MM-ddTHH:mm:ssZ'), encontrado '{texto}'.");
        }

        if (!string.Equals(HashCanonicalComputer.SerializeInstantCanonical(valor), texto, StringComparison.Ordinal))
        {
            return leitor.Malformado<DateTimeOffset?>($"{path}.{chave}", "o instante não está na forma canônica.");
        }

        return valor;
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
        if (leitor.Falhou)
        {
            return null;
        }

        JsonNode? node = pai[chave];
        if (node is null)
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
        if (leitor.Falhou)
        {
            return null;
        }

        JsonNode? node = pai[chave];
        if (node is null)
        {
            return null;
        }

        string? texto = leitor.TextoOpcional(pai, chave, path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (!decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal valor)
            || !string.Equals(HashCanonicalComputer.SerializeDecimalCanonical(valor, escala), texto, StringComparison.Ordinal))
        {
            return leitor.Malformado<decimal?>($"{path}.{chave}", $"esperado um decimal canônico com {escala} casas, encontrado '{texto}'.");
        }

        // A escala sozinha não basta. `numeric(18,4)` também limita o total de dígitos, e um
        // peso como "99999999999999999999.0000" tem escala 4 impecável — recanonicaliza nos
        // MESMOS bytes, satisfaz o domínio, e a prova de round-trip aprova. O estouro só
        // aflora no SaveChanges, como 22003 (numeric field overflow) no meio do descarte:
        // 500 não tratado em vez de recusa nomeada.
        if (precisao > 0 && DigitosSignificativos(valor) > precisao)
        {
            return leitor.Malformado<decimal?>(
                $"{path}.{chave}",
                $"o decimal '{texto}' tem mais dígitos do que a coluna comporta (numeric({precisao},{escala})).");
        }

        return valor;
    }

    /// <summary>Dígitos totais de um decimal já arredondado à escala do campo — o <c>p</c> de <c>numeric(p,s)</c>.</summary>
    private static int DigitosSignificativos(decimal valor)
    {
        string digitos = Math.Abs(valor).ToString(CultureInfo.InvariantCulture).Replace(".", string.Empty, StringComparison.Ordinal);
        string semZerosAEsquerda = digitos.TrimStart('0');

        return semZerosAEsquerda.Length == 0 ? 1 : semZerosAEsquerda.Length;
    }
}
