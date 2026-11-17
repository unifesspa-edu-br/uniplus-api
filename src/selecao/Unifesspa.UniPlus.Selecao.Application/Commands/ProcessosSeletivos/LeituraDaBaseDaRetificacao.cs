namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ProcessosSeletivos.Retificacao;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// O que a retificação de ato único confere sobre a cadeia de versões antes de canonicalizar: a versão corrente, o ato
/// que seria emendado e o ciclo de recurso em aberto. A versão de configuração é agregado próprio (ADR-0104), e a
/// leitura a entrega à retificação com o motivo e o que o codec leu dela.
/// </summary>
public sealed class LeituraDaBaseDaRetificacao(
    IProcessoSeletivoRepository processos,
    IAtoDoProcessoRepository atos,
    IRegistroCodecsEnvelope registroCodecs)
{
    // A ausência de versão corrente é uma transição inválida, e o motivo é o do pedido, normalizado
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
