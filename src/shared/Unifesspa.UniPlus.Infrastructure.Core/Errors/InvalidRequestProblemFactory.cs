namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Globalization;
using System.Text.RegularExpressions;

using Kernel.Results;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resposta canônica para a carga que não chega a ser lida — o corpo que o desserializador
/// recusa, e o campo obrigatório que ele não encontra.
/// </summary>
/// <remarks>
/// <para>
/// Sem isto, essas recusas saem no envelope padrão do framework, que difere do nosso em tudo
/// que a ADR-0023 fixa: sem <c>code</c>, sem <c>traceId</c>, <c>type</c> apontando para a RFC
/// do status em vez do catálogo, título em inglês. E, o mais grave, o corpo carrega o <b>nome
/// completo do tipo CLR</b> que falhou a desserialização — namespace, camada e nome do record
/// da Application entregues a qualquer cliente que mande uma carga incompleta.
/// </para>
/// <para>
/// <strong>A mensagem do framework nunca é repassada.</strong> É dela que o nome do tipo vem, e
/// repassá-la filtrando o que parece nome de tipo seria apostar no formato de um texto que não
/// controlamos. O que se aproveita são os <b>nomes dos campos</b> — a única informação ali que
/// serve ao cliente — extraídos da lista que a mensagem de propriedade obrigatória enumera.
/// </para>
/// <para>
/// <strong>O que este tipo NÃO responde, e por quê.</strong> Ele devolve <see langword="null"/>
/// para tudo que não seja leitura da requisição, deixando o factory anterior da cadeia
/// responder. É deliberado: uma falha de <c>[RegularExpression]</c> num parâmetro de rota
/// acontece DEPOIS de o binding ter dado certo, e a mensagem dela — escrita por nós, em pt-BR,
/// dizendo ao cliente qual é o formato esperado — é muito melhor do que qualquer coisa que se
/// possa dizer genericamente. Capturá-la aqui trocaria orientação útil por "o corpo não pôde
/// ser lido", que além de inútil seria falso.
/// </para>
/// </remarks>
public static class InvalidRequestProblemFactory
{
    /// <summary>
    /// Nomes entre aspas simples que a mensagem de propriedade obrigatória enumera depois de
    /// <c>including:</c>. O recorte começa DEPOIS dessa palavra de propósito: antes dela vem o
    /// nome do tipo, também entre aspas simples, que é justamente o que não pode sair daqui.
    /// </summary>
    private static readonly Regex NamesAfterIncluding = new(
        @"including:(?<list>.*)$",
        RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

    private static readonly Regex SingleQuoted = new(
        @"'(?<name>[^']{1,128})'",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Chave sintética que o desserializador usa para apontar posição no documento
    /// (<c>$</c>, <c>$[0]</c>, <c>$.campo</c>). Não é nome de campo do contrato, e a presença
    /// dela é o que identifica a falha como de LEITURA do corpo.
    /// </summary>
    private static readonly Regex SyntheticKey = new(
        @"^\$",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// A resposta canônica, ou <see langword="null"/> quando a falha não é de leitura — caso em
    /// que o caller delega ao factory anterior da cadeia.
    /// </summary>
    public static IActionResult? TryBuild(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<string> missing = MissingFields(context.ModelState);
        bool bodyUnreadable = HasBodyReadFailure(context.ModelState);

        if (missing.Count == 0 && !bodyUnreadable)
        {
            return null;
        }

        IDomainErrorMapper mapper = context.HttpContext.RequestServices.GetRequiredService<IDomainErrorMapper>();

        (string code, string detail) = missing.Count > 0
            ? (InvalidRequestErrorCodes.MissingRequiredField, $"A requisição não declara {Enumerate(missing)}.")
            : (InvalidRequestErrorCodes.Malformed, "A requisição não pôde ser lida: o corpo não está no formato que o contrato declara.");

        return Result.Failure(new DomainError(code, detail)).ToActionResult(mapper);
    }

    /// <summary>
    /// Os nomes de campo que a recusa consegue afirmar, sem nenhum texto do framework junto.
    /// </summary>
    private static IReadOnlyList<string> MissingFields(ModelStateDictionary modelState)
    {
        SortedSet<string> names = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, ModelStateEntry> entry in modelState)
        {
            // Entrada que casou não descreve defeito nenhum: o ModelState traz TODAS as
            // propriedades da requisição, não só as reprovadas.
            if (entry.Value.ValidationState != ModelValidationState.Invalid)
            {
                continue;
            }

            if (SyntheticKey.IsMatch(entry.Key))
            {
                foreach (ModelError error in entry.Value.Errors)
                {
                    Match list = NamesAfterIncluding.Match(error.ErrorMessage ?? string.Empty);
                    if (!list.Success)
                    {
                        continue;
                    }

                    foreach (Match name in SingleQuoted.Matches(list.Groups["list"].Value))
                    {
                        names.Add(name.Groups["name"].Value);
                    }
                }

                continue;
            }

            // Reprovado não quer dizer ausente: `?vigentes=abc` também reprova, e ali o campo
            // FOI declarado — só não converte, e quem responde por ele é o factory anterior.
            // O que caracteriza ausência é não haver valor tentado nem valor cru: não houve o
            // que converter, porque nada veio.
            if (!string.IsNullOrEmpty(entry.Key)
                && entry.Value.AttemptedValue is null
                && entry.Value.RawValue is null)
            {
                names.Add(entry.Key);
            }
        }

        return [.. names];
    }

    /// <summary>
    /// Se alguma entrada aponta posição no documento — a marca de que quem reprovou foi o
    /// desserializador, e não um validador que rodou depois do binding.
    /// </summary>
    private static bool HasBodyReadFailure(ModelStateDictionary modelState) =>
        modelState.Any(entry =>
            entry.Value is { ValidationState: ModelValidationState.Invalid }
            && SyntheticKey.IsMatch(entry.Key));

    private static string Enumerate(IReadOnlyList<string> fields) =>
        fields.Count == 1
            ? $"o campo obrigatório '{fields[0]}'"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"os campos obrigatórios {string.Join(", ", fields.Take(fields.Count - 1).Select(static f => $"'{f}'"))} e '{fields[^1]}'");
}
