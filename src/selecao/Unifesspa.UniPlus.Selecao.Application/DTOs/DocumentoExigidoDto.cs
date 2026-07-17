namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// DTO de leitura de <see cref="Domain.Entities.DocumentoExigido"/> (Story #554, PR-a).
/// Compõe <c>ProcessoSeletivoDto</c> — não há rota aninhada própria de leitura, mesmo
/// padrão de <c>FaseCronogramaDto</c>.
/// </summary>
public sealed record DocumentoExigidoDto(
    Guid Id,
    Guid ExigidoNaFaseId,
    string TipoDocumentoCodigo,
    string TipoDocumentoNome,
    string TipoDocumentoCategoria,
    string Aplicabilidade,
    bool Obrigatorio,
    string? ConsequenciaIndeferimento,
    Guid? GrupoSatisfacaoId);
