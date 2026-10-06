namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A resposta do campo de texto está no formato que o fato declara no catálogo (ADR-0136): o tipo de
/// valor do Kernel confere CPF, e-mail, telefone, CEP e nome de pessoa, e o texto livre aceita
/// qualquer texto. Não é restrição que o administrador declara no item: vem do formato do campo, e a
/// avaliação a confere junto das restrições dele, para o interpretador do front não ser mais rígido
/// nem mais frouxo que o servidor.
/// </summary>
public sealed record FormatoDeTexto : RestricaoValor
{
    private static readonly Dictionary<string, Func<string, bool>> Conferencias = new(StringComparer.Ordinal)
    {
        ["LIVRE"] = static _ => true,
        ["CPF"] = static texto => Cpf.Criar(texto).IsSuccess,
        ["EMAIL"] = static texto => Email.Criar(texto).IsSuccess,
        ["TELEFONE"] = static texto => Telefone.Criar(texto).IsSuccess,
        ["CEP"] = static texto => Cep.Criar(texto).IsSuccess,
        ["NOME_PESSOA"] = static texto => NomePessoa.Criar(texto).IsSuccess,
    };

    public FormatoDeTexto(string formato)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formato);
        if (!Conferencias.ContainsKey(formato))
        {
            throw new ArgumentException($"O formato de texto '{formato}' não é do vocabulário do catálogo.", nameof(formato));
        }

        Formato = formato;
    }

    public override TipoRestricaoValor Tipo => TipoRestricaoValor.FormatoTexto;

    /// <summary>O token do formato no catálogo, como <c>CPF</c> ou <c>EMAIL</c>.</summary>
    public string Formato { get; }

    public override Ternario Avaliar(JsonElement resposta, IReadOnlyDictionary<string, FatoResolvido> fatos) =>
        ComoTernario(resposta.ValueKind == JsonValueKind.String && Conferencias[Formato](resposta.GetString()!));
}
