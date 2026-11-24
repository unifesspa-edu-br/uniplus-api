namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Valores de FundamentoIsencao.</summary>
public enum FundamentoIsencao
{
    [CodigoDeContrato("CADASTRO_UNICO", "Cadastro Único", Descricao = "Candidato de família de baixa renda inscrita no Cadastro Único do Governo Federal.")]
    CadastroUnico = 1,
    [CodigoDeContrato("DOACAO_MEDULA_OSSEA", "Doação de medula óssea", Descricao = "Candidato doador de medula óssea.")]
    DoacaoMedulaOssea = 2,
    [CodigoDeContrato("CARENCIA_SOCIOECONOMICA", "Carência socioeconômica", Descricao = "Candidato com renda familiar per capita de até um salário mínimo e meio que tenha cursado o ensino médio completo em escola pública ou como bolsista integral da rede privada — os dois requisitos são cumulativos (Lei nº 12.799/2013).")]
    CarenciaSocioeconomica = 3,
}
