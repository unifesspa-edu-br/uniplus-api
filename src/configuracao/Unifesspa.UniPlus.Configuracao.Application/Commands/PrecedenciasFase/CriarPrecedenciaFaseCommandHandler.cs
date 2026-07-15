namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarPrecedenciaFaseCommand"/> (convention-based
/// Wolverine). Trava o grafo para escrita, carrega o grafo vigente (arestas
/// vivas) e o passa como parâmetro para a factory <see cref="PrecedenciaFase.Criar"/>
/// — o domínio não navega/consulta (ADR-0042); as guardas de self-loop, aresta
/// duplicada e ciclo (422) moram lá.
/// </summary>
public static class CriarPrecedenciaFaseCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarPrecedenciaFaseCommand command,
        IPrecedenciaFaseRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // Serializa contra outra escrita concorrente no grafo ANTES de ler as
        // arestas vivas — sem isso, duas arestas distintas (ex. A→B e B→A)
        // podem cada uma passar a guarda de ciclo vendo o grafo sem a aresta da
        // outra e juntas formarem um ciclo que a UNIQUE de par não protege.
        await repository.TravarGrafoParaEscritaAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PrecedenciaFase> arestasVivas = await repository
            .ListarVivasAsync(cancellationToken)
            .ConfigureAwait(false);

        Result<PrecedenciaFase> arestaResult = PrecedenciaFase.Criar(
            command.AntecessoraCodigo,
            command.SucessoraCodigo,
            command.PermiteSobreposicao,
            arestasVivas);

        if (arestaResult.IsFailure)
        {
            return Result<Guid>.Failure(arestaResult.Error!);
        }

        PrecedenciaFase aresta = arestaResult.Value!;

        await repository.AdicionarAsync(aresta, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsParConflict(constraint))
        {
            // Corrida entre ListarVivasAsync e o INSERT (check-then-act): o índice
            // único parcial dispara 23505 e viramos o mesmo ArestaDuplicada do
            // caminho não-race — 422 consistente. O filtro do `when` garante que
            // outras exceções propagam intactas.
            return Result<Guid>.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.ArestaDuplicada,
                $"Já existe uma aresta de precedência viva de '{aresta.AntecessoraCodigo}' para '{aresta.SucessoraCodigo}'."));
        }

        return Result<Guid>.Success(aresta.Id);
    }
}
