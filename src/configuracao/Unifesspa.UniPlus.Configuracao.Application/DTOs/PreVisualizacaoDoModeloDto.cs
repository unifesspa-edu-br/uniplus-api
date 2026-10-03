namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// O formulário do modelo avaliado contra respostas simuladas: o que cada item e cada termo faria
/// diante delas. Os estados são <c>VERDADEIRO</c>, <c>FALSO</c> ou <c>INDETERMINADO</c>, quando
/// dependem de resposta ainda não dada.
/// </summary>
public sealed record PreVisualizacaoDoModeloDto(IReadOnlyList<ItemPreVisualizadoDto> Itens, IReadOnlyList<TermoPreVisualizadoDto> Termos);

/// <summary>
/// Um item avaliado: se aparece, se é obrigatório, os tipos das restrições que a resposta viola e se
/// a resposta impede a inscrição, com a mensagem ao candidato quando o item tem impedimento.
/// </summary>
public sealed record ItemPreVisualizadoDto(
    string FatoCodigo,
    string EtapaCodigo,
    string Visivel,
    string Obrigatorio,
    IReadOnlyList<string> RestricoesVioladas,
    string Impedido,
    string? MensagemDoImpedimento);

/// <summary>Um termo avaliado: se aparece e se é obrigatório.</summary>
public sealed record TermoPreVisualizadoDto(string Codigo, string Visivel, string Obrigatorio);
