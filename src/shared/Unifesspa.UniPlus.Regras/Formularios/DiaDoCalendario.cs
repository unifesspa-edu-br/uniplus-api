namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Globalization;
using System.Text.Json;

/// <summary>
/// O dia do calendário na forma que atravessa o fio e fica gravado: ISO 8601, <c>aaaa-mm-dd</c>, sem
/// hora nem fuso. É a única leitura e escrita de dia das regras do formulário, para que a conferência do
/// tipo da resposta, as regras que a leem e a gravação aceitem exatamente os mesmos dias.
/// </summary>
public static class DiaDoCalendario
{
    public const string Formato = "yyyy-MM-dd";

    /// <summary>O dia do texto; <see langword="null"/> quando o texto não está no <see cref="Formato"/>.</summary>
    public static DateOnly? Ler(string? texto) =>
        DateOnly.TryParseExact(texto, Formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly dia) ? dia : null;

    /// <summary>O dia do elemento; <see langword="null"/> quando ele não é um texto no <see cref="Formato"/>.</summary>
    public static DateOnly? Ler(JsonElement elemento) => elemento.ValueKind == JsonValueKind.String ? Ler(elemento.GetString()) : null;

    public static string Escrever(DateOnly dia) => dia.ToString(Formato, CultureInfo.InvariantCulture);
}
