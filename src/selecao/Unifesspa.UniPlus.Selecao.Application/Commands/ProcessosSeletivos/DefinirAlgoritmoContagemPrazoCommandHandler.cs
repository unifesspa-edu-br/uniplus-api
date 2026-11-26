namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Handler do <see cref="DefinirAlgoritmoContagemPrazoCommand"/> (UNI-REQ-0112): resolve o
/// par declarado contra o rol de regras e delega ao domínio a identidade completa.
/// </summary>
/// <remarks>
/// A referência é montada a partir dos valores <b>resolvidos</b> do catálogo — nunca
/// ecoados do payload —, o mesmo caminho que as demais dimensões usam ao aplicar uma
/// regra. Assim o hash congelado é, por construção, o da definição efetivamente aplicada.
/// </remarks>
public static class DefinirAlgoritmoContagemPrazoCommandHandler
{
    public static Task<Result> Handle(
        DefinirAlgoritmoContagemPrazoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        return new DefinicaoDaContagem(processoSeletivoRepository, regraCatalogoReader, unitOfWork)
            .ExecutarAsync(command, cancellationToken);
    }

    private sealed class DefinicaoDaContagem(
        IProcessoSeletivoRepository repository, IRegraCatalogoReader reader, IUnitOfWork unitOfWork)
    {
        public async Task<Result> ExecutarAsync(DefinirAlgoritmoContagemPrazoCommand command, CancellationToken cancellationToken)
        {
            ProcessoSeletivo? processo = await repository.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
                .ConfigureAwait(false);
            Result conferencia = ConferirProcessoEDeclaracao(processo, command);
            if (conferencia.IsFailure)
            {
                return conferencia;
            }

            Result<ReferenciaRegra> referencia = await ConsultarRegraAsync(command, reader, cancellationToken).ConfigureAwait(false);
            return referencia.IsFailure
                ? Result.Failure(referencia.Error!)
                : await DefinirESalvarAsync(processo!, referencia.Value!, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Result> DefinirESalvarAsync(
            ProcessoSeletivo processo, ReferenciaRegra referencia, CancellationToken cancellationToken)
        {
            Result resultado = processo.DefinirAlgoritmoContagemPrazo(referencia);
            if (resultado.IsFailure)
            {
                return Result.Failure(resultado.Error!);
            }

            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
    }

    private static Result ConferirProcessoEDeclaracao(ProcessoSeletivo? processo, DefinirAlgoritmoContagemPrazoCommand command)
    {
        if (processo is null)
        {
            return Result.Failure(AcessoAoProcesso.NaoEncontrado());
        }

        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        return ConferirDeclaracao(command);
    }

    private static Result ConferirDeclaracao(DefinirAlgoritmoContagemPrazoCommand command) =>
        command.AlgoritmoNaoDeclarado
            ? Result.Failure(new DomainError("ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
                "A convenção de contagem dos prazos é obrigatória — informe código e versão."))
            : Result.Success();

    private static async Task<Result<ReferenciaRegra>> ConsultarRegraAsync(
        DefinirAlgoritmoContagemPrazoCommand command, IRegraCatalogoReader reader, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await reader.ObterAsync(command.Codigo!, command.Versao!, cancellationToken).ConfigureAwait(false);
        return regra is null || regra.Tipo != TipoRegra.ConvencaoContagemPrazo
            ? Result<ReferenciaRegra>.Failure(new DomainError(
                "ProcessoSeletivo.AlgoritmoContagemPrazoNaoEncontrado",
                $"A regra {command.Codigo}/{command.Versao} não é uma entrada de algoritmo de contagem do catálogo de regras."))
            : Result<ReferenciaRegra>.Success(regra.Referencia());
    }

}
