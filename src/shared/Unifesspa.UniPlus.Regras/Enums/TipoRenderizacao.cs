namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Como o campo que produz um fato é apresentado no formulário. Conjunto fechado por design, coerente com o domínio do fato no catálogo do
/// candidato: <see cref="Booleano"/> só para domínio <c>BOOLEANO</c>, <see cref="Numero"/> só
/// para <c>NUMERICO</c>, <see cref="SelecaoUnica"/>/<see cref="SelecaoMultipla"/> para
/// <c>CATEGORICO</c> conforme a cardinalidade do fato no catálogo, <see cref="Texto"/> para
/// <c>TEXTO</c>, <see cref="Data"/> para <c>DATA</c> e <see cref="Endereco"/> para <c>ENDERECO</c>.
/// </summary>
/// <remarks>
/// A identidade persistida é o código declarado (ADR-0176); a numeração dos membros não é gravada
/// nem é peso de ordenação — mesmo raciocínio de <see cref="Operador"/>.
/// </remarks>
[DescricaoDoContrato("Como o campo do formulário é apresentado e respondido pelo candidato.")]
public enum TipoRenderizacao
{
    /// <summary>Campo numérico — só aceito quando o fato tem domínio <c>NUMERICO</c>.</summary>
    [CodigoDeContrato("NUMERO", "Número")]
    Numero = 1,

    /// <summary>Campo booleano (sim/não) — só aceito quando o fato tem domínio <c>BOOLEANO</c>.</summary>
    [CodigoDeContrato("BOOLEANO", "Sim ou não")]
    Booleano = 2,

    /// <summary>
    /// Seleção de um único valor — aceito para fato <c>CATEGORICO</c> de cardinalidade escalar.
    /// </summary>
    [CodigoDeContrato("SELECAO_UNICA", "Escolha de um valor")]
    SelecaoUnica = 3,

    /// <summary>
    /// Seleção de múltiplos valores — aceito para fato <c>CATEGORICO</c> de cardinalidade
    /// multivalorada.
    /// </summary>
    [CodigoDeContrato("SELECAO_MULTIPLA", "Escolha de vários valores")]
    SelecaoMultipla = 4,

    /// <summary>
    /// Campo de texto — só aceito quando o fato tem domínio <c>TEXTO</c>; o formato do fato no
    /// catálogo (livre, CPF, e-mail…) diz como a resposta é conferida.
    /// </summary>
    [CodigoDeContrato("TEXTO", "Texto")]
    Texto = 5,

    /// <summary>Campo de data — só aceito quando o fato tem domínio <c>DATA</c>.</summary>
    [CodigoDeContrato("DATA", "Data")]
    Data = 6,

    /// <summary>
    /// Endereço estruturado, composto a partir da busca do CEP no Geo (ADR-0096) — só aceito quando o
    /// fato tem domínio <c>ENDERECO</c>.
    /// </summary>
    [CodigoDeContrato("ENDERECO", "Endereço")]
    Endereco = 7,

    /// <summary>
    /// Município escolhido entre os da UF respondida antes, listados pelo Geo no cliente (ADR-0096) —
    /// só aceito quando o fato é categórico escalar de fonte <c>GEO_MUNICIPIO</c>. Não congela lista:
    /// a resposta é o código IBGE, conferido pela UF.
    /// </summary>
    [CodigoDeContrato("MUNICIPIO", "Município")]
    Municipio = 8,
}
