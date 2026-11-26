namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// Um grupo repetível do formulário (UNI-REQ-0146): a posição na ordem dos itens, a seção, o rótulo, as
/// ocorrências admitidas, a exibição — nula quando sempre aparece — e a obrigatoriedade em
/// <see cref="Regras"/> e os campos de cada ocorrência, na ordem dentro do grupo.
/// </summary>
public sealed record GrupoColetadoDto(
    string Codigo,
    int Ordem,
    string? PaginaCodigo,
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
/// O formulário do processo (UNI-REQ-0144): o código que o identifica, a fase, se é obrigatório, o título, o
/// modelo de origem — nulo quando o formulário não partiu de um modelo —, as páginas, o que ele coleta e os
/// termos exigidos.
/// </summary>
public sealed record FormularioDto(
    string Codigo,
    Guid? FaseId,
    bool Obrigatorio,
    string? Titulo,
    ModeloDeOrigemDto? ModeloOrigem,
    IReadOnlyList<PaginaDoFormularioDto> Paginas,
    ColetaDoFormularioDto Coleta,
    IReadOnlyList<TermoExigidoDto> Termos)
{
    /// <summary>O que acontece com quem não envia o formulário; nulo quando a falta de envio não tem consequência própria.</summary>
    public CodigoComRotulo<ConsequenciaDoDescumprimento>? ConsequenciaDaFaltaDeEnvio { get; init; }
}
