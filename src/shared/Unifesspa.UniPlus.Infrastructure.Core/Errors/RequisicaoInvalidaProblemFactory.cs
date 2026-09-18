namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

using Kernel.Results;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resposta canônica para a carga que não chega a virar comando — a que o MVC recusa no
/// binding, antes de qualquer código nosso rodar.
/// </summary>
/// <remarks>
/// <para>
/// Sem isto, essas recusas saem no envelope padrão do framework, que difere do nosso em tudo
/// que a ADR-0023 fixa: sem <c>code</c>, sem <c>traceId</c>, <c>type</c> apontando para a RFC
/// em vez do catálogo, título em inglês. E, o mais grave, o corpo carrega o <b>nome completo
/// do tipo CLR</b> que falhou a desserialização — namespace, camada e nome do record da
/// Application entregues a qualquer cliente que mande uma carga incompleta.
/// </para>
/// <para>
/// A recusa se divide em duas, e a separação é estrutural. <b>Ausência</b> é o campo que o
/// contrato exige e a carga não traz — não há valor tentado nem valor cru, porque não houve o
/// que converter. <b>Malformada</b> é todo o resto: JSON que não parseia, valor presente que
/// não converte para o tipo declarado, parâmetro de rota fora de forma. Confundir as duas manda
/// o cliente acrescentar um campo que ele já mandou.
/// </para>
/// <para>
/// <strong>A mensagem do framework nunca é repassada.</strong> É dela que o nome do tipo vem, e
/// repassá-la filtrando o que parece nome de tipo seria apostar no formato de um texto que não
/// controlamos. O que se aproveita são os <b>nomes dos campos</b> — a única informação ali que
/// serve ao cliente — extraídos das chaves do <c>ModelState</c> e da lista que a mensagem de
/// propriedade obrigatória enumera. Quando nada se extrai, a recusa sai sem nomear campo algum,
/// que é pior para quem depura e correto para quem não deve ver nossa estrutura interna.
/// </para>
/// </remarks>
public static class RequisicaoInvalidaProblemFactory
{
    /// <summary>
    /// Nomes entre aspas simples que a mensagem de propriedade obrigatória enumera depois de
    /// <c>including:</c>. O recorte começa DEPOIS dessa palavra de propósito: antes dela vem o
    /// nome do tipo, também entre aspas simples, que é justamente o que não pode sair daqui.
    /// </summary>
    private static readonly Regex NomesDepoisDeIncluding = new(
        @"including:(?<lista>.*)$",
        RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

    private static readonly Regex EntreAspasSimples = new(
        @"'(?<nome>[^']{1,128})'",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Chave sintética que o desserializador usa para apontar posição no documento
    /// (<c>$</c>, <c>$[0]</c>, <c>$.campo</c>). Não é nome de campo do contrato.
    /// </summary>
    private static readonly Regex ChaveSintetica = new(
        @"^\$",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    public static IActionResult Build(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IDomainErrorMapper mapper = context.HttpContext.RequestServices.GetRequiredService<IDomainErrorMapper>();

        IReadOnlyList<string> campos = CamposAusentes(context.ModelState);
        bool ausencia = campos.Count > 0;

        string code = ausencia
            ? RequisicaoInvalidaErrorCodes.CampoObrigatorioAusente
            : RequisicaoInvalidaErrorCodes.Malformada;

        string detail = ausencia
            ? $"A requisição não declara {Enumerar(campos)}."
            : "A requisição não pôde ser lida: o corpo não está no formato que o contrato declara.";

        return Result.Failure(new DomainError(code, detail)).ToActionResult(mapper);
    }

    /// <summary>
    /// Os nomes de campo que a recusa consegue afirmar, sem nenhum texto do framework junto.
    /// </summary>
    private static IReadOnlyList<string> CamposAusentes(ModelStateDictionary modelState)
    {
        SortedSet<string> nomes = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, ModelStateEntry> entrada in modelState)
        {
            // Entrada que casou não descreve defeito nenhum: o ModelState traz TODAS as
            // propriedades da requisição, não só as reprovadas.
            if (entrada.Value.ValidationState != ModelValidationState.Invalid)
            {
                continue;
            }

            // A chave é o nome do campo quando o binder reprova o parâmetro ou a propriedade;
            // é posição no documento quando quem reprovou foi o desserializador.
            //
            // Reprovado não quer dizer ausente: `?vigentes=abc` também reprova, e ali o campo
            // FOI declarado — só não converte. Chamar isso de "campo obrigatório não declarado"
            // manda o cliente acrescentar o que ele já mandou. O que separa os dois casos é
            // estrutural, e não o texto da mensagem: quando nada foi fornecido, não há valor
            // tentado nem valor cru para o binder ter tentado converter.
            if (!string.IsNullOrEmpty(entrada.Key) && !ChaveSintetica.IsMatch(entrada.Key))
            {
                if (entrada.Value.AttemptedValue is null && entrada.Value.RawValue is null)
                {
                    nomes.Add(entrada.Key);
                }

                continue;
            }

            foreach (ModelError erro in entrada.Value.Errors)
            {
                Match lista = NomesDepoisDeIncluding.Match(erro.ErrorMessage ?? string.Empty);
                if (!lista.Success)
                {
                    continue;
                }

                foreach (Match nome in EntreAspasSimples.Matches(lista.Groups["lista"].Value))
                {
                    nomes.Add(nome.Groups["nome"].Value);
                }
            }
        }

        return [.. nomes];
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
        Justification = "Nome de campo do contrato JSON é minúsculo por convenção; a comparação não é de segurança.")]
    private static string Enumerar(IReadOnlyList<string> campos) =>
        campos.Count == 1
            ? $"o campo obrigatório '{campos[0]}'"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"os campos obrigatórios {string.Join(", ", campos.Take(campos.Count - 1).Select(static c => $"'{c}'"))} e '{campos[^1]}'");
}
