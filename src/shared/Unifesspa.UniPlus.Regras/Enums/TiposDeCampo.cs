namespace Unifesspa.UniPlus.Regras.Enums;

/// <summary>O que cada <see cref="TipoDeCampo"/> implica para o campo.</summary>
public static class TiposDeCampo
{
    /// <summary>
    /// Se o candidato escolhe entre valores oferecidos — e o campo, por isso, congela os valores
    /// selecionáveis. O <c>switch</c> nomeia todos os membros: um tipo novo cai no ramo de exceção
    /// até ser classificado aqui.
    /// </summary>
    public static bool EhEscolha(this TipoDeCampo tipo) => tipo switch
    {
        TipoDeCampo.EscolhaUnica or TipoDeCampo.EscolhaMultipla => true,
        TipoDeCampo.Numero or TipoDeCampo.SimOuNao or TipoDeCampo.Texto or TipoDeCampo.Data or TipoDeCampo.Endereco
            or TipoDeCampo.Municipio => false,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoDeCampo desconhecido."),
    };
}
