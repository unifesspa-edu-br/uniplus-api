namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum ConvencaoDeContagemDoPrazo
{
    [CodigoDeContrato("CONTAGEM_PRAZO_EXCLUI_DIA_INICIAL", "Exclui o dia inicial", Descricao = "Exclui o dia do evento inicial e conta os dias úteis seguintes.")]
    ExcluiDiaInicial = 1,

    [CodigoDeContrato("CONTAGEM_PRAZO_HORAS_UTEIS_DESDE_O_EVENTO_INICIAL", "Conta horas úteis desde o evento inicial", Descricao = "Conta as horas em dias úteis a partir do instante do evento inicial.")]
    HorasUteisDesdeOEventoInicial = 2,

    [CodigoDeContrato("CONTAGEM_PRAZO_AVANCA_DATA_UTIL", "Avança para a data útil", Descricao = "Avança pelas datas úteis, mantendo a hora do evento inicial.")]
    AvancaDataUtil = 3,
}
