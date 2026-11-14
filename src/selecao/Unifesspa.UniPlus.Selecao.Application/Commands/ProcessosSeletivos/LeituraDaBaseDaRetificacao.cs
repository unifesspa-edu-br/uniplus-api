namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// O que a retificação de ato único e o fechamento da sessão conferem sobre a cadeia de versões antes de
/// canonicalizar: a versão corrente, o ato que seria emendado e o ciclo de recurso em aberto. A versão de configuração
/// é agregado próprio (ADR-0104), e a leitura a entrega à retificação com o motivo e o que o codec leu dela. A fase
/// que já começou também não se desfaz: a unidade trocada e a etapa removida são conferidas contra o que a versão
/// publicada tem no instante do fechamento, no ponto em que o handler transita o agregado.
/// </summary>
public sealed class LeituraDaBaseDaRetificacao(
    IProcessoSeletivoRepository processos,
    IAtoDoProcessoRepository atos,
    IRegistroCodecsEnvelope registroCodecs,
    IFasesIniciadasDoProcesso fasesIniciadas)
{
    // Sem sessão, a ausência de versão corrente é uma transição inválida, e o motivo é o do pedido, normalizado
    // uma vez só (Trim e NFC): o bloco 'retificacao' do envelope e o motivo do ato têm de guardar o mesmo valor, e o
    // Postgres não normaliza texto.
    internal async Task<Result<BaseDaRetificacao>> DaVersaoPublicadaAsync(
        ProcessoSeletivo processo, string motivoDoPedido, CancellationToken cancellationToken)
    {
        VersaoConfiguracao? versaoAtual = await processos.ObterVersaoAtualAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        if (versaoAtual is null)
        {
            return Result<BaseDaRetificacao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {processo.Status}."));
        }

        string motivo = HashCanonicalComputer.NormalizeNfc(motivoDoPedido.Trim());
        return await ConferirAsync(processo, versaoAtual, motivo, cancellationToken).ConfigureAwait(false);
    }

    // O motivo é o do rascunho, declarado na abertura e normalizado então: é o que o envelope congela e o que viaja
    // para o ato, e lê-lo de outra fonte faria os dois divergirem. A corrente tem de ser a versão sobre a qual a
    // sessão foi aberta, senão o fechamento emendaria um ato já emendado.
    internal async Task<Result<BaseDaRetificacao>> DaSessaoAbertaAsync(ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        RascunhoRetificacao sessao = processo.Rascunho!;
        VersaoConfiguracao? versaoAtual = await processos.ObterVersaoAtualAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        if (versaoAtual is null || versaoAtual.Id != sessao.VersaoBaseId)
        {
            return Result<BaseDaRetificacao>.Failure(new DomainError(
                "RascunhoRetificacao.BaseDesatualizada",
                "A versão sobre a qual esta retificação foi aberta não é mais o topo da cadeia de configuração — "
                + "o fechamento emendaria um ato que já foi emendado."));
        }

        return await ConferirAsync(processo, versaoAtual, sessao.Motivo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A recusa da fase que começou depois da edição do cronograma: a unidade trocada ou a etapa removida de uma fase
    /// iniciada, no <paramref name="instante"/> em que o fechamento publica.
    /// </summary>
    internal async Task<DomainError?> RecusaPorFaseIniciadaAsync(
        ProcessoSeletivo processo, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        FasesIniciadas iniciadas = await fasesIniciadas
            .ObterAsync(processo.Id, instante, cancellationToken).ConfigureAwait(false);

        return iniciadas.RecusaDaAlteracao(processo.CronogramaFases) ?? iniciadas.RecusaDaRemocaoDaEtapa(processo.Etapas);
    }

    // A versão nova é coerente sozinha, mas não pode desfazer o ciclo de recurso que um preliminar já publicado abriu e
    // que o definitivo ainda não fechou. A linearidade da cadeia de atos é conferida pelo registro do ato, que enxerga o
    // que o endpoint administrativo de Publicações já emendou.
    private async Task<Result<BaseDaRetificacao>> ConferirAsync(
        ProcessoSeletivo processo, VersaoConfiguracao versaoAtual, string motivo, CancellationToken cancellationToken)
    {
        Result cicloRecursal = await ConferenciaDoCicloRecursalAberto
            .ConferirAsync(atos, registroCodecs, processo, versaoAtual, cancellationToken)
            .ConfigureAwait(false);
        return cicloRecursal.IsFailure
            ? Result<BaseDaRetificacao>.ValidationFailure(cicloRecursal.Errors)
            : Result<BaseDaRetificacao>.Success(new BaseDaRetificacao(versaoAtual, motivo, registroCodecs.Reidratar(versaoAtual)));
    }
}
