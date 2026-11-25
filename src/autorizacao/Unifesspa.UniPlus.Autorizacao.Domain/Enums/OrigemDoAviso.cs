namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O fato que gerou um aviso da reclassificação.</summary>
public enum OrigemDoAviso
{
    /// <summary>Uma permissão de perfil comum foi reclassificada para cima ou mudou de escopo.</summary>
    [CodigoDeContrato("RECLASSIFICACAO", "Reclassificação")]
    Reclassificacao = 1,

    /// <summary>Um perfil de governança divergiu do esperado.</summary>
    [CodigoDeContrato("DIVERGENCIA", "Divergência")]
    Divergencia = 2,
}
