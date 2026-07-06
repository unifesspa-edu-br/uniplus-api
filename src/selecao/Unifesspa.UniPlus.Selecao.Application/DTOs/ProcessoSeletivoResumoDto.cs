namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Projeção de resumo do <c>ProcessoSeletivo</c> para a listagem paginada
/// (Story #758). Omite as coleções de configuração do agregado (etapas, oferta
/// de atendimento e as demais dimensões conforme forem modeladas) de propósito:
/// a listagem não as carrega e devolver coleções vazias no DTO completo faria
/// um processo já configurado parecer incompleto. O detalhe completo vive em
/// <c>GET /{id}</c> (<see cref="ProcessoSeletivoDto"/>).
/// </summary>
public sealed record ProcessoSeletivoResumoDto(
    Guid Id,
    string Nome,
    string Tipo,
    string Status,
    DateTimeOffset CriadoEm);
