namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="CriarInstituicaoCommand"/>. Convention-based para
/// Wolverine — método estático <c>Handle</c> com dependências por parâmetro.
/// </summary>
/// <remarks>
/// Sequência:
/// <list type="number">
///   <item>Guard de domínio do singleton: rejeita se já existe Instituição viva (CA-02);</item>
///   <item>Valida o vínculo com a Unidade raiz (reitoria), se informado (CA-04);</item>
///   <item>Cria o agregado via <see cref="Instituicao.Criar"/>;</item>
///   <item>Persiste + commit; uma corrida concorrente que passe pelo guard e
///   colida no índice único parcial é traduzida para o mesmo erro de singleton (CA-02);</item>
///   <item>Invalida o cache do reader cross-módulo (ADR-0056).</item>
/// </list>
/// </remarks>
public static class CriarInstituicaoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarInstituicaoCommand command,
        IInstituicaoRepository repository,
        IUnidadeRepository unidadeRepository,
        IOrganizacaoInstitucionalUnitOfWork unitOfWork,
        IInstituicaoCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unidadeRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (await repository.ExisteAlgumaVivaAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                InstituicaoErrorCodes.JaExisteInstituicaoViva,
                "Já existe uma Instituição cadastrada — cada instância da plataforma atende uma única instituição."));
        }

        DomainError? vinculoInvalido = await InstituicaoUnidadeRaizGuard
            .ValidarAsync(command.UnidadeRaizId, unidadeRepository, cancellationToken)
            .ConfigureAwait(false);
        if (vinculoInvalido is not null)
        {
            return Result<Guid>.Failure(vinculoInvalido);
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();

        // Carimbo server-side do display cache da cidade (ADR-0090): com cidade
        // informada, proveniência geo-api + instante atual; sem cidade, ambos nulos.
        bool temCidade = !string.IsNullOrWhiteSpace(command.CidadeCodigoIbge);
        string? cidadeOrigem = temCidade ? ReferenciaCidadeGeo.OrigemGeoApi : null;
        DateTimeOffset? cidadeAtualizadoEm = temCidade ? agora : null;

        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, existente: null, agora);
        if (enderecoErro is not null)
        {
            return Result<Guid>.Failure(enderecoErro);
        }

        Result<Instituicao> instituicaoResult = Instituicao.Criar(
            command.CodigoEmec,
            command.Nome,
            command.Sigla,
            command.OrganizacaoAcademica,
            command.CategoriaAdministrativa,
            command.Cnpj,
            command.Mantenedora,
            command.CodigoMantenedoraEmec,
            command.Situacao,
            command.AtoCredenciamento,
            command.AtoRecredenciamento,
            command.ConceitoInstitucional,
            command.Igc,
            command.Website,
            endereco,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            cidadeOrigem,
            cidadeAtualizadoEm,
            command.UnidadeRaizId);

        if (instituicaoResult.IsFailure)
        {
            return Result<Guid>.Failure(instituicaoResult.Error!);
        }

        Instituicao instituicao = instituicaoResult.Value!;
        await repository.AdicionarAsync(instituicao, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsSingletonConflict(constraint))
        {
            // Corrida entre ExisteAlgumaVivaAsync e o INSERT (check-then-act): o
            // índice único parcial sentinela dispara 23505 e viramos o mesmo erro
            // de singleton (CA-02) — 409 consistente com o caminho não-race, em vez
            // de deixar o DbUpdateException virar 500 no middleware global. O filtro
            // do `when` garante que outras exceções propagam intactas.
            return Result<Guid>.Failure(new DomainError(
                InstituicaoErrorCodes.JaExisteInstituicaoViva,
                "Já existe uma Instituição cadastrada — cada instância da plataforma atende uma única instituição."));
        }

        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(instituicao.Id);
    }
}
