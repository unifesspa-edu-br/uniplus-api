namespace Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// DTO read-only da <c>Instituicao</c> singleton para consumo cross-módulo via
/// <see cref="IInstituicaoReader"/> (ADR-0056). Expõe o cabeçalho institucional
/// (identificação regulatória e-MEC) que outros bounded contexts — Seleção,
/// Configuração — exibem em editais e comprovantes, sem dados de auditoria
/// interna nem acesso direto à tabela do banco de Organização (ADR-0054).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="CodigoEmec">Código e-MEC da instituição (chave natural regulatória).</param>
/// <param name="Nome">Nome formal da instituição.</param>
/// <param name="Sigla">Sigla da instituição.</param>
/// <param name="Cnpj">CNPJ da pessoa jurídica, ou <see langword="null"/> se não informado.</param>
/// <param name="OrganizacaoAcademica">Classificação e-MEC de organização acadêmica.</param>
/// <param name="CategoriaAdministrativa">Classificação e-MEC de categoria administrativa.</param>
/// <param name="CidadeCodigoIbge">Código IBGE (7 dígitos) do município da sede, ou <see langword="null"/> se não informado. Referência de cidade do Geo (ADR-0090) — composição no cliente, sem FK cross-banco.</param>
/// <param name="CidadeNome">Nome do município da sede (display cache), ou <see langword="null"/> se não informado.</param>
/// <param name="CidadeUf">UF do município da sede (display cache), ou <see langword="null"/> se não informado.</param>
/// <param name="UnidadeRaizId">Id da Unidade raiz (reitoria), ou <see langword="null"/> se ainda não vinculada.</param>
public sealed record InstituicaoView(
    Guid Id,
    string CodigoEmec,
    string Nome,
    string Sigla,
    string? Cnpj,
    string OrganizacaoAcademica,
    string CategoriaAdministrativa,
    string? CidadeCodigoIbge,
    string? CidadeNome,
    string? CidadeUf,
    Guid? UnidadeRaizId);
