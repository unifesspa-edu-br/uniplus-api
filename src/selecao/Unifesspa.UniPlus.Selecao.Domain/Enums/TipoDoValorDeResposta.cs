namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>O tipo em que o valor de uma resposta da inscrição é gravado.</summary>
public enum TipoDoValorDeResposta
{
    Booleano = 1,
    Numero = 2,

    /// <summary>Valor de vocabulário fechado: opção categórica, município, UF. É o que a classificação filtra.</summary>
    Codigo = 3,

    /// <summary>Texto livre, como um nome.</summary>
    Texto = 4,

    Data = 5,
    Endereco = 6,
}
