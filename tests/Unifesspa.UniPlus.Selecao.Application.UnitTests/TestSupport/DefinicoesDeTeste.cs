namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>DefinicoesDasRespostas para o teste que mexe no agregado sem montar a versão do formulário.</summary>
internal static class DefinicoesDeTeste
{
    public static Dictionary<string, DefinicaoDaResposta> Das(IReadOnlyDictionary<string, JsonElement> respostas) =>
        respostas.ToDictionary(
            static r => r.Key,
            static r => new DefinicaoDaResposta(
                r.Value.ValueKind is JsonValueKind.True or JsonValueKind.False ? TipoDoValorDeResposta.Booleano : TipoDoValorDeResposta.Texto,
                Multivalorado: false,
                Identificador: false),
            StringComparer.Ordinal);

    public static Dictionary<string, DefinicaoDaResposta> Nenhuma() => new(StringComparer.Ordinal);
}
