namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação apresentada para uma concessão na revisão.</summary>
public enum SituacaoDaConcessaoNaRevisao
{
    [CodigoDeContrato("AGUARDANDO_DECISOES", "Aguardando decisão da gestão")]
    AguardandoDecisoes = 1,
    [CodigoDeContrato("RETIDA", "Retida")]
    Retida = 2,
    [CodigoDeContrato("AGUARDANDO_PROTECAO_DE_DADOS", "Aguardando proteção de dados")]
    AguardandoProtecaoDeDados = 3,
    [CodigoDeContrato("MIGRADA", "Migrada")]
    Migrada = 4,
    [CodigoDeContrato("RECUSADA", "Recusada")]
    Recusada = 5,
    [CodigoDeContrato("NAO_MIGRADA", "Não migrada")]
    NaoMigrada = 6,
    [CodigoDeContrato("FORA_DA_LISTA", "Fora da lista")]
    ForaDaLista = 7,
}
