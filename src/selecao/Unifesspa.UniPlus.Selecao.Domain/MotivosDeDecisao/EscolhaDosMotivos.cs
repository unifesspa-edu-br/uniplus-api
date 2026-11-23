namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Quando o processo inclui e retira motivos do cadastro em uma fase ou etapa (UNI-REQ-0285). A escolha é livre no
/// rascunho; depois da publicação o processo não aceita escolha direta, e a retificação inclui o motivo novo, com
/// justificativa e antes de começar a avaliação naquele lugar (UNI-REQ-0124), pelo documento
/// (<see cref="ProcessosSeletivos.Retificacao.ConferenciaDosMotivosDaRetificacao"/>).
/// </summary>
/// <remarks>
/// A escolha vive fora da raiz do processo, mas obedece à mesma guarda de mutação dela: só o rascunho edita. A escolha
/// entra na primeira versão do edital, a única que o rascunho prepara.
/// </remarks>
public static class EscolhaDosMotivos
{
    /// <summary>Inclui no lugar a cópia do motivo do cadastro, ou devolve a recusa.</summary>
    public static Result<MotivoDeDecisaoDoProcesso> Incluir(InclusaoDeMotivo inclusao)
    {
        ArgumentNullException.ThrowIfNull(inclusao);

        ProcessoSeletivo processo = inclusao.Processo;
        return (RecusaDoLugarOuDoBloqueio(processo, inclusao.Lugar) ?? RecusaDaOrigem(inclusao)) is { } recusa
            ? Result<MotivoDeDecisaoDoProcesso>.Failure(recusa)
            : Result<MotivoDeDecisaoDoProcesso>.Success(
                MotivoDeDecisaoDoProcesso.Copiar(processo.Id, inclusao.Lugar, inclusao.Origem, VersaoConfiguracao.NumeroDaAbertura, justificativa: null));
    }

    /// <summary>Devolve a escolha a retirar do lugar, ou a recusa.</summary>
    public static Result<MotivoDeDecisaoDoProcesso> Retirar(
        ProcessoSeletivo processo, IReadOnlyCollection<MotivoDeDecisaoDoProcesso> escolhaAtual, LugarDaDecisao lugar, Guid motivoDeDecisaoId)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(escolhaAtual);
        ArgumentNullException.ThrowIfNull(lugar);

        MotivoDeDecisaoDoProcesso? escolhido = escolhaAtual.FirstOrDefault(e => e.Lugar == lugar && e.MotivoDeDecisaoOrigemId == motivoDeDecisaoId);
        return (RecusaDoLugarOuDoBloqueio(processo, lugar) ?? RecusaDeEscolhaAusente(escolhido, motivoDeDecisaoId)) is { } recusa
            ? Result<MotivoDeDecisaoDoProcesso>.Failure(recusa)
            : Result<MotivoDeDecisaoDoProcesso>.Success(escolhido!);
    }

    /// <summary>
    /// As recusas que só dependem do próprio processo, na ordem em que antecedem a leitura de qualquer cadastro: o lugar
    /// que ele não tem e depois o bloqueio da publicação.
    /// </summary>
    public static DomainError? RecusaDoLugarOuDoBloqueio(ProcessoSeletivo processo, LugarDaDecisao lugar)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(lugar);

        return RecusaDoLugar(processo, lugar) ?? processo.MutacaoBloqueada();
    }

    /// <summary>
    /// A escolha que sai do processo junto com as fases e etapas que ele deixou de ter desde <paramref name="antes"/>. Só
    /// lê; quem chama remove o devolvido.
    /// </summary>
    public static IReadOnlyList<MotivoDeDecisaoDoProcesso> QueSaemComOsLugares(
        LugaresDaDecisao antes, ProcessoSeletivo processo, IReadOnlyCollection<MotivoDeDecisaoDoProcesso> escolhaAtual)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(escolhaAtual);

        HashSet<LugarDaDecisao> ausentes = [.. antes.AusentesEm(LugaresDaDecisao.Das(processo.CronogramaFases, processo.Etapas))];

        return [.. escolhaAtual.Where(e => ausentes.Contains(e.Lugar))];
    }

    /// <summary>A recusa de uma fase ou etapa que o processo não tem, inclusive a identidade vazia, que não designa nenhuma.</summary>
    public static DomainError LugarNaoEncontrado() =>
        new(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada, "A fase ou a etapa informada não existe neste processo.");

    private static DomainError? RecusaDoLugar(ProcessoSeletivo processo, LugarDaDecisao lugar) =>
        LugaresDaDecisao.Das(processo.CronogramaFases, processo.Etapas).Contem(lugar) ? null : LugarNaoEncontrado();

    private static DomainError? RecusaDeEscolhaAusente(MotivoDeDecisaoDoProcesso? escolhido, Guid motivoDeDecisaoId) =>
        escolhido is null
            ? new DomainError(MotivoDeDecisaoErrorCodes.NaoEscolhido, $"O processo não escolheu o motivo de decisão {motivoDeDecisaoId} neste lugar.")
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
}

/// <summary>O pedido de inclusão de um motivo do cadastro numa fase ou etapa do processo.</summary>
/// <param name="EscolhaAtual">Os motivos que o processo escolheu e ainda valem, em todos os lugares.</param>
public sealed record InclusaoDeMotivo(
    ProcessoSeletivo Processo,
    LugarDaDecisao Lugar,
    IReadOnlyCollection<MotivoDeDecisaoDoProcesso> EscolhaAtual,
    MotivoDeDecisao Origem);
