namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Interpreta o valor JSON polimórfico de <c>FormatosPermitidos</c>, resolvido por <see cref="JsonValueKind"/>
/// e não por um DTO discriminado. A ausência estrutural (propriedade ausente ou <c>null</c> explícito) é
/// distinta de "veio, mas em forma inválida": a primeira é <c>FormatosPermitidos.Obrigatorio</c>, a
/// segunda <c>FormatosPermitidos.FormaInvalida</c>. Cada item da lista aceita um texto (<c>"PDF"</c>, sem
/// teto por formato) ou um objeto <c>{formato, tamanhoMaximoBytesMax}</c> (com teto por formato).
/// </summary>
internal static class FormatosPermitidosDoPayload
{
    private const string FormaInvalida = "FormatosPermitidos.FormaInvalida";

    public static Result<FormatosPermitidos> Resolver(JsonElement? valor)
    {
        if (valor is not { } elemento)
        {
            return Result<FormatosPermitidos>.Failure(new DomainError(
                "FormatosPermitidos.Obrigatorio",
                "FormatosPermitidos é obrigatório: declare QUALQUER ou uma lista com ao menos um formato."));
        }

        return elemento.ValueKind switch
        {
            JsonValueKind.String => ResolverTexto(elemento),
            JsonValueKind.Array => ResolverLista(elemento),
            _ => Result<FormatosPermitidos>.Failure(RecusaDaForma()),
        };
    }

    private static Result<FormatosPermitidos> ResolverTexto(JsonElement elemento) =>
        string.Equals(elemento.GetString(), "QUALQUER", StringComparison.Ordinal)
            ? FormatosPermitidos.Criar(qualquer: true, entradas: null)
            : Result<FormatosPermitidos>.Failure(RecusaDaForma());

    private static Result<FormatosPermitidos> ResolverLista(JsonElement lista)
    {
        List<(string Formato, int? TamanhoMaximoBytesMax)> entradas = [];
        foreach (JsonElement item in lista.EnumerateArray())
        {
            Result<(string Formato, int? TamanhoMaximoBytesMax)> entrada = ResolverItem(item);
            if (entrada.IsFailure)
            {
                return Result<FormatosPermitidos>.Failure(entrada.Error!);
            }

            entradas.Add(entrada.Value);
        }

        return FormatosPermitidos.Criar(qualquer: false, entradas);
    }

    private static Result<(string Formato, int? TamanhoMaximoBytesMax)> ResolverItem(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            return Result<(string Formato, int? TamanhoMaximoBytesMax)>.Success((item.GetString() ?? string.Empty, null));
        }

        if (item.ValueKind != JsonValueKind.Object)
        {
            return Falha("Cada item da lista de formatos permitidos deve ser um texto ou um objeto {formato, tamanhoMaximoBytesMax}.");
        }

        string? formato = item.TryGetProperty("formato", out JsonElement formatoElemento)
            && formatoElemento.ValueKind == JsonValueKind.String
                ? formatoElemento.GetString()
                : null;
        if (formato is null)
        {
            return Falha("Cada item da lista de formatos permitidos deve declarar 'formato'.");
        }

        Result<int?> tamanho = ResolverTamanhoMaximoBytesMax(item);
        return tamanho.IsFailure
            ? Result<(string Formato, int? TamanhoMaximoBytesMax)>.Failure(tamanho.Error!)
            : Result<(string Formato, int? TamanhoMaximoBytesMax)>.Success((formato, tamanho.Value));
    }

    // <c>tamanhoMaximoBytesMax</c> ausente ou <c>null</c> é "sem teto por formato". Qualquer outra forma
    // que não seja um inteiro é malformada: falha nomeada, nunca convertida em silêncio nem deixada
    // estourar em GetInt32, que viraria 500 em vez de 422.
    private static Result<int?> ResolverTamanhoMaximoBytesMax(JsonElement item)
    {
        if (!item.TryGetProperty("tamanhoMaximoBytesMax", out JsonElement tamanhoElemento)
            || tamanhoElemento.ValueKind == JsonValueKind.Null)
        {
            return Result<int?>.Success(null);
        }

        return tamanhoElemento.ValueKind == JsonValueKind.Number && tamanhoElemento.TryGetInt32(out int tamanho)
            ? Result<int?>.Success(tamanho)
            : Result<int?>.Failure(new DomainError(FormaInvalida, "'tamanhoMaximoBytesMax', quando presente, deve ser um número inteiro."));
    }

    private static Result<(string Formato, int? TamanhoMaximoBytesMax)> Falha(string mensagem) =>
        Result<(string Formato, int? TamanhoMaximoBytesMax)>.Failure(new DomainError(FormaInvalida, mensagem));

    private static DomainError RecusaDaForma() =>
        new(FormaInvalida, "FormatosPermitidos deve ser o token QUALQUER ou um array de formatos.");
}
