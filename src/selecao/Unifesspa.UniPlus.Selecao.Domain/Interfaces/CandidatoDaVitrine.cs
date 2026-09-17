namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Uma linha candidata da vitrine, como o banco a devolve: identidade, nome e a janela pela qual a
/// ordenação acontece.
/// </summary>
/// <remarks>
/// Candidata, e não item final: a visibilidade pública exige ato normativo registrado, e isso vive
/// em outro módulo — nenhuma consulta SQL daqui pode afirmá-lo. Quem monta a página confere os atos
/// da linhagem e descarta o que não tem.
/// </remarks>
/// <param name="ProcessoSeletivoId">Identificador do processo.</param>
/// <param name="Nome">Título do certame.</param>
/// <param name="InscricoesAte">Encerramento da janela de inscrição da versão vigente.</param>
public readonly record struct CandidatoDaVitrine(Guid ProcessoSeletivoId, string Nome, DateTimeOffset InscricoesAte);

/// <summary>Filtro por situação da janela de inscrição, resolvido contra o instante da consulta.</summary>
public enum SituacaoDoCertame
{
    /// <summary>Sem filtro: publicado aparece, encerrado ou não.</summary>
    Todas = 0,

    /// <summary>Apenas os que ainda recebem inscrição.</summary>
    InscricoesAbertas = 1,

    /// <summary>Apenas os que já encerraram.</summary>
    Encerradas = 2,
}
