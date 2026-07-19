namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

/// <summary>
/// DTO de leitura de <see cref="Domain.Entities.DocumentoExigido"/> (Story #554, PR #895).
/// Compõe <c>ProcessoSeletivoDto</c> — não há rota aninhada própria de leitura, mesmo
/// padrão de <c>FaseCronogramaDto</c>.
/// </summary>
/// <remarks>
/// <see cref="FormatosPermitidos"/> (Story #918) é o mesmo valor JSON polimórfico do
/// comando (<c>ItemDocumentoExigidoInput.FormatosPermitidos</c>) — a string <c>"QUALQUER"</c>
/// ou o array de <c>{formato, tamanhoMaximoBytesMax}</c> — round-tripável direto de volta
/// pelo mesmo PUT, sem transformação do cliente.
/// </remarks>
public sealed record DocumentoExigidoDto(
    Guid Id,
    Guid ExigidoNaFaseId,
    Guid TipoDocumentoOrigemId,
    string TipoDocumentoCodigo,
    string TipoDocumentoNome,
    string TipoDocumentoCategoria,
    string Aplicabilidade,
    bool Obrigatorio,
    string? ConsequenciaIndeferimento,
    Guid? GrupoSatisfacaoId,
    IReadOnlyList<CondicaoGatilhoDto> Condicoes,
    IReadOnlyList<BaseLegalDto> BasesLegais,
    IdadeMaximaEmissaoDto? IdadeMaximaEmissao,
    JsonElement FormatosPermitidos,
    int? TamanhoMaximoBytes);

/// <summary>
/// DTO de leitura de <see cref="Domain.ValueObjects.IdadeMaximaEmissao"/> (Story #554,
/// PR #900, issue #893). Mesmo formato flat de <c>IdadeMaximaEmissaoInput</c> (comando de
/// escrita) — round-trip GET→PUT direto.
/// </summary>
public sealed record IdadeMaximaEmissaoDto(int Valor, string Unidade, string ReferenciaTipo, DateOnly? Data, Guid? ReferenciaFaseId);

/// <summary>
/// DTO de leitura de <see cref="Domain.Entities.CondicaoGatilho"/> (Story #554, PR #896,
/// issue #892). <see cref="Operador"/>/<see cref="Valor"/> seguem o mesmo formato flat de
/// <c>CondicaoGatilhoInput</c> (comando de escrita) — <see cref="Valor"/> é o texto JSON
/// canônico (<c>JsonElement.GetRawText()</c>), round-tripável direto de volta pelo mesmo
/// PUT (<c>DefinirDocumentosExigidosCommandHandler.InterpretarValor</c> o reparseia como JSON).
/// </summary>
public sealed record CondicaoGatilhoDto(Guid Id, int Clausula, string Fato, string Operador, string Valor);

/// <summary>
/// DTO de leitura de <see cref="Domain.Entities.DocumentoExigidoBaseLegal"/> (Story #554,
/// PR #898, issue #549). Mesmo formato flat de <c>BaseLegalInput</c> (comando de escrita) —
/// round-trip GET→PUT direto, incluindo bases <c>PENDENTE</c> (a projeção "somente
/// RESOLVIDO" é interna ao domínio — <c>DocumentoExigido.BasesLegaisResolvidas</c> — e da
/// materialização do envelope na PR #903, não deste DTO de edição).
/// </summary>
public sealed record BaseLegalDto(Guid Id, string Referencia, string Abrangencia, string Status, string? Observacao);
