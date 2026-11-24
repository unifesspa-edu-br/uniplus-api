namespace Unifesspa.UniPlus.Regras.Enums;

/// <summary>O que cada <see cref="TipoRenderizacao"/> implica para o campo.</summary>
public static class TiposRenderizacao
{
    /// <summary>
    /// Se o candidato escolhe entre valores oferecidos — e o campo, por isso, congela os valores
    /// selecionáveis. O <c>switch</c> nomeia todos os membros: um tipo novo cai no ramo de exceção
    /// até ser classificado aqui.
    /// </summary>
    public static bool EhEscolha(this TipoRenderizacao tipo) => tipo switch
    {
        TipoRenderizacao.EscolhaUnica or TipoRenderizacao.EscolhaMultipla => true,
        TipoRenderizacao.Numero or TipoRenderizacao.SimOuNao or TipoRenderizacao.Texto or TipoRenderizacao.Data or TipoRenderizacao.Endereco
            or TipoRenderizacao.Municipio => false,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoRenderizacao desconhecido."),
    };
}
