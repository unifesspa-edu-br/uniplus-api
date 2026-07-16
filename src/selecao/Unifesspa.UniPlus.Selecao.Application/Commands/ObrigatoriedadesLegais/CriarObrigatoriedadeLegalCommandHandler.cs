namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based do <see cref="CriarObrigatoriedadeLegalCommand"/>.
/// Checa colisão de <c>RegraCodigo</c>, chama a factory canônica da entity e
/// persiste via repositório. A regra é cross-cutting por tipo de processo —
/// sem proprietário nem áreas de interesse.
/// </summary>
public static class CriarObrigatoriedadeLegalCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        bool duplicado = await repository.ExisteRegraCodigoAtivoAsync(
            command.RegraCodigo,
            excluirId: null,
            cancellationToken).ConfigureAwait(false);
        if (duplicado)
        {
            return Result<Guid>.Failure(new DomainError(
                "ObrigatoriedadeLegal.RegraCodigoDuplicada",
                $"Já existe regra ativa com RegraCodigo '{command.RegraCodigo}'."));
        }

        Result<ObrigatoriedadeLegal> regraResult = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: command.TipoProcessoCodigo,
            categoria: command.Categoria,
            regraCodigo: command.RegraCodigo,
            predicado: command.Predicado,
            descricaoHumana: command.DescricaoHumana,
            baseLegal: command.BaseLegal,
            vigenciaInicio: command.VigenciaInicio,
            vigenciaFim: command.VigenciaFim,
            atoNormativoUrl: command.AtoNormativoUrl,
            portariaInternaCodigo: command.PortariaInternaCodigo);
        if (regraResult.IsFailure)
        {
            return Result<Guid>.Failure(regraResult.Error!);
        }

        ObrigatoriedadeLegal regra = regraResult.Value!;

        await repository.AdicionarAsync(regra, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint)
        {
            // Race entre ExisteRegraCodigoAtivoAsync e o INSERT (check-then-act):
            // a constraint UNIQUE parcial sobre regra_codigo/hash dispara 23505,
            // viramos 409 ProblemDetails consistente com o caminho não-race.
            // Filtro do `when` garante que outras exceções não-23505 propagam
            // intactas (não engolimos falhas inesperadas).
            if (UniqueConstraintViolation.IsRegraCodigoConflict(constraint))
            {
                return Result<Guid>.Failure(new DomainError(
                    "ObrigatoriedadeLegal.RegraCodigoDuplicada",
                    $"Já existe regra ativa com RegraCodigo '{command.RegraCodigo}'."));
            }
            if (UniqueConstraintViolation.IsHashConflict(constraint))
            {
                return Result<Guid>.Failure(new DomainError(
                    "ObrigatoriedadeLegal.HashColisao",
                    "Já existe regra ativa com o mesmo conteúdo canônico."));
            }
            throw;
        }

        return Result<Guid>.Success(regra.Id);
    }
}
