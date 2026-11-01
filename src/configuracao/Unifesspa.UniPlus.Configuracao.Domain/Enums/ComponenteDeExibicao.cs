namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Como a tela mostra o valor de um <see cref="Entities.FatoCandidato"/> quando o modo de exibição não se
/// deduz do tipo, do formato nem da classificação do fato. Sem declaração, o fato é exibido pelo padrão
/// que a consulta resolve; o CPF e o identificador têm componente próprio, derivado do formato e da
/// classificação, e por isso não se declaram.
/// </summary>
public enum ComponenteDeExibicao
{
    /// <summary>Sentinela — componente não informado.</summary>
    Nenhum = 0,

    /// <summary>Valor em reais, como <c>R$ 1.234,56</c>.</summary>
    Moeda,

    /// <summary>Situação de um processo ou pedido, como uma etiqueta.</summary>
    Situacao,
}
