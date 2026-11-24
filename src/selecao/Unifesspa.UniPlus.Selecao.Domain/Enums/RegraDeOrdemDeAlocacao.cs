namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Regras reconhecidas pelo catálogo.</summary>
public enum RegraDeOrdemDeAlocacao
{
    [CodigoDeContrato("ALOCACAO_PRIMEIRA_OPCAO_PRIORITARIA", "Prioriza a primeira opção de curso",
        Descricao = "Preenche as vagas disponíveis para a primeira opção antes de atender à segunda opção.")]
    PrimeiraOpcaoPrioritaria = 1,
}
