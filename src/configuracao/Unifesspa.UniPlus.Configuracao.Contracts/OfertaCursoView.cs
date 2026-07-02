namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>OfertaCurso</c> para consumo cross-módulo via
/// <see cref="IOfertaCursoReader"/> (ADR-0056). Expõe a oferta viva que outro
/// bounded context (ex.: o Módulo Seleção, ao montar o quadro de vagas de um
/// edital) lê antes de congelar por valor o que precisar (snapshot-copy,
/// ADR-0061) — este contrato não define, por si só, o que o consumidor
/// congelará. Os enums são expostos como tokens textuais (UPPER_SNAKE); a
/// unidade ofertante já vem achatada (é, ela mesma, um snapshot-copy — ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="CursoId">Curso curricular referenciado (FK intra-schema).</param>
/// <param name="LocalOfertaId">Local de oferta referenciado (FK intra-schema).</param>
/// <param name="UnidadeOfertanteOrigemId">Identificador de origem da unidade ofertante congelada.</param>
/// <param name="UnidadeOfertanteSigla">Sigla da unidade ofertante congelada.</param>
/// <param name="UnidadeOfertanteNome">Nome da unidade ofertante congelada.</param>
/// <param name="UnidadeOfertanteTipo">Tipo da unidade ofertante congelado.</param>
/// <param name="ProgramaDeOferta">Programa de oferta (token; ex.: "REGULAR").</param>
/// <param name="FormatoPedagogico">Formato pedagógico (token; ex.: "PRESENCIAL").</param>
/// <param name="Turno">Turno (token) ou null.</param>
/// <param name="EMecCodigo">Código e-MEC por campus-sede, ou null.</param>
/// <param name="CodigoSga">Código no sistema de gestão acadêmica, ou null.</param>
/// <param name="VagasAnuaisAutorizadas">Teto de vagas autorizadas no e-MEC (não são vagas de certame), ou null.</param>
/// <param name="BaseLegal">Base legal (obrigatória quando o programa não é Regular), ou null.</param>
/// <param name="AtoAutorizacaoMec">Ato de autorização específico da oferta, ou null.</param>
public sealed record OfertaCursoView(
    Guid Id,
    Guid CursoId,
    Guid LocalOfertaId,
    Guid UnidadeOfertanteOrigemId,
    string UnidadeOfertanteSigla,
    string UnidadeOfertanteNome,
    string UnidadeOfertanteTipo,
    string ProgramaDeOferta,
    string FormatoPedagogico,
    string? Turno,
    string? EMecCodigo,
    string? CodigoSga,
    int? VagasAnuaisAutorizadas,
    string? BaseLegal,
    string? AtoAutorizacaoMec);
