namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Identificação regulatória e-MEC da Instituição: o código, a classificação, a mantenedora, os
/// atos de credenciamento e os indicadores de qualidade. Os cinco primeiros campos são
/// obrigatórios e os nove últimos, opcionais. A conferência dos campos é acumulada no agregado
/// (ADR-0125); este tipo só carrega os dados e os normaliza.
/// </summary>
public readonly record struct IdentificacaoRegulatoriaDaInstituicao(
    string? CodigoEmec,
    string? Nome,
    string? Sigla,
    string? OrganizacaoAcademica,
    string? CategoriaAdministrativa,
    string? Cnpj = null,
    string? Mantenedora = null,
    string? CodigoMantenedoraEmec = null,
    string? Situacao = null,
    string? AtoCredenciamento = null,
    string? AtoRecredenciamento = null,
    string? ConceitoInstitucional = null,
    string? Igc = null,
    string? Website = null)
{
    /// <summary>Apara os campos e converte em nulo os opcionais em branco.</summary>
    internal IdentificacaoRegulatoriaDaInstituicao Normalizar() => new(
        CodigoEmec?.Trim(),
        Nome?.Trim(),
        Sigla?.Trim(),
        OrganizacaoAcademica?.Trim(),
        CategoriaAdministrativa?.Trim(),
        NormalizarOpcional(Cnpj),
        NormalizarOpcional(Mantenedora),
        NormalizarOpcional(CodigoMantenedoraEmec),
        NormalizarOpcional(Situacao),
        NormalizarOpcional(AtoCredenciamento),
        NormalizarOpcional(AtoRecredenciamento),
        NormalizarOpcional(ConceitoInstitucional),
        NormalizarOpcional(Igc),
        NormalizarOpcional(Website));

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
