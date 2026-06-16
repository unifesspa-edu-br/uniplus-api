namespace Unifesspa.UniPlus.Authorization.Contracts;

using System.ComponentModel;

using Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Motivo estrutural de uma negativa de autorização (ADR-0078). O motivo é um
/// código de um <b>conjunto fechado</b> (<see cref="MotivoNegativa"/>); o tipo
/// não tem campo de texto livre e, por construção, é <b>incapaz de veicular
/// dado pessoal</b> (identidade, CPF, nome do titular). Qualquer mensagem
/// legível ao humano é derivada do código no consumidor (i18n), nunca entrada
/// livre — LGPD-by-design.
/// </summary>
public sealed record DenyReason
{
    /// <summary>Código do motivo da negativa, do conjunto fechado.</summary>
    public MotivoNegativa Codigo { get; }

    private DenyReason(MotivoNegativa codigo) => Codigo = codigo;

    /// <summary>
    /// Constrói um <see cref="DenyReason"/> a partir de um código do conjunto
    /// fechado <see cref="MotivoNegativa"/>. Rejeita um valor fora do conjunto
    /// (<i>cast</i> de inteiro arbitrário): a negativa só se explica por um
    /// motivo conhecido. Lança em vez de retornar <c>Result</c> porque um código
    /// fora do conjunto é violação de contrato de programação — o motivo é
    /// produzido pelo motor de decisão, não por dado externo.
    /// </summary>
    public static DenyReason De(MotivoNegativa codigo)
    {
        if (!Enum.IsDefined(codigo))
        {
            throw new InvalidEnumArgumentException(nameof(codigo), (int)codigo, typeof(MotivoNegativa));
        }

        return new DenyReason(codigo);
    }
}
