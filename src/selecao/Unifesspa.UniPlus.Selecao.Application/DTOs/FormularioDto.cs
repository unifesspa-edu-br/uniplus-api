namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Como a etapa se apresenta ao candidato: o título, a descrição e o aviso. Tem a forma de
/// <see cref="ApresentacaoDaEtapaInput"/>, que a escrita aceita.
/// </summary>
public sealed record ApresentacaoDaEtapaDto(string Titulo, string? Descricao, string? Aviso);

/// <summary>
/// Uma etapa do formulário: seção ou bloco de sistema, em token canônico, com a apresentação e a exibição
/// condicional da seção — nula quando ela sempre aparece.
/// </summary>
public sealed record EtapaFormularioDto(
    string Codigo,
    int Ordem,
    string Tipo,
    string? Bloco,
    ApresentacaoDaEtapaDto Apresentacao,
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

/// <summary>O modelo de formulário de que o formulário do processo partiu: o identificador e o código dele.</summary>
public sealed record ModeloDeOrigemDto(Guid Id, string Codigo);

/// <summary>
/// O que o formulário coleta do candidato: os itens em ordem e os grupos repetíveis, na forma do corpo do PUT
/// dos itens do formulário.
/// </summary>
public sealed record ColetaDoFormularioDto(IReadOnlyList<FatoColetadoDto> Itens, IReadOnlyList<GrupoColetadoDto> Grupos);

/// <summary>
/// O formulário de uma finalidade (UNI-REQ-0144): a fase, o título, o modelo de origem — nulo quando o formulário não
/// partiu de um modelo —, as etapas, o que ele coleta e os termos exigidos.
/// </summary>
public sealed record FormularioDto(
    string Finalidade,
    Guid? FaseId,
    string? Titulo,
    ModeloDeOrigemDto? ModeloOrigem,
    IReadOnlyList<EtapaFormularioDto> Etapas,
    ColetaDoFormularioDto Coleta,
    IReadOnlyList<TermoExigidoDto> Termos);
