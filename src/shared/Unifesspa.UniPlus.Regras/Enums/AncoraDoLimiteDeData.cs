namespace Unifesspa.UniPlus.Regras.Enums;

/// <summary>De onde um limite da faixa de data é contado.</summary>
public enum AncoraDoLimiteDeData
{
    /// <summary>Uma data do calendário, a mesma para todo candidato.</summary>
    DataFixa = 1,

    /// <summary>O dia do envio da resposta, deslocado em anos e dias.</summary>
    Envio = 2,
}
