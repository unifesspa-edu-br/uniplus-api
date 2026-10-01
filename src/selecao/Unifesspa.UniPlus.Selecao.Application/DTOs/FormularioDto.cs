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
/// Um grupo repetível do formulário (UNI-REQ-0146): a posição na ordem dos itens, a seção, o
/// rótulo, o mínimo e o máximo de ocorrências, a exibição — nula quando sempre aparece —, a
/// obrigatoriedade e os campos de cada ocorrência, na ordem dentro do grupo.
/// </summary>
public sealed record GrupoColetadoDto(
    string Codigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    int Minimo,
    int Maximo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Exibicao,
    ObrigatoriedadeDto Obrigatoriedade,
    IReadOnlyList<FatoColetadoDto> Subitens);

/// <summary>
/// O formulário de uma finalidade (UNI-REQ-0144): a fase, o título, o modelo de origem, as etapas,
/// os itens em ordem, os grupos repetíveis e os termos exigidos.
/// </summary>
public sealed record FormularioDto(
    string Finalidade,
    Guid? FaseId,
    string? Titulo,
    Guid? ModeloOrigemId,
    string? ModeloOrigemCodigo,
    IReadOnlyList<EtapaFormularioDto> Etapas,
    IReadOnlyList<FatoColetadoDto> FatosColetados,
    IReadOnlyList<TermoExigidoDto> Termos,
    IReadOnlyList<GrupoColetadoDto> Grupos);
