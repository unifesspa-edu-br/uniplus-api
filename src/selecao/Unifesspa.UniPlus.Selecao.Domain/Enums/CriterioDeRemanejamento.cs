namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum CriterioDeRemanejamento
{
    [CodigoDeContrato("REMANEJAMENTO_ORDEM_LEI_12711", "Segue a ordem da Lei 12.711", Descricao = "Remaneja as vagas conforme a ordem prevista na Lei 12.711.")]
    OrdemDaLei12711 = 1,
}
