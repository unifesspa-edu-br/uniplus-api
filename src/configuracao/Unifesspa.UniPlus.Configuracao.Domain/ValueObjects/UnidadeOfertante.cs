namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Snapshot-copy da unidade ofertante (instituto/faculdade) congelado na
/// <c>OfertaCurso</c> (story #588, ADR-0061): o <see cref="OrigemId"/> aponta a
/// Unidade viva de Organização (proveniência, sem FK cross-módulo) e
/// <see cref="Sigla"/>/<see cref="Nome"/>/<see cref="Tipo"/> são a cópia
/// congelada no ato da criação. Mudanças posteriores na Unidade viva
/// <b>não</b> retropropagam — a oferta preserva o rótulo histórico.
/// </summary>
/// <remarks>
/// A resolução da Unidade viva (via <c>IUnidadeReader</c>, ADR-0056) é
/// responsabilidade do handler de criação; este value object valida apenas
/// formato/completude do snapshot. Os tamanhos máximos espelham as colunas da
/// entidade <c>Unidade</c> de Organização (sigla 50 / nome 250 / tipo 30).
/// </remarks>
public sealed record UnidadeOfertante
{
    public const int SiglaMaxLength = 50;
    public const int NomeMaxLength = 250;
    public const int TipoMaxLength = 30;

    /// <summary>Id da Unidade viva de origem (proveniência do snapshot, sem FK).</summary>
    public Guid OrigemId { get; }

    /// <summary>Sigla da unidade congelada no ato da criação da oferta.</summary>
    public string Sigla { get; }

    /// <summary>Nome formal da unidade congelado no ato da criação da oferta.</summary>
    public string Nome { get; }

    /// <summary>Classificação organizacional congelada (string estável do contrato).</summary>
    public string Tipo { get; }

    private UnidadeOfertante(Guid origemId, string sigla, string nome, string tipo)
    {
        OrigemId = origemId;
        Sigla = sigla;
        Nome = nome;
        Tipo = tipo;
    }

    /// <summary>
    /// Cria o snapshot da unidade ofertante validando completude e tamanhos.
    /// Retorna falha de domínio (códigos <c>OfertaCurso.UnidadeOfertante*</c>)
    /// no primeiro problema. Os textos são normalizados por <c>Trim</c>.
    /// </summary>
    public static Result<UnidadeOfertante> Criar(
        Guid origemId,
        string? sigla,
        string? nome,
        string? tipo)
    {
        if (origemId == Guid.Empty)
        {
            return Falha(
                OfertaCursoErrorCodes.UnidadeOfertanteOrigemObrigatoria,
                "Id de origem da unidade ofertante é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(sigla))
        {
            return Falha(
                OfertaCursoErrorCodes.UnidadeOfertanteSiglaObrigatoria,
                "Sigla da unidade ofertante é obrigatória.");
        }

        if (sigla.Trim().Length > SiglaMaxLength)
        {
            return Falha(
                OfertaCursoErrorCodes.UnidadeOfertanteSiglaTamanho,
                $"Sigla da unidade ofertante deve ter no máximo {SiglaMaxLength} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Falha(
                OfertaCursoErrorCodes.UnidadeOfertanteNomeObrigatorio,
                "Nome da unidade ofertante é obrigatório.");
        }

        if (nome.Trim().Length > NomeMaxLength)
        {
            return Falha(
                OfertaCursoErrorCodes.UnidadeOfertanteNomeTamanho,
                $"Nome da unidade ofertante deve ter no máximo {NomeMaxLength} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(tipo))
        {
            return Falha(
                OfertaCursoErrorCodes.UnidadeOfertanteTipoObrigatorio,
                "Tipo da unidade ofertante é obrigatório.");
        }

        if (tipo.Trim().Length > TipoMaxLength)
        {
            return Falha(
                OfertaCursoErrorCodes.UnidadeOfertanteTipoTamanho,
                $"Tipo da unidade ofertante deve ter no máximo {TipoMaxLength} caracteres.");
        }

        return Result<UnidadeOfertante>.Success(
            new UnidadeOfertante(origemId, sigla.Trim(), nome.Trim(), tipo.Trim()));
    }

    private static Result<UnidadeOfertante> Falha(string code, string mensagem) =>
        Result<UnidadeOfertante>.Failure(new DomainError(code, mensagem));
}
