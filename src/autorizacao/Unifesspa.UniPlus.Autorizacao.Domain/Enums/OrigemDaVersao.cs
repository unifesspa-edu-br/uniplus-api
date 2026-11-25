namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Como a versão nasceu: por uma proposta que espera aprovação ou por uma remoção, que vale na hora.</summary>
public enum OrigemDaVersao
{
    /// <summary>Criada por quem mantém perfis, pendente até a aprovação.</summary>
    [CodigoDeContrato("PROPOSTA", "Proposta")]
    Proposta = 1,

    /// <summary>Criada pela remoção de permissões: entra em vigor sem aprovação.</summary>
    [CodigoDeContrato("REMOCAO", "Remoção")]
    Remocao = 2,
}
