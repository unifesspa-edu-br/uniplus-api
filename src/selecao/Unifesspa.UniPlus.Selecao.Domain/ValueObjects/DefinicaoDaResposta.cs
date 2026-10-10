namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Globalization;
using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Como o valor de um fato é gravado na inscrição.</summary>
/// <param name="Identificador">Número de documento: gravado cifrado, nunca em claro nem no comprovante.</param>
public sealed record DefinicaoDaResposta(TipoDoValorDeResposta Tipo, bool Multivalorado, bool Identificador)
{
    public const string ClassificacaoIdentificador = "IDENTIFICADOR";

    public static DefinicaoDaResposta De(TipoRenderizacao renderizacao, string classificacaoProtecao)
    {
        bool identificador = string.Equals(classificacaoProtecao, ClassificacaoIdentificador, StringComparison.Ordinal);
        return renderizacao switch
        {
            TipoRenderizacao.Booleano => new(TipoDoValorDeResposta.Booleano, false, identificador),
            TipoRenderizacao.Numero => new(TipoDoValorDeResposta.Numero, false, identificador),
            TipoRenderizacao.SelecaoUnica or TipoRenderizacao.Municipio => new(TipoDoValorDeResposta.Codigo, false, identificador),
            TipoRenderizacao.SelecaoMultipla => new(TipoDoValorDeResposta.Codigo, true, identificador),
            TipoRenderizacao.Texto => new(TipoDoValorDeResposta.Texto, false, identificador),
            TipoRenderizacao.Data => new(TipoDoValorDeResposta.Data, false, identificador),
            TipoRenderizacao.Endereco => new(TipoDoValorDeResposta.Endereco, false, identificador),
            _ => throw new ArgumentOutOfRangeException(nameof(renderizacao), renderizacao, "Tipo de renderização sem tipo de valor."),
        };
    }

    /// <summary>O valor cabe no tipo: um elemento escalar, ou uma lista deles quando multivalorado.</summary>
    public bool Aceita(JsonElement valor) =>
        Multivalorado
            ? valor.ValueKind == JsonValueKind.Array && valor.EnumerateArray().All(AceitaEscalar)
            : AceitaEscalar(valor);

    private bool AceitaEscalar(JsonElement valor) => Tipo switch
    {
        TipoDoValorDeResposta.Booleano => valor.ValueKind is JsonValueKind.True or JsonValueKind.False,
        TipoDoValorDeResposta.Numero => valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out _),
        TipoDoValorDeResposta.Codigo => valor.ValueKind == JsonValueKind.String
            && valor.GetString() is { Length: > 0 and <= OpcaoDeclaradaFato.CodigoMaxLength },
        TipoDoValorDeResposta.Texto => valor.ValueKind == JsonValueKind.String,
        TipoDoValorDeResposta.Data => valor.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(valor.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        TipoDoValorDeResposta.Endereco => EnderecoDeclarado.TentarLer(valor, out _),
        _ => false,
    };
}
