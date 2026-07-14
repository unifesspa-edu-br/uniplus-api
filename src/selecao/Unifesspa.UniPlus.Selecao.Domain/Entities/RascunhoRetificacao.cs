namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A <b>sessão editorial</b> aberta sobre a configuração de um certame publicado — o
/// <b>portador</b> da retificação (ADR-0110 D3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ela é o estado, e é por isso que não há status novo.</b> O <c>Status</c> do
/// processo continua <c>Publicado</c> durante toda a edição: um certame com retificação
/// aberta <b>está publicado</b> — juridicamente, e para o candidato, que continua vendo a
/// versão congelada vigente. O <c>status</c> marca o estado do <b>ato</b>, não a
/// atividade em curso; a atividade vive aqui, na <b>existência</b> desta entidade.
/// </para>
/// <para>
/// <b>1:1 com a raiz, e sem histórico.</b> É apagada no fechamento (que congela a versão
/// nova) e no descarte (que repõe a configuração congelada). Não é evidência forense:
/// a auditoria com peso jurídico vive na <see cref="VersaoConfiguracao"/>, que é
/// append-only e não é tocada por nenhum dos dois caminhos. Por isso <see cref="EntityBase"/>
/// puro — sem soft-delete (ADR-0063).
/// </para>
/// <para>
/// <b><see cref="Revisao"/> protege a sessão inteira, não só o motivo</b> (D5). Os seis
/// <c>Definir*</c> são as rotas que de fato alteram a configuração — uma revisão que
/// governasse apenas o <c>PUT</c> do motivo seria decorativa, e dois administradores
/// editando dimensões diferentes se sobrescreveriam em silêncio.
/// </para>
/// </remarks>
public sealed class RascunhoRetificacao : EntityBase
{
    /// <summary>
    /// Teto do motivo, aferido sobre o valor <b>normalizado</b>. É o menor dos dois
    /// limites que ele atravessa: a coluna de Seleção comportaria mais, mas no fechamento
    /// este mesmo motivo viaja para o ato em Publicações (ADR-0108), onde o limite é 1000.
    /// Aceitar aqui um motivo que o fechamento recusaria abriria uma sessão condenada —
    /// o administrador só descobriria ao tentar fechá-la.
    /// </summary>
    public const int MotivoMaxLength = 1000;

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Justificativa do ato de retificação (ADR-0101) — já normalizada (Trim + NFC).</summary>
    public string Motivo { get; private set; } = string.Empty;

    /// <summary>A <see cref="VersaoConfiguracao"/> corrente quando a sessão foi aberta.</summary>
    public Guid VersaoBaseId { get; private set; }

    public int NumeroVersaoBase { get; private set; }

    public DateTimeOffset AbertoEm { get; private set; }

    /// <summary>Sub do usuário autenticado que abriu a sessão (via <c>IUserContext</c>).</summary>
    public string AbertoPorSub { get; private set; } = string.Empty;

    /// <summary>
    /// Nasce em <b>1</b> e é incrementada por <b>toda mutação aceita</b> — os seis
    /// <c>Definir*</c> e o <c>PUT</c> do motivo.
    /// </summary>
    public int Revisao { get; private set; }

    private RascunhoRetificacao() { }

    internal static Result<RascunhoRetificacao> Criar(
        Guid processoSeletivoId,
        string motivo,
        VersaoConfiguracao versaoBase,
        string abertoPorSub,
        DateTimeOffset abertoEm)
    {
        ArgumentNullException.ThrowIfNull(versaoBase);
        ArgumentException.ThrowIfNullOrWhiteSpace(abertoPorSub);

        Result<string> motivoNormalizado = NormalizarMotivo(motivo);
        if (motivoNormalizado.IsFailure)
        {
            return Result<RascunhoRetificacao>.Failure(motivoNormalizado.Error!);
        }

        return Result<RascunhoRetificacao>.Success(new RascunhoRetificacao
        {
            ProcessoSeletivoId = processoSeletivoId,
            Motivo = motivoNormalizado.Value!,
            VersaoBaseId = versaoBase.Id,
            NumeroVersaoBase = versaoBase.NumeroVersao,
            AbertoPorSub = abertoPorSub,
            AbertoEm = abertoEm,
            Revisao = 1,
        });
    }

    /// <summary>
    /// A precondição de concorrência da sessão — <b>forte</b>, e com a identidade da
    /// sessão dentro dela (D5).
    /// </summary>
    /// <remarks>
    /// A <see cref="Revisao"/> sozinha sofre <b>ABA</b>: descartar e reabrir reinicia a
    /// contagem, e um tag emitido pela sessão anterior validaria a nova — que é
    /// exatamente a edição cega que a precondição existe para impedir. Carregar o
    /// <see cref="EntityBase.Id"/> junto faz o tag morrer com a sessão que o emitiu.
    /// </remarks>
    public string ETag => $"\"{Id}:{Revisao}\"";

    /// <summary>
    /// Confere o <c>If-Match</c> contra o <see cref="ETag"/> corrente.
    /// <see langword="null"/> quando a mutação pode prosseguir.
    /// </summary>
    internal DomainError? ConferirPrecondicao(PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        if (!precondicao.Presente)
        {
            return new DomainError(
                "Precondicao.Requerida",
                "Há uma retificação em curso neste processo — informe o If-Match com o ETag da sessão editorial.");
        }

        if (!precondicao.Casa(ETag))
        {
            return new DomainError(
                "Precondicao.Falhou",
                "O If-Match informado não corresponde ao estado atual da sessão editorial — releia o rascunho e refaça a operação.");
        }

        return null;
    }

    internal Result AlterarMotivo(string motivo)
    {
        Result<string> normalizado = NormalizarMotivo(motivo);
        if (normalizado.IsFailure)
        {
            return Result.Failure(normalizado.Error!);
        }

        Motivo = normalizado.Value!;
        return Result.Success();
    }

    internal void IncrementarRevisao() => Revisao++;

    /// <summary>
    /// <b>Trim + NFC, na abertura</b> — e o teto aferido sobre o valor já normalizado.
    /// </summary>
    /// <remarks>
    /// A normalização acontece <b>uma vez</b>, aqui, e o valor guardado é o que o
    /// canonicalizador congelaria: o <c>SnapshotPublicacaoCanonicalizer</c> aplica NFC ao
    /// congelar, e um motivo persistido em forma decomposta (NFD) divergiria do bloco
    /// congelado — Postgres não normaliza texto, então a paridade é responsabilidade da
    /// aplicação. O teto vem depois da normalização porque ela pode <b>expandir</b> code
    /// points (U+0958 → U+0915 U+093C): medir o valor cru deixaria passar um motivo que
    /// estoura a coluna só na hora do <c>SaveChanges</c> — um 500 no meio da abertura.
    /// </remarks>
    private static Result<string> NormalizarMotivo(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Result<string>.Failure(new DomainError(
                "RascunhoRetificacao.MotivoObrigatorio",
                "O motivo da retificação é obrigatório."));
        }

        string normalizado = HashCanonicalComputer.NormalizeNfc(motivo.Trim());

        if (normalizado.Length > MotivoMaxLength)
        {
            return Result<string>.Failure(new DomainError(
                "RascunhoRetificacao.MotivoMuitoLongo",
                $"O motivo da retificação deve ter no máximo {MotivoMaxLength} caracteres."));
        }

        return Result<string>.Success(normalizado);
    }
}
