namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// Um documento do conjunto padrão do modelo: a fase e o tipo de documento pelo código, as condições de
/// exigência — nulas quando vale para todo candidato —, o indeferimento, os requisitos e, quando há, a exigência por opção.
/// </summary>
public sealed record DocumentoExigidoDoModeloDto(
    string FaseCodigo,
    string TipoDocumentoCodigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Quando,
    IndeferimentoDoDocumentoDoModeloDto Indeferimento,
    RequisitosDoDocumentoDoModeloDto Requisitos,
    ExigenciaPorOpcaoDto? PorOpcao);

/// <summary>O prazo de emissão e a validade que o modelo escolhe para o documento; cada um é nulo quando ainda não foi escolhido.</summary>
public sealed record RequisitosDoDocumentoDoModeloDto(PrazoDeEmissaoDoModeloDto? PrazoDeEmissao, ValidadeDoDocumentoDoModeloDto? Validade);

/// <summary>O indeferimento do documento exigido do modelo: a obrigatoriedade, a consequência e as bases legais.</summary>
public sealed record IndeferimentoDoDocumentoDoModeloDto(bool Obrigatorio, string? ConsequenciaDoDescumprimento, IReadOnlyList<BaseLegalDoModeloDto> BasesLegais, bool AbrePrazoParaReenvio = false);

/// <summary>Uma base legal de uma exigência do modelo, com abrangência e situação em token canônico.</summary>
public sealed record BaseLegalDoModeloDto(string Referencia, string Abrangencia, string Situacao, string? Observacao);

/// <summary>O documento exigido uma vez por opção que o processo oferece ao fato, salvo as listadas em <see cref="Exceto"/>.</summary>
public sealed record ExigenciaPorOpcaoDto(string FatoCodigo, IReadOnlyList<string> Exceto);

/// <summary>
/// O indeferimento de um grupo de documentos do modelo: a consequência de a quantidade mínima não ser
/// atingida e as bases legais.
/// </summary>
public sealed record IndeferimentoDoGrupoDeDocumentosDoModeloDto(string? ConsequenciaDoDescumprimento, IReadOnlyList<BaseLegalDoModeloDto> BasesLegais, bool AbrePrazoParaReenvio = false);

/// <summary>
/// Um grupo de documentos do modelo: o candidato apresenta ao menos a quantidade mínima — nula vale um —, e o
/// indeferimento é nulo quando o grupo não determina resultado.
/// </summary>
public sealed record GrupoDeDocumentosDoModeloDto(
    int? QuantidadeMinima,
    IndeferimentoDoGrupoDeDocumentosDoModeloDto? Indeferimento,
    IReadOnlyList<DocumentoExigidoDoModeloDto> Documentos);

/// <summary>O conjunto padrão de documentos do modelo: os documentos avulsos e os grupos de documentos.</summary>
public sealed record ConjuntoDeDocumentosDoModeloDto(
    IReadOnlyList<DocumentoExigidoDoModeloDto> DocumentosExigidos,
    IReadOnlyList<GrupoDeDocumentosDoModeloDto> GruposDeDocumentos);
