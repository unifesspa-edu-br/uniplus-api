namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O que o sistema sabe apurar de uma inscrição para homologá-la. Só entra aqui o que já é apurável:
/// condição que o sistema ainda não apura é recusada, nunca guardada para depois (UNI-REQ-0265).
/// </summary>
public enum TipoDeCondicaoDeHomologacao
{
    /// <summary>A inscrição tem resultado no retorno do INEP da edição indicada.</summary>
    [CodigoDeContrato("RESULTADO_NO_RETORNO_DO_INEP", "Resultado no retorno do INEP", Descricao = "A inscrição tem resultado no retorno do INEP.")]
    ResultadoNoRetornoDoInep = 1,
}
