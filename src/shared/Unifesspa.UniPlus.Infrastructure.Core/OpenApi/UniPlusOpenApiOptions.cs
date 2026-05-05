namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Configuração compartilhada do <c>UniPlusInfoTransformer</c>: dados
/// institucionais que aparecem em todos os documentos OpenAPI gerados pela
/// API Uni+ (contact, license, servers). Bind a partir de
/// <c>UniPlus:OpenApi</c>; defaults sensatos para dev/CI.
/// </summary>
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings",
    Justification = "Properties bound from JSON via IOptions; System.Text.Json não desserializa Uri nativamente. Conversão para Uri acontece no transformer.")]
public sealed record UniPlusOpenApiOptions
{
    public const string SectionName = "UniPlus:OpenApi";

    /// <summary>Email institucional para contato técnico.</summary>
    public string ContactEmail { get; init; } = "ctic@unifesspa.edu.br";

    /// <summary>Nome do contato exibido no spec.</summary>
    public string ContactName { get; init; } = "CTIC Unifesspa";

    /// <summary>URL do portal de developers (Milestone C).</summary>
    public string ContactUrl { get; init; } = "https://developers.uniplus.unifesspa.edu.br";

    /// <summary>Versão do contrato V1 — alinhada com ADR-0022.</summary>
    public string ContractVersion { get; init; } = "1.0.0";

    /// <summary>URL base de produção; sobrescrever via configuração em deploy.</summary>
    public string ProductionServerUrl { get; init; } = "https://api.uniplus.unifesspa.edu.br";

    /// <summary>URL base de homologação.</summary>
    public string StagingServerUrl { get; init; } = "https://api.hml.uniplus.unifesspa.edu.br";
}
