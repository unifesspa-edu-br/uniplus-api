namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

internal static class FormasDoValorDoItem
{
    public static FormaDoValorDoItem De(TipoDeCampo tipo) => tipo switch
    {
        TipoDeCampo.Texto => FormaDoValorDoItem.Texto,
        TipoDeCampo.Numero => FormaDoValorDoItem.Numero,
        TipoDeCampo.SimOuNao => FormaDoValorDoItem.SimOuNao,
        TipoDeCampo.Data => FormaDoValorDoItem.Data,
        TipoDeCampo.EscolhaUnica or TipoDeCampo.Municipio => FormaDoValorDoItem.Opcao,
        TipoDeCampo.EscolhaMultipla => FormaDoValorDoItem.Opcoes,
        TipoDeCampo.Endereco => FormaDoValorDoItem.Endereco,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
    };

    public static FormaDoValorDoItem De(TipoDoDado tipo, QuantidadeDeValores quantidade) => tipo switch
    {
        TipoDoDado.Texto => FormaDoValorDoItem.Texto,
        TipoDoDado.Numerico => FormaDoValorDoItem.Numero,
        TipoDoDado.SimOuNao => FormaDoValorDoItem.SimOuNao,
        TipoDoDado.Data => FormaDoValorDoItem.Data,
        TipoDoDado.Opcao => De(quantidade),
        TipoDoDado.Endereco => FormaDoValorDoItem.Endereco,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
    };

    private static FormaDoValorDoItem De(QuantidadeDeValores quantidade) => quantidade switch
    {
        QuantidadeDeValores.ValorUnico => FormaDoValorDoItem.Opcao,
        QuantidadeDeValores.VariosValores => FormaDoValorDoItem.Opcoes,
        _ => throw new ArgumentOutOfRangeException(nameof(quantidade)),
    };
}
