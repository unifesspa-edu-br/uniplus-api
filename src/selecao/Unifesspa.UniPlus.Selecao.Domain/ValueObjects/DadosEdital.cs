namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Dados de entrada do ato de publicação (Story #759, T4 #785): número do
/// Edital, período de inscrição e a referência ao documento (PDF) confirmado
/// da T3 (#784) — cujo <c>HashSha256</c> vira o <c>ato_criador_hash</c> da
/// <see cref="Entities.VersaoConfiguracao"/>. Não é persistido isoladamente —
/// só existe como entrada de <see cref="Entities.ProcessoSeletivo.Publicar"/>.
/// </summary>
/// <remarks>
/// O período é <see cref="DateTimeOffset"/>, não <c>DateOnly</c>: quando o processo
/// coleta inscrição, ele é projetado da janela da fase do cronograma, que é instante
/// (issue #1350). O dia civil continua sendo o que importa para vigência de norma, mas
/// a derivação passa a ser explícita e no fuso institucional, em vez de implícita na
/// granularidade do tipo.
/// </remarks>
public sealed record DadosEdital
{
    private DadosEdital(
        string? numero,
        DateTimeOffset periodoInscricaoInicio,
        DateTimeOffset periodoInscricaoFim,
        Guid documentoEditalId)
    {
        Numero = numero;
        PeriodoInscricaoInicio = periodoInscricaoInicio;
        PeriodoInscricaoFim = periodoInscricaoFim;
        DocumentoEditalId = documentoEditalId;
    }

    public string? Numero { get; }

    /// <summary>Início do período, sempre em UTC (<c>Offset</c> zero).</summary>
    public DateTimeOffset PeriodoInscricaoInicio { get; }

    /// <summary>Fim do período, sempre em UTC (<c>Offset</c> zero).</summary>
    public DateTimeOffset PeriodoInscricaoFim { get; }

    public Guid DocumentoEditalId { get; }

    /// <summary>
    /// O dia civil que responde <i>quais obrigatoriedades legais estavam em vigor</i> para este
    /// certame (ADR-0114) — o início do período de inscrição, no fuso institucional.
    /// </summary>
    /// <remarks>
    /// Vigência de norma é por dia, mas o período passou a ser instante (issue #1350), então a
    /// conversão precisa ser explícita. Derivar o dia sobre UTC faria um processo publicado às 22h
    /// de Belém — 01h UTC do dia seguinte — resolver a vigência do dia errado, e com ela outro
    /// conjunto de regras. Não quebra teste nenhum: muda qual norma se aplica.
    /// </remarks>
    /// <param name="fusoInstitucional">
    /// Zona em que o instante vira dia civil. Vem de fora pelo mesmo motivo de
    /// <c>ProcessoSeletivo.ResolverDataReferenciaFatos</c>: deixar o método escolher a zona
    /// sozinho permitiria calcular o dia por uma zona enquanto a versão congela outra.
    /// </param>
    public DateOnly DiaDeReferenciaLegal(TimeZoneInfo fusoInstitucional)
    {
        ArgumentNullException.ThrowIfNull(fusoInstitucional);

        return DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(PeriodoInscricaoInicio, fusoInstitucional).DateTime);
    }

    /// <summary>
    /// Cria os dados do Edital validando que o período de inscrição é
    /// coerente e que a referência ao documento não é vazia — a existência e
    /// a confirmação efetiva do documento são responsabilidade do handler
    /// (exige consulta ao repositório, fora do alcance de um value object).
    /// </summary>
    /// <remarks>
    /// A recusa do instante zerado mora aqui, e não no validator de entrada, porque este
    /// value object é o ponto por onde passam os dois caminhos que produzem um período: a
    /// publicação, que projeta da fase do cronograma, e a decodificação do envelope, que
    /// reidrata uma versão publicada. Um período em <c>0001-01-01</c> satisfaz
    /// <c>fim &gt;= início</c> e faria round-trip perfeito, restaurando um certame cujo
    /// período de inscrição é impossível.
    /// </remarks>
    public static Result<DadosEdital> Criar(
        string? numero,
        DateTimeOffset periodoInscricaoInicio,
        DateTimeOffset periodoInscricaoFim,
        Guid documentoEditalId)
    {
        if (documentoEditalId == Guid.Empty)
        {
            return Result<DadosEdital>.Failure(new DomainError(
                "DadosEdital.DocumentoEditalIdObrigatorio",
                "A referência ao documento do Edital é obrigatória."));
        }

        if (periodoInscricaoInicio == default || periodoInscricaoFim == default)
        {
            return Result<DadosEdital>.Failure(new DomainError(
                "DadosEdital.PeriodoInscricaoObrigatorio",
                "O período de inscrição exige início e fim definidos."));
        }

        if (periodoInscricaoFim < periodoInscricaoInicio)
        {
            return Result<DadosEdital>.Failure(new DomainError(
                "DadosEdital.PeriodoInscricaoInvalido",
                "O fim do período de inscrição não pode anteceder o início."));
        }

        return Result<DadosEdital>.Success(new DadosEdital(
            numero?.Trim(),
            periodoInscricaoInicio.ToUniversalTime(),
            periodoInscricaoFim.ToUniversalTime(),
            documentoEditalId));
    }
}
