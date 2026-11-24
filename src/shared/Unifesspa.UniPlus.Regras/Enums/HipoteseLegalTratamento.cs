namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

[DescricaoDoContrato("Fundamento legal para o tratamento de dados pessoais.")]
public enum HipoteseLegalTratamento
{
    [CodigoDeContrato("CONSENTIMENTO", "Consentimento", Descricao = "Consentimento da pessoa titular dos dados.")]
    Consentimento = 1,

    [CodigoDeContrato("CUMPRIMENTO_OBRIGACAO_LEGAL", "Cumprimento de obrigação legal", Descricao = "Cumprimento de obrigação legal ou regulatória.")]
    CumprimentoObrigacaoLegal = 2,

    [CodigoDeContrato("EXECUCAO_POLITICAS_PUBLICAS", "Execução de políticas públicas", Descricao = "Execução de políticas públicas pela administração pública.")]
    ExecucaoPoliticasPublicas = 3,

    [CodigoDeContrato("ESTUDOS_POR_ORGAO_DE_PESQUISA", "Estudos por órgão de pesquisa", Descricao = "Realização de estudos por órgão de pesquisa.")]
    EstudosPorOrgaoDePesquisa = 4,

    [CodigoDeContrato("EXECUCAO_CONTRATO", "Execução de contrato", Descricao = "Execução de contrato do qual a pessoa titular é parte.")]
    ExecucaoContrato = 5,

    [CodigoDeContrato("EXERCICIO_REGULAR_DE_DIREITOS", "Exercício regular de direitos", Descricao = "Exercício regular de direitos em processo.")]
    ExercicioRegularDeDireitos = 6,

    [CodigoDeContrato("PROTECAO_DA_VIDA", "Proteção da vida", Descricao = "Proteção da vida ou da integridade física.")]
    ProtecaoDaVida = 7,

    [CodigoDeContrato("TUTELA_DA_SAUDE", "Tutela da saúde", Descricao = "Proteção da saúde da pessoa titular.")]
    TutelaDaSaude = 8,

    [CodigoDeContrato("INTERESSE_LEGITIMO", "Interesse legítimo", Descricao = "Atendimento a interesse legítimo do controlador ou de terceiro.")]
    InteresseLegitimo = 9,

    [CodigoDeContrato("PROTECAO_DO_CREDITO", "Proteção do crédito", Descricao = "Proteção do crédito.")]
    ProtecaoDoCredito = 10,

    [CodigoDeContrato("PREVENCAO_A_FRAUDE", "Prevenção à fraude", Descricao = "Prevenção à fraude e segurança da pessoa titular.")]
    PrevencaoAFraude = 11,
}
