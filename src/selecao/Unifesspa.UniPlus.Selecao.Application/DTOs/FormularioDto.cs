namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Uma etapa do formulário: seção ou bloco de sistema, em token canônico, com a exibição
/// condicional da seção — nula quando ela sempre aparece.
/// </summary>
public sealed record EtapaFormularioDto(
    string Codigo,
    int Ordem,
    string Tipo,
    string? Bloco,
    string Titulo,
    string? Descricao,
    string? Aviso,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Exibicao);

/// <summary>
/// O formulário de uma finalidade (UNI-REQ-0144): a fase, o título, o modelo de origem, as etapas,
/// os itens em ordem e os termos exigidos.
/// </summary>
public sealed record FormularioDto(
    string Finalidade,
    Guid? FaseId,
    string? Titulo,
    Guid? ModeloOrigemId,
    string? ModeloOrigemCodigo,
    IReadOnlyList<EtapaFormularioDto> Etapas,
    IReadOnlyList<FatoColetadoDto> FatosColetados,
    IReadOnlyList<TermoExigidoDto> Termos);
