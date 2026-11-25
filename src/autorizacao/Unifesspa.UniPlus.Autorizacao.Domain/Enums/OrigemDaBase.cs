namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>De onde vem a base de uma permissão na concessão: a classificação e o escopo contra os quais a reclassificação se mede.</summary>
public enum OrigemDaBase
{
    /// <summary>A concessão trocou de versão e manteve a permissão: a base é o que a versão que saiu guardava.</summary>
    [CodigoDeContrato("MIGRACAO", "Migração")]
    Migracao = 1,

    /// <summary>A permissão saiu da composição da concessão: encerra a base dela.</summary>
    [CodigoDeContrato("SAIDA", "Saída")]
    Saida = 2,

    /// <summary>As etapas exigidas liberaram a reclassificação: a base passa a ser a do catálogo da reclassificação.</summary>
    [CodigoDeContrato("LIBERACAO", "Liberação")]
    Liberacao = 3,

    /// <summary>Uma troca de catálogo mudou a permissão: a base acompanha a descida da classe e guarda o escopo anterior.</summary>
    [CodigoDeContrato("CONCILIACAO", "Conciliação")]
    Conciliacao = 4,
}
