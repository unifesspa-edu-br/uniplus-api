namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

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
    CodigoComRotulo<FinalidadeFormulario> Finalidade,
    Guid? FaseId,
    string? Titulo,
    ModeloDeOrigemDto? ModeloOrigem,
    IReadOnlyList<EtapaFormularioDto> Etapas,
    ColetaDoFormularioDto Coleta,
    IReadOnlyList<TermoExigidoDto> Termos);
