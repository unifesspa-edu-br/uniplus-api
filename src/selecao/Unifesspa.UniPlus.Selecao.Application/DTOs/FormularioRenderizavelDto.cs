namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Um valor selecionável de um fato de seleção do formulário público (issue #1059,
/// UNI-REQ-0072) — o código que o candidato escolhe, a descrição que orienta a escolha e a
/// ordem de apresentação.
/// </summary>
public sealed record ValorSelecionavelDto(string Codigo, string? Descricao, int Ordem);

/// <summary>
/// Um fato coletado pronto para renderização pública (Story #559, issue #1059): a apresentação do
/// campo e <see cref="ValoresSelecionaveis"/> — as opções que o candidato pode escolher. Presente
/// com cardinalidade mínima 1 (issue #1077: nunca vazio) quando <see cref="TipoRenderizacao"/> é de
/// seleção, <see langword="null"/> quando não é. <see cref="Formato"/> diz como a resposta do campo
/// de texto é conferida; <see langword="null"/> nos demais campos. As regras do campo — exibição,
/// obrigatoriedade, restrições e impedimento — estão em <see cref="FormularioRenderizavelDto.Regras"/>,
/// pelo código do fato, e só lá (ADR-0139).
/// </summary>
/// <remarks>
/// DTO PRÓPRIO, e não reaproveitamento de <see cref="FatoColetadoDto"/>: aquele é o read-back
/// administrativo da configuração EDITÁVEL (<c>GET</c> do processo e <c>PUT</c> dos itens do formulário,
/// <see cref="ProcessoSeletivoDto"/>), projetado direto do agregado vivo, sem I/O ao catálogo.
/// Acrescentar o campo nele quebraria aquela projeção — devolver <see langword="null"/> sempre
/// (seletor mudo no GET administrativo) ou fazer I/O novo ao catálogo, mudança de contrato que
/// esta issue não desenha.
/// </remarks>
public sealed record FatoFormularioRenderizavelDto(
    string FatoCodigo,
    int Ordem,
    string Rotulo,
    string TipoRenderizacao,
    IReadOnlyList<ValorSelecionavelDto>? ValoresSelecionaveis,
    string? EtapaCodigo,
    string? Formato,
    string? Ajuda,
    bool PedirConfirmacao);

/// <summary>
/// Um grupo repetível pronto para renderização (UNI-REQ-0146): a posição na ordem dos itens, a
/// seção, o rótulo, o mínimo e o máximo de ocorrências, se o próprio candidato é um dos membros e os
/// campos que cada ocorrência responde, na forma dos itens. A exibição e a obrigatoriedade do grupo
/// estão nas regras.
/// </summary>
public sealed record GrupoFormularioRenderizavelDto(
    string Codigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    int Minimo,
    int? Maximo,
    bool IncluiCandidato,
    IReadOnlyList<FatoFormularioRenderizavelDto> Subitens);

/// <summary>
/// Uma seção ou um bloco do formulário pronto para renderização. <see cref="CodigoNasRegras"/> é o
/// código com que a seção aparece nas regras — nulo no bloco, que não tem regra própria.
/// </summary>
public sealed record SecaoRenderizavelDto(
    string Codigo,
    string? CodigoNasRegras,
    int Ordem,
    string Tipo,
    string? Bloco,
    string Titulo,
    string? Descricao,
    string? Aviso);

/// <summary>
/// Um termo exigido pronto para renderização (UNI-REQ-0086): a versão escolhida no catálogo, com o
/// conteúdo congelado. <see cref="CodigoNasRegras"/> é o código com que o termo aparece nas regras,
/// que dizem quando ele aparece e quando é obrigatório.
/// </summary>
public sealed record TermoRenderizavelDto(
    string Codigo,
    string CodigoNasRegras,
    int Ordem,
    Guid TermoId,
    Guid VersaoId,
    string Nome,
    string Texto,
    string BaseLegal,
    string FormaAceite,
    string HashVersao);

/// <summary>
/// Formulário de uma finalidade pronto para renderização (UNI-REQ-0144): título, seções, termos
/// exigidos (UNI-REQ-0086), os grupos repetíveis (UNI-REQ-0146) e os fatos coletados na ordem de
/// coleta, cada um com rótulo, tipo de renderização e valores selecionáveis (issue #1059). Projetado
/// da <c>VersaoConfiguracao</c> vigente — nunca da raiz viva — pelo <c>FormulariosController</c>,
/// endpoint público.
/// </summary>
/// <param name="Regras">
/// As regras do formulário, que o front interpreta (ADR-0139): a exibição e a obrigatoriedade de
/// cada seção, campo, grupo e termo, as restrições, os impedimentos e a oferta de valores, com as
/// derivações e os agregados que o formulário cita. A API confere de novo tudo o que recebe.
/// </param>
/// <param name="Pressupostos">
/// Os fatos que as regras citam e o formulário não pergunta — respondidos em formulário anterior ou
/// calculados pelo sistema —, que o interpretador recebe como já conhecidos.
/// </param>
/// <param name="ComprovacaoDocumental">
/// As exigências que o bloco de comprovação documental lista: as da fase do formulário, na forma
/// que o certame publica. Nula quando o formulário não tem o bloco.
/// </param>
public sealed record FormularioRenderizavelDto(
    string Finalidade,
    string? Titulo,
    IReadOnlyList<SecaoRenderizavelDto> Etapas,
    IReadOnlyList<TermoRenderizavelDto> Termos,
    IReadOnlyList<FatoFormularioRenderizavelDto> FatosColetados,
    IReadOnlyList<ExigenciaDocumentalCertameDto>? ComprovacaoDocumental,
    IReadOnlyList<GrupoFormularioRenderizavelDto> Grupos,
    FormularioPortavel Regras,
    IReadOnlyList<string> Pressupostos);
