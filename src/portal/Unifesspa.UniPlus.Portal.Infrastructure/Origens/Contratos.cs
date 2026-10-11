namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

/// <summary>
/// Os campos do detalhe público do certame (Seleção) que a Portal lê. Campos que a origem envia
/// e não estão aqui são ignorados na leitura.
/// </summary>
internal sealed record CertamePublicadoPayload(
    Guid ProcessoSeletivoId,
    Guid AtoCriadorId,
    string? Nome,
    PeriodoDoCertamePayload? Periodo,
    DocumentoEditalDoCertamePayload? DocumentoEdital);

/// <summary>A parte do período de inscrição que a Portal usa: o número do edital.</summary>
internal sealed record PeriodoDoCertamePayload(string? Numero);

/// <summary>A parte do documento do edital que a Portal usa: o endereço público no acervo.</summary>
internal sealed record DocumentoEditalDoCertamePayload(Uri? UrlDownload);

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
