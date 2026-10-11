namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Como o valor de um fato é gravado na inscrição.</summary>
/// <param name="Identificador">Número de documento: gravado cifrado, nunca em claro nem no comprovante.</param>
/// <param name="Formato">O formato da resposta de texto no catálogo, como <c>TELEFONE</c>; nulo nos demais tipos.</param>
public sealed record DefinicaoDaResposta(TipoDoValorDeResposta Tipo, bool Multivalorado, bool Identificador, string? Formato = null)
{
    public const string ClassificacaoIdentificador = "IDENTIFICADOR";

    public static DefinicaoDaResposta De(TipoRenderizacao renderizacao, string classificacaoProtecao, string? formato = null)
    {
        bool identificador = string.Equals(classificacaoProtecao, ClassificacaoIdentificador, StringComparison.Ordinal);
        return renderizacao switch
        {
            TipoRenderizacao.Booleano => new(TipoDoValorDeResposta.Booleano, false, identificador),
            TipoRenderizacao.Numero => new(TipoDoValorDeResposta.Numero, false, identificador),
            TipoRenderizacao.SelecaoUnica or TipoRenderizacao.Municipio => new(TipoDoValorDeResposta.Codigo, false, identificador),
            TipoRenderizacao.SelecaoMultipla => new(TipoDoValorDeResposta.Codigo, true, identificador),
            TipoRenderizacao.Texto => new(TipoDoValorDeResposta.Texto, false, identificador, formato),
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

    /// <summary>
    /// O valor como é gravado: o texto na forma canônica do formato, como o telefone só com os dígitos.
    /// O texto fora do formato fica como veio, para o rascunho guardá-lo e a submissão recusá-lo.
    /// </summary>
    public JsonElement NaFormaCanonica(JsonElement valor)
    {
        if (Formato is null || valor.ValueKind != JsonValueKind.String)
        {
            return valor;
        }

        string texto = valor.GetString()!;
        return new FormatoDeTexto(Formato).FormaCanonica(texto) is { } canonica && !string.Equals(canonica, texto, StringComparison.Ordinal)
            ? JsonSerializer.SerializeToElement(canonica)
            : valor;
    }

    private bool AceitaEscalar(JsonElement valor) => Tipo switch
    {
        TipoDoValorDeResposta.Booleano => valor.ValueKind is JsonValueKind.True or JsonValueKind.False,
        TipoDoValorDeResposta.Numero => valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out _),
        TipoDoValorDeResposta.Codigo => valor.ValueKind == JsonValueKind.String
            && valor.GetString() is { Length: > 0 and <= OpcaoDeclaradaFato.CodigoMaxLength },
        TipoDoValorDeResposta.Texto => valor.ValueKind == JsonValueKind.String,
        TipoDoValorDeResposta.Data => DiaDoCalendario.Ler(valor) is not null,
        TipoDoValorDeResposta.Endereco => EnderecoDeclarado.TentarLer(valor, out _),
        _ => false,
    };
}
