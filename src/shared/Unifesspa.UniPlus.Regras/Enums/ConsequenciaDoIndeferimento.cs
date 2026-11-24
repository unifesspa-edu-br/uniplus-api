namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum ConsequenciaDoIndeferimento
{
    [CodigoDeContrato("ELIMINA", "Eliminação", Descricao = "Retira a pessoa do processo seletivo.")]
    Elimina = 1,
    [CodigoDeContrato("RECLASSIFICA_AC", "Ampla concorrência", Descricao = "Passa a pessoa para a ampla concorrência.")]
    ReclassificaAc = 2,
    [CodigoDeContrato("REMOVE_VANTAGEM", "Perda da vantagem", Descricao = "Retira a vantagem solicitada.")]
    RemoveVantagem = 3,
    [CodigoDeContrato("ABRE_PRAZO_PARA_REENVIO", "Prazo para reenvio", Descricao = "Abre prazo para reenviar o documento.")]
    AbrePrazoParaReenvio = 4,
}
