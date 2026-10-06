namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// O formulário do modelo avaliado contra respostas simuladas: o que cada item, cada termo e cada grupo
/// repetível faria diante delas. Os estados são <c>VERDADEIRO</c>, <c>FALSO</c> ou
/// <c>INDETERMINADO</c>, quando dependem de resposta ainda não dada.
/// </summary>
public sealed record PreVisualizacaoDoModeloDto(
    IReadOnlyList<ItemPreVisualizadoDto> Itens,
    IReadOnlyList<TermoPreVisualizadoDto> Termos,
    IReadOnlyList<GrupoPreVisualizadoDto> Grupos,
    IReadOnlyList<SecaoPreVisualizadaDto> Secoes);

/// <summary>Uma seção do modelo avaliada: se ela aparece.</summary>
public sealed record SecaoPreVisualizadaDto(string Codigo, string Visivel);

/// <summary>
/// As opções que um campo de escolha deixa escolher diante das respostas: as que valem em definitivo e
/// se já são todas — falso enquanto uma resposta de que elas dependem ainda não foi dada.
/// </summary>
public sealed record OpcoesPreVisualizadasDto(IReadOnlyList<string> Codigos, bool Definitivas);

/// <summary>
/// Um grupo repetível avaliado: se aparece e é obrigatório, se a contagem de ocorrências e a
/// ocorrência do próprio candidato valem, e cada ocorrência com os campos dela.
/// </summary>
public sealed record GrupoPreVisualizadoDto(
    string Codigo,
    string EtapaCodigo,
    string Visivel,
    string Obrigatorio,
    bool ContagemValida,
    bool OcorrenciaDoCandidatoValida,
    IReadOnlyList<OcorrenciaPreVisualizadaDto> Ocorrencias);

/// <summary>Uma ocorrência avaliada e os campos dela.</summary>
public sealed record OcorrenciaPreVisualizadaDto(string Id, IReadOnlyList<ItemPreVisualizadoDto> Itens);

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
    string? MensagemDoImpedimento,
    OpcoesPreVisualizadasDto? Opcoes);

/// <summary>Um termo avaliado: se aparece e se é obrigatório.</summary>
public sealed record TermoPreVisualizadoDto(string Codigo, string Visivel, string Obrigatorio);
