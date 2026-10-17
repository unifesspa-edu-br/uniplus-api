namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Errors;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Dados de entrada do ato de publicação (Story #759, T4 #785): número do
/// Edital, período de inscrição e a referência ao documento (PDF) confirmado
/// da T3 (#784) — cujo <c>HashSha256</c> vira o <c>ato_criador_hash</c> da
/// <see cref="Entities.VersaoConfiguracao"/> — e os anexos do edital. Não é persistido isoladamente —
/// só existe como entrada de <see cref="Entities.ProcessoSeletivo.Publicar"/>.
/// </summary>
public sealed record DadosEdital
{
    private DadosEdital(
        string? numero,
        DateTimeOffset periodoInscricaoInicio,
        DateTimeOffset periodoInscricaoFim,
        Guid documentoEditalId,
        IReadOnlyList<AnexoDoEdital> anexos)
    {
        Numero = numero;
        PeriodoInscricaoInicio = periodoInscricaoInicio;
        PeriodoInscricaoFim = periodoInscricaoFim;
        DocumentoEditalId = documentoEditalId;
        Anexos = anexos;
    }

    public string? Numero { get; }

    /// <summary>Início do período, sempre em UTC (<c>Offset</c> zero).</summary>
    public DateTimeOffset PeriodoInscricaoInicio { get; }

    /// <summary>Fim do período, sempre em UTC (<c>Offset</c> zero).</summary>
    public DateTimeOffset PeriodoInscricaoFim { get; }

    public Guid DocumentoEditalId { get; }

    /// <summary>
    /// Os anexos do edital, na ordem em que o edital os apresenta. Congelam com a configuração, como
    /// os modelos de documento, e o hash dela os cobre.
    /// </summary>
    public IReadOnlyList<AnexoDoEdital> Anexos { get; }

    /// <summary>
    /// O dia civil que responde <i>quais obrigatoriedades legais estavam em vigor</i> para este
    /// certame (ADR-0114) — o início do período de inscrição, no fuso institucional.
    /// </summary>
    /// <remarks>
    /// Derivar o dia sobre UTC faz um processo publicado às 22h de Belém — 01h UTC do dia
    /// seguinte — resolver a vigência do dia errado, e com ela outro conjunto de regras. Não
    /// quebra teste nenhum: muda qual norma se aplica.
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
        Guid documentoEditalId,
        IReadOnlyList<AnexoDoEdital>? anexos = null)
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

        IReadOnlyList<AnexoDoEdital> anexosDoEdital = anexos ?? [];
        if (RecusaDosAnexos(anexosDoEdital, documentoEditalId) is { } recusa)
        {
            return Result<DadosEdital>.Failure(recusa);
        }

        return Result<DadosEdital>.Success(new DadosEdital(
            numero.EmBrancoViraNulo(),
            periodoInscricaoInicio.ToUniversalTime(),
            periodoInscricaoFim.ToUniversalTime(),
            documentoEditalId,
            anexosDoEdital));
    }

    /// <summary>
    /// A regra dos anexos mora aqui, e não só no handler, porque este value object é o ponto por
    /// onde passam a publicação e a decodificação do envelope: um envelope com anexo repetido não
    /// sai do encoder, então achá-lo é sinal de bytes que não vieram desse caminho.
    /// </summary>
    private static DomainError? RecusaDosAnexos(IReadOnlyList<AnexoDoEdital> anexos, Guid documentoEditalId)
    {
        if (anexos.Count > AnexoDoEdital.QuantidadeMaxima)
        {
            return new DomainError(
                AnexoDoEditalErrorCodes.QuantidadeExcedida,
                $"O edital aceita no máximo {AnexoDoEdital.QuantidadeMaxima} anexos.");
        }

        bool repetido = anexos.Select(static a => a.Id).Distinct().Count() != anexos.Count
            || anexos.Select(static a => a.DocumentoId).Distinct().Count() != anexos.Count
            || anexos.Any(a => a.DocumentoId == documentoEditalId);
        return repetido
            ? new DomainError(
                AnexoDoEditalErrorCodes.Repetido,
                "O mesmo documento não pode constar duas vezes no edital, nem como anexo e como documento principal.")
            : null;
    }
}
