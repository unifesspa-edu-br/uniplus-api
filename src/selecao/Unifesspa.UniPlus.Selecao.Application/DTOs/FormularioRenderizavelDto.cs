namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Um valor selecionável de um fato de seleção do formulário público (issue #1059,
/// UNI-REQ-0072) — o código que o candidato escolhe, a descrição que orienta a escolha e a
/// ordem de apresentação.
/// </summary>
public sealed record ValorSelecionavelDto(string Codigo, string? Descricao, int Ordem);

/// <summary>
/// Um fato coletado pronto para renderização pública (Story #559, issue #1059): mesmos campos
/// de <see cref="FatoColetadoDto"/> mais <see cref="ValoresSelecionaveis"/> — as opções que o
/// candidato pode escolher. Presente com cardinalidade mínima 1 (issue #1077: nunca vazio) quando
/// <see cref="TipoRenderizacao"/> é de seleção, <see langword="null"/> quando não é.
/// <see cref="Formato"/> diz como a resposta do campo de texto é conferida; <see langword="null"/>
/// nos demais campos.
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
    ObrigatoriedadeDto Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Precondicao,
    IReadOnlyList<ValorSelecionavelDto>? ValoresSelecionaveis,
    string? EtapaCodigo,
    string? Formato,
    string? Ajuda,
    bool PedirConfirmacao,
    IReadOnlyList<RestricaoValorDto> Restricoes);

/// <summary>
/// Um grupo repetível pronto para renderização (UNI-REQ-0146): a posição na ordem dos itens, a
/// seção, o rótulo, o mínimo e o máximo de ocorrências, se o próprio candidato é um dos membros, a
/// exibição — nula quando sempre aparece —, a obrigatoriedade e os campos que cada ocorrência
/// responde, na forma dos itens.
/// </summary>
public sealed record GrupoFormularioRenderizavelDto(
    string Codigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    int Minimo,
    int? Maximo,
    bool IncluiCandidato,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Exibicao,
    ObrigatoriedadeDto Obrigatoriedade,
    IReadOnlyList<FatoFormularioRenderizavelDto> Subitens);

/// <summary>
/// Formulário de uma finalidade pronto para renderização (UNI-REQ-0144): título, etapas, termos
/// exigidos (UNI-REQ-0086), os grupos repetíveis (UNI-REQ-0146) e os fatos coletados na ordem de coleta, cada um com rótulo, tipo de renderização, obrigatoriedade,
/// a pré-condição já congelada e os valores selecionáveis (issue #1059). Projetado da
/// <c>VersaoConfiguracao</c> vigente — nunca da raiz viva — pelo <c>FormulariosController</c>,
/// endpoint público.
/// </summary>
/// <param name="ComprovacaoDocumental">
/// As exigências que o bloco de comprovação documental lista: as da fase do formulário, na forma
/// que o certame publica. Nula quando o formulário não tem o bloco.
/// </param>
public sealed record FormularioRenderizavelDto(
    string Finalidade,
    string? Titulo,
    IReadOnlyList<EtapaFormularioDto> Etapas,
    IReadOnlyList<TermoExigidoDto> Termos,
    IReadOnlyList<FatoFormularioRenderizavelDto> FatosColetados,
    IReadOnlyList<ExigenciaDocumentalCertameDto>? ComprovacaoDocumental,
    IReadOnlyList<GrupoFormularioRenderizavelDto> Grupos);
