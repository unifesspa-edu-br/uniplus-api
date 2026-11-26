namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Ação aplicada quando a comprovação exigida para a modalidade é indeferida.</summary>
public enum AcaoQuandoIndeferido
{
    [CodigoDeContrato("RETIRA_A_MODALIDADE", "Retirada da modalidade", Descricao = "A pessoa perde a modalidade e continua nas demais em que concorre.")]
    RetiraAModalidade = 1,
}
