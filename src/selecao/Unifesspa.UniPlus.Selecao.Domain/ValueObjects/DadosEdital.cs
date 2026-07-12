namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Dados de entrada do ato de publicação (Story #759, T4 #785): número do
/// Edital, período de inscrição e a referência ao documento (PDF) confirmado
/// da T3 (#784) — cujo <c>HashSha256</c> vira o <c>ato_criador_hash</c> da
/// <see cref="Entities.VersaoConfiguracao"/>. Não é persistido isoladamente —
/// só existe como entrada de <see cref="Entities.ProcessoSeletivo.Publicar"/>.
/// </summary>
public sealed record DadosEdital
{
    private DadosEdital(
        string? numero,
        DateOnly periodoInscricaoInicio,
        DateOnly periodoInscricaoFim,
        Guid documentoEditalId)
    {
        Numero = numero;
        PeriodoInscricaoInicio = periodoInscricaoInicio;
        PeriodoInscricaoFim = periodoInscricaoFim;
        DocumentoEditalId = documentoEditalId;
    }

    public string? Numero { get; }
    public DateOnly PeriodoInscricaoInicio { get; }
    public DateOnly PeriodoInscricaoFim { get; }
    public Guid DocumentoEditalId { get; }

    /// <summary>
    /// Cria os dados do Edital validando que o período de inscrição é
    /// coerente e que a referência ao documento não é vazia — a existência e
    /// a confirmação efetiva do documento são responsabilidade do handler
    /// (exige consulta ao repositório, fora do alcance de um value object).
    /// </summary>
    public static Result<DadosEdital> Criar(
        string? numero,
        DateOnly periodoInscricaoInicio,
        DateOnly periodoInscricaoFim,
        Guid documentoEditalId)
    {
        if (documentoEditalId == Guid.Empty)
        {
            return Result<DadosEdital>.Failure(new DomainError(
                "DadosEdital.DocumentoEditalIdObrigatorio",
                "A referência ao documento do Edital é obrigatória."));
        }

        if (periodoInscricaoFim < periodoInscricaoInicio)
        {
            return Result<DadosEdital>.Failure(new DomainError(
                "DadosEdital.PeriodoInscricaoInvalido",
                "O fim do período de inscrição não pode anteceder o início."));
        }

        return Result<DadosEdital>.Success(new DadosEdital(
            numero?.Trim(),
            periodoInscricaoInicio,
            periodoInscricaoFim,
            documentoEditalId));
    }
}
