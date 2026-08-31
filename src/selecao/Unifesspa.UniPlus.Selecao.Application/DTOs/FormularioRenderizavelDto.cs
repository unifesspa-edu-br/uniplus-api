namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Um valor selecionável de um fato de seleção do formulário público (issue #1059,
/// UNI-REQ-0072) — o código que o candidato escolhe, a descrição que orienta a escolha e a
/// ordem de apresentação.
/// </summary>
public sealed record ValorSelecionavelDto(string Codigo, string? Rotulo, string? Descricao, int Ordem);

/// <summary>
/// Um fato coletado pronto para renderização pública (Story #559, issue #1059): mesmos campos
/// de <see cref="FatoColetadoDto"/> mais <see cref="ValoresSelecionaveis"/> — as opções que o
/// candidato pode escolher. Presente com cardinalidade mínima 1 (issue #1077: nunca vazio) quando
/// <see cref="TipoRenderizacao"/> é de seleção, <see langword="null"/> quando não é.
/// </summary>
/// <remarks>
/// DTO PRÓPRIO, e não reaproveitamento de <see cref="FatoColetadoDto"/>: aquele é o read-back
/// administrativo da configuração EDITÁVEL (<c>GET</c>/<c>PUT /fatos-coletados</c>,
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
    bool Obrigatorio,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Precondicao,
    IReadOnlyList<ValorSelecionavelDto>? ValoresSelecionaveis);

/// <summary>
/// Formulário de inscrição pronto para renderização (Story #559): título, termo de aceite e os
/// fatos coletados na ordem de coleta, cada um com rótulo, tipo de renderização, obrigatoriedade,
/// a pré-condição já congelada e os valores selecionáveis (issue #1059). Projetado da
/// <c>VersaoConfiguracao</c> vigente — nunca da raiz viva — pelo <c>FormularioInscricaoController</c>,
/// endpoint público.
/// </summary>
public sealed record FormularioRenderizavelDto(
    string? Titulo,
    string? TermoAceiteTexto,
    IReadOnlyList<FatoFormularioRenderizavelDto> FatosColetados);
