namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Onde a proteção de dados de um tipo de documento ou de um fato está no circuito de aprovação
/// da proteção de dados (ADR-0153). Só <see cref="Aprovada"/> libera o item para a configuração e
/// a publicação de processo seletivo.
/// </summary>
public enum SituacaoDaAprovacao
{
    /// <summary>Sentinela — situação não informada.</summary>
    Nenhuma = 0,

    /// <summary>
    /// Criado ou alterado na proteção, ainda fora de qualquer solicitação: ninguém foi avisado.
    /// </summary>
    Pendente,

    /// <summary>Enviado à proteção de dados numa solicitação, aguardando a decisão.</summary>
    Solicitada,

    /// <summary>Aprovada pela proteção de dados, ou pelo aceite da recomendação dela.</summary>
    Aprovada,

    /// <summary>A proteção de dados recomendou a classificação correta; o administrador a aceita ou edita o item.</summary>
    AjusteRecomendado,

    /// <summary>Reprovada pela proteção de dados, com a justificativa; o administrador só pode editar o item.</summary>
    Reprovada,
}
