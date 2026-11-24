namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum RegraDeDistribuicaoDeVagas
{
    [CodigoDeContrato("DISTRIBUICAO_LEI_12711", "Distribuição pela Lei 12.711", Descricao = "Calcula a distribuição conforme a Lei 12.711.")]
    Lei12711 = 1,
    [CodigoDeContrato("DISTRIBUICAO_LEI_12711_COM_AMPLA_CONCORRENCIA_E_PESSOA_COM_DEFICIENCIA", "Lei 12.711 com ampla concorrência e pessoa com deficiência", Descricao = "Aplica a Lei 12.711 com reserva adicional para pessoa com deficiência.")]
    Lei12711ComAmplaConcorrenciaEPessoaComDeficiencia = 2,
    [CodigoDeContrato("DISTRIBUICAO_INSTITUCIONAL", "Distribuição institucional", Descricao = "Usa as quantidades fixadas no edital.")]
    Institucional = 3,
    [CodigoDeContrato("DISTRIBUICAO_POR_ACRESCIMO_INDIGENA_E_QUILOMBOLA", "Vagas por acréscimo indígena e quilombola", Descricao = "Usa vagas por acréscimo para indígenas e quilombolas.")]
    PorAcrescimoIndigenaEQuilombola = 4,
    [CodigoDeContrato("DISTRIBUICAO_COM_AMPLA_CONCORRENCIA_E_PESSOA_COM_DEFICIENCIA", "Ampla concorrência e pessoa com deficiência", Descricao = "Usa vagas de ampla concorrência e de pessoa com deficiência.")]
    ComAmplaConcorrenciaEPessoaComDeficiencia = 5,
}
