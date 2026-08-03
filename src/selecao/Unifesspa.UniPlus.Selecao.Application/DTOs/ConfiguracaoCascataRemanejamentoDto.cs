namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Projeção de leitura de uma posição da fila legal de remanejamento (Story #575).</summary>
public sealed record DestinoRemanejamentoDto(Guid Id, string ModalidadeOrigemCodigo, int Ordem, string ModalidadeDestinoCodigo);

/// <summary>
/// Projeção de leitura da cascata de remanejamento (RN-CASCATA-1..5, Story
/// #575) — a sequência que a flag <c>SegueCascata</c> de
/// <c>ModalidadeSelecionada</c> invoca. <see langword="null"/> é o estado
/// válido "sem cascata configurada".
/// </summary>
public sealed record ConfiguracaoCascataRemanejamentoDto(
    Guid Id,
    ReferenciaRegraDto Regra,
    string FallbackCodigo,
    IReadOnlyList<DestinoRemanejamentoDto> Destinos);
