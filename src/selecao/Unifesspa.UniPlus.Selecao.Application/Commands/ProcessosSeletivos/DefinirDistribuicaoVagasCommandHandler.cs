namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirDistribuicaoVagasCommand"/> (Story #773):
/// resolve cada oferta e regra de distribuição nos cadastros vivos do módulo
/// Configuração e no catálogo <c>regras_do_catalogo</c> (<see cref="ConfiguracaoDistribuicaoVagasResolver"/>,
/// ADR-0056), congela cada peça por valor (snapshot-copy, ADR-0061) e monta a
/// distribuição — as invariantes (percentual da reserva legal, referência demográfica, modalidades
/// federais, coerência de cada modalidade) são garantidas pelas factories do
/// domínio.
/// </summary>
public static class DefinirDistribuicaoVagasCommandHandler
{
    public static async Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>> Handle(
        DefinirDistribuicaoVagasCommand command, IProcessoSeletivoRepository processoSeletivoRepository, ConfiguracaoDistribuicaoVagasResolver resolvedor,
        IFatoCandidatoReader fatoCandidatoReader, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<ProcessoSeletivo> definido = await DefinirNoProcessoAsync(
            command, processoSeletivoRepository, resolvedor, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
        return definido.IsFailure
            ? Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.ValidationFailure(definido.Errors)
            : await PersistirAsync(definido.Value!, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // Agregado tracked (ObterParaMutacaoAsync): a nova coleção e suas filhas (Guid v7 já preenchido) são persistidas
    // por change detection. NÃO chamar DbSet.Update — marcaria os filhos novos como Modified, emitindo UPDATE de
    // linhas nunca inseridas.
    private static async Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>> PersistirAsync(
        ProcessoSeletivo processo, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.Success(Persistida(processo));
    }

    private static async Task<Result<ProcessoSeletivo>> DefinirNoProcessoAsync(
        DefinirDistribuicaoVagasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ConfiguracaoDistribuicaoVagasResolver resolvedor,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        Result<ProcessoSeletivo> processo = await CarregarParaMutacaoAsync(command, processoSeletivoRepository, cancellationToken).ConfigureAwait(false);
        if (processo.IsFailure)
        {
            return processo;
        }

        ArgumentNullException.ThrowIfNull(resolvedor);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>> resolvidas = await resolvedor
            .ResolverTodasAsync(command.DistribuicaoVagas, cancellationToken).ConfigureAwait(false);
        return resolvidas.IsFailure
            ? Result<ProcessoSeletivo>.ValidationFailure(resolvidas.Errors)
            : await DefinirAsync(command, processo.Value!, resolvidas.Value!, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
    }

    // Fora do rascunho a recusa vem antes de consultar cadastros.
    private static async Task<Result<ProcessoSeletivo>> CarregarParaMutacaoAsync(
        DefinirDistribuicaoVagasCommand command, IProcessoSeletivoRepository processoSeletivoRepository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return processo is null
            ? Result<ProcessoSeletivo>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."))
            : processo.MutacaoBloqueada() is { } bloqueio
                ? Result<ProcessoSeletivo>.Failure(bloqueio)
                : Result<ProcessoSeletivo>.Success(processo);
    }

    // Uma modalidade retirada da oferta não pode deixar sem valor válido a condição viva sobre qualquer fato cujos
    // valores são modalidades — a fonte vem do catálogo, não do código.
    private static async Task<Result<ProcessoSeletivo>> DefinirAsync(
        DefinirDistribuicaoVagasCommand command,
        ProcessoSeletivo processo,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicoes,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);

        FatosDeModalidade fatosDeModalidade = VocabularioDeFatos.ComValoresDeModalidade(
            await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false));
        Result definido = processo.DefinirDistribuicaoVagas(distribuicoes, fatosDeModalidade);
        return definido.IsFailure ? Result<ProcessoSeletivo>.Failure(definido.Error!) : Result<ProcessoSeletivo>.Success(processo);
    }

    // Reprojeta do agregado (não da lista local): DefinirDistribuicaoVagas substitui a coleção inteira, então
    // processo.DistribuicaoVagas já reflete exatamente o que foi persistido — mesma projeção do GET, para o corpo
    // da resposta nunca divergir do que um GET mostraria em seguida.
    private static IReadOnlyList<ConfiguracaoDistribuicaoVagasDto> Persistida(ProcessoSeletivo processo) =>
        ProjecaoDasVagas.ProjetarDistribuicoes(processo);
}
