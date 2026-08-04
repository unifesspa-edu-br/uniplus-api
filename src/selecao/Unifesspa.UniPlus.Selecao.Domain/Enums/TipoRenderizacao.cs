namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Como o campo produtor de um <see cref="Entities.FatoColetado"/> é apresentado no formulário de
/// inscrição. Conjunto fechado por design, coerente com o domínio do fato no catálogo do
/// candidato: <see cref="Booleano"/> só para domínio <c>BOOLEANO</c>, <see cref="Numero"/> só
/// para <c>NUMERICO</c>, <see cref="SelecaoUnica"/>/<see cref="SelecaoMultipla"/> para
/// <c>CATEGORICO</c> conforme a cardinalidade do fato no catálogo.
/// </summary>
/// <remarks>
/// A numeração dos membros é identidade de persistência, não peso de ordenação — mesmo raciocínio
/// de <see cref="Operador"/>. O código textual de wire/envelope vive em
/// <see cref="TipoRenderizacaoCodigo"/>, nunca em <c>enum.ToString()</c>.
/// </remarks>
public enum TipoRenderizacao
{
    Nenhuma = 0,

    /// <summary>Campo numérico — só aceito quando o fato tem domínio <c>NUMERICO</c>.</summary>
    Numero = 1,

    /// <summary>Campo booleano (sim/não) — só aceito quando o fato tem domínio <c>BOOLEANO</c>.</summary>
    Booleano = 2,

    /// <summary>
    /// Seleção de um único valor — aceito para fato <c>CATEGORICO</c> de cardinalidade escalar.
    /// </summary>
    SelecaoUnica = 3,

    /// <summary>
    /// Seleção de múltiplos valores — aceito para fato <c>CATEGORICO</c> de cardinalidade
    /// multivalorada.
    /// </summary>
    SelecaoMultipla = 4,
}
