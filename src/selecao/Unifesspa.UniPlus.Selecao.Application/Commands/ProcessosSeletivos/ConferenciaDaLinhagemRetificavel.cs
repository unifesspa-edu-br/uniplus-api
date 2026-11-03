namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// O que a retificação de uma vez e o fechamento da sessão conferem sobre a cadeia de versões antes
/// de canonicalizar: a versão corrente, o ato que seria emendado e o ciclo de recurso em aberto.
/// A versão de configuração é agregado próprio (ADR-0104), e o handler a carrega para entregá-la ao agregado.
/// </summary>
internal static class ConferenciaDaLinhagemRetificavel
{
    /// <param name="versaoBaseEsperada">
    /// A versão sobre a qual a sessão foi aberta, no fechamento: a corrente tem de ser ela, senão o
    /// fechamento emendaria um ato já emendado. Sem sessão, a ausência de versão corrente é uma
    /// transição inválida.
    /// </param>
    internal static async Task<Result<VersaoConfiguracao>> ConferirAsync(
        IProcessoSeletivoRepository processos,
        IVagaDeLinhagemReader vagasDeLinhagem,
        IAtoDoProcessoRepository atos,
        IRegistroCodecsEnvelope registroCodecs,
        ProcessoSeletivo processo,
        Guid? versaoBaseEsperada,
        CancellationToken cancellationToken)
    {
        VersaoConfiguracao? versaoAtual = await processos
            .ObterVersaoAtualAsync(processo.Id, cancellationToken)
            .ConfigureAwait(false);

        if (versaoBaseEsperada is { } esperada)
        {
            if (versaoAtual is null || versaoAtual.Id != esperada)
            {
                return Result<VersaoConfiguracao>.Failure(new DomainError(
                    "RascunhoRetificacao.BaseDesatualizada",
                    "A versão sobre a qual esta retificação foi aberta não é mais o topo da cadeia de configuração — "
                    + "o fechamento emendaria um ato que já foi emendado."));
            }
        }
        else if (versaoAtual is null)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {processo.Status}."));
        }

        // A cadeia de atos é linear: o ato que esta retificação emendaria pode já ter sido emendado
        // pelo endpoint administrativo de Publicações, e o registro recusaria depois de publicado.
        bool jaRetificado = await vagasDeLinhagem
            .AtoJaFoiRetificadoAsync(versaoAtual.AtoCriadorId, cancellationToken)
            .ConfigureAwait(false);
        if (jaRetificado)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.AtoJaRetificado",
                "O ato que esta retificação emendaria já foi retificado — a cadeia de atos é linear."));
        }

        // A versão nova é coerente sozinha, mas não pode desfazer o ciclo de recurso que um
        // preliminar já publicado abriu e que o definitivo ainda não fechou.
        Result cicloRecursal = await ConferenciaDoCicloRecursalAberto
            .ConferirAsync(atos, registroCodecs, processo, versaoAtual, cancellationToken)
            .ConfigureAwait(false);
        return cicloRecursal.IsFailure
            ? Result<VersaoConfiguracao>.ValidationFailure(cicloRecursal.Errors)
            : Result<VersaoConfiguracao>.Success(versaoAtual);
    }
}
