namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A resposta do campo de texto está no formato que o fato declara no catálogo (ADR-0136): o tipo de
/// valor do Kernel confere CPF, e-mail, telefone, CEP e nome de pessoa, e o texto livre aceita
/// qualquer texto. Não é restrição que o administrador declara no item: vem do formato do campo, e a
/// avaliação a confere junto das restrições dele, para o interpretador do front não ser mais rígido
/// nem mais frouxo que o servidor. O mesmo tipo de valor dá a forma canônica com que a resposta é
/// gravada, como o telefone só com os dígitos.
/// </summary>
public sealed record FormatoDeTexto : RestricaoValor
{
    /// <summary>A forma canônica do texto em cada formato; nula quando o texto não está no formato.</summary>
    private static readonly Dictionary<string, Func<string, string?>> FormasCanonicas = new(StringComparer.Ordinal)
    {
        ["LIVRE"] = static texto => texto,
        ["CPF"] = static texto => Cpf.Criar(texto) is { IsSuccess: true } cpf ? cpf.Value!.Valor : null,
        ["EMAIL"] = static texto => Email.Criar(texto) is { IsSuccess: true } email ? email.Value!.Valor : null,
        ["TELEFONE"] = static texto => Telefone.Criar(texto) is { IsSuccess: true } telefone ? telefone.Value!.Valor : null,
        ["CEP"] = static texto => Cep.Criar(texto) is { IsSuccess: true } cep ? cep.Value!.Valor : null,
        ["NOME_PESSOA"] = static texto => NomePessoa.Criar(texto) is { IsSuccess: true } nome ? nome.Value!.Valor : null,
    };

    public FormatoDeTexto(string formato)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formato);
        if (!FormasCanonicas.ContainsKey(formato))
        {
            throw new ArgumentException($"O formato de texto '{formato}' não é do vocabulário do catálogo.", nameof(formato));
        }

        Formato = formato;
    }

    public override TipoRestricaoValor Tipo => TipoRestricaoValor.FormatoTexto;

    /// <summary>O token do formato no catálogo, como <c>CPF</c> ou <c>EMAIL</c>.</summary>
    public string Formato { get; }

    public override Ternario Avaliar(JsonElement resposta, ContextoDaResposta contexto) =>
        ComoTernario(resposta.ValueKind == JsonValueKind.String && FormaCanonica(resposta.GetString()!) is not null);

    /// <summary>O texto na forma com que é gravado, como o telefone só com os dígitos; nulo fora do formato.</summary>
    public string? FormaCanonica(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return FormasCanonicas[Formato](texto);
    }
}
