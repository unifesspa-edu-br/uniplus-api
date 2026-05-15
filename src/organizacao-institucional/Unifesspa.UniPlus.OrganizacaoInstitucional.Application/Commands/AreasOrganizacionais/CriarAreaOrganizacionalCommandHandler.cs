namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.AreasOrganizacionais;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="CriarAreaOrganizacionalCommand"/>. Convention-based
/// para Wolverine — método estático <c>Handle</c> com dependências por
/// parâmetro (mesmo padrão de <c>CriarEditalCommandHandler</c>).
/// </summary>
/// <remarks>
/// <para>Sequência:
/// <list type="number">
///   <item><description>Valida <see cref="AreaCodigo"/> via factory <c>From</c>;</description></item>
///   <item><description>Confirma unicidade do código (retorna 409 via <see cref="AreaOrganizacionalErrorCodes.CodigoJaExiste"/>);</description></item>
///   <item><description>Cria o agregado via <see cref="AreaOrganizacional.Criar"/>;</description></item>
///   <item><description>Persiste + commit;</description></item>
///   <item><description>Invalida o cache do reader cross-módulo (ADR-0056 §"Carve-out read-side").</description></item>
/// </list></para>
/// <para>Idempotência cross-replica é responsabilidade do filter
/// <c>RequiresIdempotencyKey</c> (ADR-0027) configurado no controller, não
/// do handler.</para>
/// </remarks>
public static class CriarAreaOrganizacionalCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarAreaOrganizacionalCommand command,
        IAreaOrganizacionalRepository repository,
        IUnitOfWork unitOfWork,
        IAreaOrganizacionalCacheInvalidator cacheInvalidator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);

        Result<AreaCodigo> codigoResult = AreaCodigo.From(command.Codigo);
        if (codigoResult.IsFailure)
        {
            return Result<Guid>.Failure(codigoResult.Error!);
        }

        AreaCodigo codigo = codigoResult.Value!;
        if (await repository.ExistePorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.CodigoJaExiste,
                $"Já existe uma área organizacional com o código '{codigo}'."));
        }

        Result<AreaOrganizacional> areaResult = AreaOrganizacional.Criar(
            codigo,
            command.Nome,
            command.Tipo,
            command.Descricao,
            command.AdrReferenceCode);
        if (areaResult.IsFailure)
        {
            return Result<Guid>.Failure(areaResult.Error!);
        }

        AreaOrganizacional area = areaResult.Value!;
        await repository.AdicionarAsync(area, cancellationToken).ConfigureAwait(false);

        // NOTA: Race com criação concorrente que passe pelo ExistePorCodigoAsync e
        // chegue ao SaveChanges é improvável (endpoint admin gated por role
        // plataforma-admin + Idempotency-Key ADR-0027) e fica protegida pela unique
        // index `ix_areas_organizacionais_codigo` no banco. A tradução final
        // unique_violation → 409 é follow-up via GlobalExceptionMiddleware (mesmo
        // padrão de EfCoreIdempotencyStore.IsUniqueViolation). Documentado no PR.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        // Invalidação best-effort pós-commit (ADR-0056 §"Carve-out read-side").
        // Falha aqui é absorvida pela impl (loga warning); o TTL natural (5 min)
        // resolve no pior caso.
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(area.Id);
    }
}
