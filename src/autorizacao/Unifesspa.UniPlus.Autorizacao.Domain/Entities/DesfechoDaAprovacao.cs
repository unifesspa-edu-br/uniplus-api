namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>O que a aprovação de uma etapa deixou: o pedido concluído, a conceder, ou a aguardar a proteção de dados.</summary>
public enum DesfechoDaAprovacao
{
    /// <summary>A última aprovação exigida foi dada: quem chama concede o acesso.</summary>
    Concluido = 1,

    /// <summary>A aprovação do gestor foi dada, e o perfil exige a da proteção de dados.</summary>
    AguardaProtecaoDeDados = 2,
}
