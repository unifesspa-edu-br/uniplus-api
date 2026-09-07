namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>CategoriaDocumento</c> para consumo cross-módulo via
/// <see cref="ICategoriaDocumentoReader"/> (ADR-0056). Expõe a categoria viva que o
/// Módulo Seleção lê ao declarar o recorte de competência de uma banca requerida por
/// fase, antes de congelar por valor o par identidade-e-código (snapshot-copy,
/// ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Codigo">Código classificatório, chave natural da categoria (ex.: "RENDA").</param>
/// <param name="Nome">Rótulo legível da categoria.</param>
/// <param name="Descricao">Descrição livre opcional.</param>
/// <param name="Ordem">Posição de exibição no catálogo — critério de apresentação, não de negócio.</param>
public sealed record CategoriaDocumentoView(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    int Ordem);
