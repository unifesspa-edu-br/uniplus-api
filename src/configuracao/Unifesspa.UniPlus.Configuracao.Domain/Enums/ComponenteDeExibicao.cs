namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Como a tela mostra o valor de um <see cref="Entities.FatoCandidato"/> quando o modo de exibição não se
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

    /// <summary>Situação de um processo ou pedido, como uma etiqueta.</summary>
    [CodigoDeContrato("SITUACAO", "Situação", Descricao = "O valor aparece como a situação de um processo ou de um pedido, numa etiqueta.")]
    Situacao = 2,
}
