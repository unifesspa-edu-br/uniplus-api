namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quando o processo inclui e retira motivos do cadastro em uma fase ou etapa (UNI-REQ-0285). No rascunho a escolha é
/// livre. Depois da publicação, só a retificação inclui — com justificativa e antes de começar a avaliação naquele
/// lugar (UNI-REQ-0124) — e nada se retira do que já foi publicado.
/// </summary>
/// <remarks>
/// A escolha vive fora da raiz do processo, mas obedece à mesma guarda de mutação dela: a sessão de retificação
/// aberta, a precondição <c>If-Match</c> e o avanço da revisão da sessão, para que incluir motivo invalide o ETag como
/// qualquer outra edição da configuração. A escolha entra na versão do edital em preparo: a primeira, no rascunho, ou a
/// que sucede a base da sessão.
/// </remarks>
public static class EscolhaDosMotivos
{
    public const int JustificativaMaxLength = 1000;

    /// <summary>Inclui no lugar a cópia do motivo do cadastro, ou devolve a recusa.</summary>
    public static Result<MotivoDoProcesso> Incluir(InclusaoDeMotivo inclusao)
    {
        ArgumentNullException.ThrowIfNull(inclusao);

        ProcessoSeletivo processo = inclusao.Processo;
        if ((RecusaDoLugar(processo, inclusao.Lugar) ?? processo.MutacaoBloqueada(inclusao.Precondicao) ?? RecusaDaOrigem(inclusao) ?? RecusaAposPublicacao(inclusao)) is { } recusa)
        {
            return Result<MotivoDoProcesso>.Failure(recusa);
        }

        string? justificativa = processo.Status == StatusProcesso.Publicado ? inclusao.Justificativa!.Trim() : null;
        processo.Rascunho?.IncrementarRevisao();
        return Result<MotivoDoProcesso>.Success(
            MotivoDoProcesso.Copiar(processo.Id, inclusao.Lugar, inclusao.Origem, VersaoEmPreparo(processo), justificativa));
    }

    /// <summary>
    /// Devolve a escolha a retirar do lugar. O que já foi publicado não sai (UNI-REQ-0124): a decisão que o citou
    /// continua citando um motivo que o edital previu. A inclusão ainda pendente na sessão de retificação sai, porque
    /// ainda não é do edital.
    /// </summary>
    public static Result<MotivoDoProcesso> Retirar(
        ProcessoSeletivo processo,
        IReadOnlyCollection<MotivoDoProcesso> escolhaAtual,
        LugarDaDecisao lugar,
        Guid motivoDeDecisaoId,
        PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(escolhaAtual);
        ArgumentNullException.ThrowIfNull(lugar);

        MotivoDoProcesso? escolhido = escolhaAtual.FirstOrDefault(e => e.Lugar == lugar && e.MotivoDeDecisaoOrigemId == motivoDeDecisaoId);
        if ((escolhido is null ? RecusaDeEscolhaAusente(processo, lugar, motivoDeDecisaoId) : processo.MutacaoBloqueada(precondicao) ?? RecusaDaRetirada(processo, escolhido)) is { } recusa)
        {
            return Result<MotivoDoProcesso>.Failure(recusa);
        }

        processo.Rascunho?.IncrementarRevisao();
        return Result<MotivoDoProcesso>.Success(escolhido!);
    }

    /// <summary>
    /// A escolha que sai do processo junto com as fases e etapas que ele deixou de ter desde <paramref name="antes"/>. A
    /// que ainda é só da sessão sai com o lugar; a que já consta do edital publicado não sai (UNI-REQ-0124), e então o
    /// lugar não pode sair: a recusa traz um campo por lugar. Só lê; quem chama remove o devolvido.
    /// </summary>
    public static Result<IReadOnlyList<MotivoDoProcesso>> QueSaemComOsLugares(
        LugaresDaDecisao antes, ProcessoSeletivo processo, IReadOnlyCollection<MotivoDoProcesso> escolhaAtual)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(escolhaAtual);

        int emPreparo = VersaoEmPreparo(processo);
        (LugarNoProcesso Lugar, MotivoDoProcesso[] Escolha)[] ausentes =
            [.. antes.AusentesEm(LugaresDaDecisao.Do(processo)).Select(l => (l, Escolha: escolhaAtual.Where(e => e.Lugar == l.Lugar).ToArray()))];
        FieldError[] recusas = [.. ausentes.Select(a => RecusaDoLugarComMotivoPublicado(a.Lugar, a.Escolha, emPreparo)).OfType<FieldError>()];

