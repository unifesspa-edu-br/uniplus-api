namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Procedimentos de revisão de recursos reconhecidos pelo catálogo.</summary>
public enum RegraDeProcedimentoDoRecurso
{
    [CodigoDeContrato("PROCEDIMENTO_RECURSO_RECORRECAO", "Nova correção", Descricao = "Realiza nova correção do material apresentado.")]
    Recorrecao = 1,
    [CodigoDeContrato("PROCEDIMENTO_RECURSO_CONFERENCIA", "Conferência", Descricao = "Confere a comprovação e aplica a pontuação prevista.")]
    Conferencia = 2,
    [CodigoDeContrato("PROCEDIMENTO_RECURSO_CORRECAO_MATERIAL", "Correção de registro", Descricao = "Corrige erro de registro, soma ou presença.")]
    CorrecaoMaterial = 3,
    [CodigoDeContrato("PROCEDIMENTO_RECURSO_RECALCULO", "Novo cálculo", Descricao = "Refaz o cálculo a partir dos dados conferidos.")]
    Recalculo = 4,
}
