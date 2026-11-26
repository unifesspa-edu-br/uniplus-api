namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Como a tela mostra o valor de um fato do candidato quando o modo de exibição não se
/// deduz do tipo, do formato nem da classificação do fato. Sem declaração, o fato é exibido pelo padrão
/// que a consulta resolve; o CPF e o identificador têm componente próprio, derivado do formato e da
/// classificação, e por isso não se declaram.
/// </summary>
[DescricaoDoContrato("Como a tela mostra o valor de um dado do candidato quando o modo de exibição não se deduz do tipo, do formato nem da proteção.")]
public enum ComponenteDeExibicao
{
    /// <summary>Valor em reais, como <c>R$ 1.234,56</c>.</summary>
    [CodigoDeContrato("MOEDA", "Valor em reais", Descricao = "O valor aparece em reais, como R$ 1.234,56.")]
    Moeda = 1,

    /// <summary>A tela mostra o rótulo numa etiqueta, sem mudar a forma do valor.</summary>
    [CodigoDeContrato("SITUACAO", "Situação", Descricao = "A tela mostra o rótulo numa etiqueta, sem mudar a forma do valor.")]
    Situacao = 2,
}
