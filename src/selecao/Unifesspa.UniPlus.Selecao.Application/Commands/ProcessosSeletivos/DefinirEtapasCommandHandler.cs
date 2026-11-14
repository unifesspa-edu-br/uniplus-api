namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;

public static class DefinirEtapasCommandHandler
{
    /// <summary>
    /// Substitui as etapas do processo, reconciliando por <c>Id</c> (ver <see cref="DefinicaoDasEtapas"/>).
    /// </summary>
    /// <remarks>
    /// SEM <c>[NonTransactional]</c>, de propósito: este handler depende da transação ambiente do Wolverine
    /// para <see cref="IProcessoSeletivoRepository.ObterParaMutacaoAsync"/>. O <c>SELECT ... FOR UPDATE</c>
    /// só serializa handlers concorrentes porque roda enrolado nela, e <c>[NonTransactional]</c> o
    /// desabilitaria.
    /// </remarks>
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ITipoEtapaReader tipoEtapaReader,
        ITipoBancaReader tipoBancaReader,
        IRegraCatalogoReader regraCatalogoReader,
        ITipoAtoPublicadoReader tipoAtoPublicadoReader,
        IRegistroCodecsEnvelope registroCodecs,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        // A precondição precede as regras de negócio que o handler avalia, na ordem da ADR-0110: quem tem
        // If-Match defasado precisa saber disso antes de caçar um cadastro que não errou. Só a validação
        // de schema do payload a antecede, no middleware; o mesmo guard continua dentro do Definir* do
        // domínio, que dá a garantia enquanto esta antecipação dá a ordem.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        DefinicaoDasEtapas definicao = new(
            command,
            processo,
            new CadastrosDasEtapas(tipoEtapaReader, tipoBancaReader, regraCatalogoReader),
            new AtosConsultadosNaEdicao(tipoAtoPublicadoReader, processoSeletivoRepository, registroCodecs, command.ProcessoSeletivoId, timeProvider),
            unitOfWork);
        Result definida = await definicao.DefinirAsync(cancellationToken).ConfigureAwait(false);
        if (definida.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(definida.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
