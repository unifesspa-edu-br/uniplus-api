namespace Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// O que a Portal usa de um ato normativo da consulta unificada de Publicações.
/// </summary>
/// <remarks>
/// O assinante do ato não é trazido: é dado pessoal, e a página pública não tem uso para ele.
/// </remarks>
/// <param name="Id">Identificador do ato.</param>
/// <param name="TipoCodigo">Código do tipo do ato.</param>
/// <param name="TipoNome">
/// Nome do tipo copiado por valor no registro do ato. Nulo nos atos registrados antes de o nome
/// passar a ser copiado.
/// </param>
/// <param name="Ano">Ano da numeração declarada pelo órgão.</param>
/// <param name="Numero">Número declarado do ato, quando declarado.</param>
/// <param name="DataPublicacao">Data que o documento do ato declara.</param>
/// <param name="CongelaConfiguracao">Se o ato cria versão da configuração do certame.</param>
/// <param name="MotivoRetificacao">Motivo, quando o ato retifica outro.</param>
public sealed record AtoDaOrigem(
    Guid Id,
    string TipoCodigo,
    string? TipoNome,
    int Ano,
    string? Numero,
    DateOnly DataPublicacao,
    bool CongelaConfiguracao,
    string? MotivoRetificacao);
