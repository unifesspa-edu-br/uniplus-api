namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum RegraDeEliminacaoPorCalculo
{
    [CodigoDeContrato("ELIMINACAO_NOTA_MINIMA_ETAPA", "Elimina quem fica abaixo da nota mínima na etapa", Descricao = "Elimina quem não alcança a nota mínima da etapa referenciada.")]
    NotaMinimaNaEtapa = 1,

    [CodigoDeContrato("ELIMINACAO_CORTE_EM_AREA", "Elimina quem fica abaixo do mínimo numa área do ENEM", Descricao = "Elimina quem não alcança o mínimo definido para uma área do ENEM.")]
    CorteEmAreaDoEnem = 2,

    [CodigoDeContrato("ELIMINACAO_ZERO_EM_AREA", "Elimina quem tira zero em alguma área do ENEM", Descricao = "Elimina quem obtém nota zero em qualquer área do ENEM.")]
    ZeroEmAreaDoEnem = 3,

    [CodigoDeContrato("ELIMINACAO_FALTA_EM_DIA_DE_PROVA_ENEM", "Elimina quem falta a um dia de prova do ENEM", Descricao = "Elimina quem falta a pelo menos um dia de prova do ENEM utilizado.")]
    FaltaEmDiaDeProvaDoEnem = 4,

    [CodigoDeContrato("ELIMINACAO_AUSENCIA_NA_ETAPA", "Elimina quem falta à etapa", Descricao = "Elimina quem falta à etapa referenciada.")]
    AusenciaNaEtapa = 5,
}
