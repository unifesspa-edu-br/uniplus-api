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
/// Quantas ocorrências o grupo repetível admite: o mínimo, o máximo — nulo quando não há — e se o próprio
/// candidato é uma delas. Tem a forma de <see cref="OcorrenciasDoGrupoInput"/>, que a escrita aceita.
/// </summary>
public sealed record OcorrenciasDoGrupoDto(int Minimo, int? Maximo, bool IncluiCandidato);

/// <summary>
/// Um grupo repetível do formulário (UNI-REQ-0146): a posição na ordem dos itens, a seção, o rótulo, as
/// ocorrências admitidas, a exibição — nula quando sempre aparece — e a obrigatoriedade em
/// <see cref="Regras"/> e os campos de cada ocorrência, na ordem dentro do grupo.
/// </summary>
public sealed record GrupoColetadoDto(
    string Codigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    OcorrenciasDoGrupoDto Ocorrencias,
    ExibicaoEObrigatoriedadeDto Regras,
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