        return recusas.Length > 0
            ? Result<IReadOnlyList<MotivoDoProcesso>>.ValidationFailure(recusas)
            : Result<IReadOnlyList<MotivoDoProcesso>>.Success([.. ausentes.SelectMany(static a => a.Escolha)]);
    }

    private static FieldError? RecusaDoLugarComMotivoPublicado(LugarNoProcesso lugar, MotivoDoProcesso[] escolha, int emPreparo)
    {
        string[] publicados = [.. escolha.Where(e => !e.PendenteNa(emPreparo)).Select(static e => e.Codigo.Valor).Order(StringComparer.Ordinal)];

        return publicados.Length == 0
            ? null
            : new FieldError(lugar.Campo, new DomainError(
                MotivoDeDecisaoErrorCodes.FaseOuEtapaComMotivoPublicado,
                $"{lugar.Descricao} não pode ser removida: os motivos {string.Join(", ", publicados)} já constam do edital publicado nela."));
    }

    private static int VersaoEmPreparo(ProcessoSeletivo processo) =>
        processo.Rascunho?.NumeroDaVersaoEmPreparo ?? VersaoConfiguracao.NumeroDaAbertura;

    /// <summary>A recusa de uma fase ou etapa que o processo não tem, inclusive a identidade vazia, que não designa nenhuma.</summary>
    public static DomainError LugarNaoEncontrado() =>
        new(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada, "A fase ou a etapa informada não existe neste processo.");

    private static DomainError? RecusaDoLugar(ProcessoSeletivo processo, LugarDaDecisao lugar) =>
        LugaresDaDecisao.Do(processo).Contem(lugar) ? null : LugarNaoEncontrado();

    private static DomainError RecusaDeEscolhaAusente(ProcessoSeletivo processo, LugarDaDecisao lugar, Guid motivoDeDecisaoId) =>
        RecusaDoLugar(processo, lugar)
            ?? new DomainError(MotivoDeDecisaoErrorCodes.NaoEscolhido, $"O processo não escolheu o motivo de decisão {motivoDeDecisaoId} neste lugar.");

    private static DomainError? RecusaDaRetirada(ProcessoSeletivo processo, MotivoDoProcesso escolhido) =>
        !escolhido.PendenteNa(VersaoEmPreparo(processo))
            ? new DomainError(
                MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao,
                $"O motivo {escolhido.Codigo.Valor} já consta do edital publicado e não pode ser retirado; a retificação só inclui motivos.")
            : null;

    private static DomainError? RecusaDaOrigem(InclusaoDeMotivo inclusao)
    {
        MotivoDeDecisao origem = inclusao.Origem;
        if (!origem.Ativo)
        {
            return new DomainError(
                MotivoDeDecisaoErrorCodes.MotivoInativo,
                $"O motivo {origem.Codigo.Valor} está desativado e não entra em novas escolhas do processo.");
        }

        return inclusao.EscolhaAtual.Any(e => e.MotivoDeDecisaoOrigemId == origem.Id && e.Lugar == inclusao.Lugar)
            ? new DomainError(MotivoDeDecisaoErrorCodes.JaEscolhido, $"O processo já escolheu o motivo {origem.Codigo.Valor} neste lugar.")
            : null;
    }

    private static DomainError? RecusaAposPublicacao(InclusaoDeMotivo inclusao)
    {
        if (inclusao.Processo.Status != StatusProcesso.Publicado)
        {
            return null;
        }

        return RecusaDaJustificativa(inclusao.Justificativa)
            ?? (inclusao.Iniciadas.AvaliacaoComecou(inclusao.Lugar)
                ? new DomainError(
                    MotivoDeDecisaoErrorCodes.AvaliacaoIniciada,
                    $"A avaliação de {inclusao.Origem.Tipo.ToCodigo()} nesta fase ou etapa já começou; motivo novo não entra mais.")
                : null);
    }

    internal static DomainError? RecusaDaJustificativa(string? justificativa)
    {
        if (string.IsNullOrWhiteSpace(justificativa))
        {
            return new DomainError(
                MotivoDeDecisaoErrorCodes.JustificativaObrigatoria,
                "Incluir motivo no processo publicado exige justificativa, de uso interno da administração.");
        }

        if (ValidacaoDoMotivo.TemCaractereNulo(justificativa))
        {
            return new DomainError(MotivoDeDecisaoErrorCodes.JustificativaCaractereInvalido, "A justificativa não pode conter o caractere nulo.");
        }

        return justificativa.Trim().Length > JustificativaMaxLength
            ? new DomainError(MotivoDeDecisaoErrorCodes.JustificativaTamanho, $"A justificativa deve ter até {JustificativaMaxLength} caracteres.")
            : null;
    }
}

/// <summary>O pedido de inclusão de um motivo do cadastro numa fase ou etapa do processo.</summary>
/// <param name="EscolhaAtual">Os motivos que o processo escolheu e ainda valem, em todos os lugares.</param>
/// <param name="Iniciadas">As fases e etapas que já começaram na versão publicada vigente; nenhuma no rascunho.</param>
public sealed record InclusaoDeMotivo(
    ProcessoSeletivo Processo,
    LugarDaDecisao Lugar,
    IReadOnlyCollection<MotivoDoProcesso> EscolhaAtual,
    MotivoDeDecisao Origem,
    string? Justificativa,
    PrecondicaoIfMatch Precondicao,
    FasesIniciadas Iniciadas);
