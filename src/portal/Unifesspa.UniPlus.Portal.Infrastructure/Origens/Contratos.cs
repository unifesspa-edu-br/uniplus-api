namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

using Unifesspa.UniPlus.Portal.Application.Certames;

/// <summary>
/// Os campos do detalhe público do certame (Seleção) que a Portal lê. Campos que a origem envia
/// e não estão aqui são ignorados na leitura.
/// </summary>
internal sealed record CertamePublicadoPayload(
    Guid ProcessoSeletivoId,
    Guid AtoCriadorId,
    string? Nome,
    PeriodoDoCertamePayload? Periodo,
    DocumentoEditalDoCertamePayload? DocumentoEdital,
    SituacaoDaInscricao? Situacao,
    IReadOnlyList<EditalPublicadoDoCertamePayload?>? EditaisPublicados = null);

/// <summary>A parte do período de inscrição que a Portal usa: o número do edital.</summary>
internal sealed record PeriodoDoCertamePayload(string? Numero);

/// <summary>A parte do documento do edital que a Portal usa: o endereço público no acervo.</summary>
internal sealed record DocumentoEditalDoCertamePayload(Uri? UrlDownload);

/// <summary>
/// O edital de uma versão divulgada: o ato que a criou, o endereço público no acervo e os
/// documentos dele. Origem anterior ao campo não o envia, e item nulo ou incompleto é descartado na
/// leitura.
/// </summary>
internal sealed record EditalPublicadoDoCertamePayload(
    Guid AtoId,
    Uri? UrlDownload,
    IReadOnlyList<DocumentoDoAtoPayload?>? Documentos = null);

/// <summary>
/// Um documento do ato no acervo. O papel chega como texto (<c>principal</c> ou <c>anexo</c>): um
/// papel que a Portal não conhece descarta o documento, e não a página.
/// </summary>
internal sealed record DocumentoDoAtoPayload(
    string? Papel,
    string? Nome,
    string? TipoDeMidia,
    long? TamanhoBytes,
    Uri? UrlDownload);

/// <summary>
/// Os campos de um ato da consulta unificada (Publicações) que a Portal lê. O assinante não é
/// declarado de propósito: dado pessoal que a página pública não usa não entra na Portal.
/// </summary>
internal sealed record AtoNormativoPayload(
    Guid Id,
    string? TipoCodigo,
    string? TipoNome,
    int Ano,
    string? Numero,
    DateOnly DataPublicacao,
    bool CongelaConfiguracao,
    string? MotivoRetificacao);
