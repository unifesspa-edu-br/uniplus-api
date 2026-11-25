namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação de uma versão do perfil de acesso.</summary>
public enum SituacaoDaVersao
{
    /// <summary>Criada e ainda sem efeito: espera o documento instituidor ser verificado.</summary>
    [CodigoDeContrato("PENDENTE", "Pendente")]
    Pendente = 1,

    /// <summary>Composição em vigor do perfil. Há no máximo uma por perfil.</summary>
    [CodigoDeContrato("VIGENTE", "Vigente")]
    Vigente = 2,

    /// <summary>Foi a vigente e deu lugar a uma versão posterior.</summary>
    [CodigoDeContrato("ENCERRADA", "Encerrada")]
    Encerrada = 3,

    /// <summary>Terminal: retirada antes de entrar em vigor, por quem a criou, por quem mantém perfis ou pela contenção.</summary>
    [CodigoDeContrato("CANCELADA", "Cancelada")]
    Cancelada = 4,

    /// <summary>Terminal: uma etapa da aprovação a recusou.</summary>
    [CodigoDeContrato("RECUSADA", "Recusada")]
    Recusada = 5,

    /// <summary>Terminal: o prazo de 15 dias passou sem a aprovação.</summary>
    [CodigoDeContrato("EXPIRADA", "Expirada")]
    Expirada = 6,
}
