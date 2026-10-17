namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Como o valor de um fato é gravado na inscrição.</summary>
/// <param name="Identificador">Número de documento: gravado cifrado, nunca em claro nem no comprovante.</param>
/// <param name="Formato">O formato da resposta de texto no catálogo, como <c>TELEFONE</c>; nulo nos demais tipos.</param>
/// <param name="CifraIndividual">
/// Dado pessoal sensível gravado cifrado, mas que o titular lê de volta, inclusive no comprovante: ao
/// contrário do número de documento, que nunca sai da gravação.
/// </param>
public sealed record DefinicaoDaResposta(
    TipoDoValorDeResposta Tipo, bool Multivalorado, bool Identificador, string? Formato = null, bool CifraIndividual = false)
{
    public const string ClassificacaoIdentificador = "IDENTIFICADOR";

    public const string ClassificacaoSensivel = "SENSIVEL";

    /// <summary>
    /// Os fatos sensíveis gravados com cifra individual (parecer da DPO de 13/10/2026): a identidade de
    /// gênero e a orientação sexual. É uma lista de fatos, e não toda a classificação sensível, porque a
    /// cifra dos demais fatos sensíveis — deficiência, saúde, atendimento — é decisão à parte.
    /// </summary>
    private static readonly HashSet<string> FatosComCifraIndividual = new(["IDENTIDADE_GENERO", "ORIENTACAO_SEXUAL"], StringComparer.Ordinal);

    /// <summary>A resposta é gravada cifrada: número de documento ou dado com cifra individual.</summary>
    public bool Cifrada => Identificador || CifraIndividual;

    /// <summary>O fato tem cifra individual: está na lista e é classificado como sensível.</summary>
    public static bool TemCifraIndividual(string? fatoCodigo, string classificacaoProtecao) =>
        fatoCodigo is not null
        && string.Equals(classificacaoProtecao, ClassificacaoSensivel, StringComparison.Ordinal)
        && FatosComCifraIndividual.Contains(fatoCodigo);

    /// <summary>O fato é um dos que têm cifra individual, qualquer que seja a classificação gravada.</summary>
    public static bool EhFatoComCifraIndividual(string fatoCodigo) => FatosComCifraIndividual.Contains(fatoCodigo);

    public static DefinicaoDaResposta De(TipoRenderizacao renderizacao, string classificacaoProtecao, string? formato = null, string? fatoCodigo = null)
    {
        formato = fatoCodigo is null ? formato : ConjuntoBasicoDaInscricao.FormatoDaResposta(fatoCodigo, formato);
        bool identificador = string.Equals(classificacaoProtecao, ClassificacaoIdentificador, StringComparison.Ordinal);
        bool cifra = TemCifraIndividual(fatoCodigo, classificacaoProtecao);
        return renderizacao switch
        {
            TipoRenderizacao.Booleano => new(TipoDoValorDeResposta.Booleano, false, identificador, null, cifra),
            TipoRenderizacao.Numero => new(TipoDoValorDeResposta.Numero, false, identificador, null, cifra),
            TipoRenderizacao.SelecaoUnica or TipoRenderizacao.Municipio => new(TipoDoValorDeResposta.Codigo, false, identificador, null, cifra),
            TipoRenderizacao.SelecaoMultipla => new(TipoDoValorDeResposta.Codigo, true, identificador, null, cifra),
            TipoRenderizacao.Texto => new(TipoDoValorDeResposta.Texto, false, identificador, formato, cifra),
            TipoRenderizacao.Data => new(TipoDoValorDeResposta.Data, false, identificador, null, cifra),
            TipoRenderizacao.Endereco => new(TipoDoValorDeResposta.Endereco, false, identificador, null, cifra),
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
