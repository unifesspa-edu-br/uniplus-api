namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que levou o sistema a fechar a suspensão.</summary>
public enum OrigemDaRetomada
{
    /// <summary>A decisão de uma etapa completou as exigidas.</summary>
    [CodigoDeContrato("DECISAO_DA_ETAPA", "Decisão da etapa")]
    DecisaoDaEtapa = 1,

    /// <summary>Uma versão entrou em vigor e o perfil deixou de exigir a etapa que faltava.</summary>
    [CodigoDeContrato("TROCA_DE_VIGENCIA", "Troca de vigência")]
    TrocaDeVigencia = 2,

    /// <summary>A reclassificação de uma permissão tirou do perfil a classe que exigia a etapa que faltava.</summary>
    [CodigoDeContrato("RECLASSIFICACAO", "Reclassificação")]
    Reclassificacao = 3,
}
