namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum ConsequenciaDoDescumprimento
{
    [CodigoDeContrato("ELIMINA_DO_PROCESSO", "Eliminação do processo", Descricao = "Retira a pessoa do processo, depois dos procedimentos e recursos cabíveis.")]
    EliminaDoProcesso = 1,
    [CodigoDeContrato("RETIRA_A_MODALIDADE", "Retirada da modalidade", Descricao = "A pessoa perde a modalidade a que o requisito se refere e continua nas demais em que concorre.")]
    RetiraAModalidade = 2,
    [CodigoDeContrato("RETIRA_O_ACRESCIMO", "Retirada só do acréscimo do Fator de Atenuação das Desigualdades de Acesso", Descricao = "A pessoa perde só o acréscimo; não é eliminada nem perde modalidade.")]
    RetiraOAcrescimo = 3,
}
