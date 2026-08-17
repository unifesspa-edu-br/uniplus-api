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
/// Sequência: valida o agregado por inteiro primeiro (sem I/O) — os cinco campos
/// obrigatórios, a referência de cidade e a coerência com o endereço acumulados
/// no mesmo lote (validação sempre vence I/O) — só então confere o guard de
/// singleton (rejeita se já existe Instituição viva) e o vínculo com a Unidade
/// raiz (reitoria), cria o agregado carimbando a proveniência do display cache e
/// persiste. Uma corrida concorrente que passe pelo guard de singleton e colida
/// no índice único parcial é traduzida para o mesmo erro; ao final, invalida o
/// cache do reader cross-módulo (ADR-0056).
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

        DateTimeOffset agora = timeProvider.GetUtcNow();

        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, existente: null, agora);

        Result validacao = Instituicao.ValidarCampos(
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
            enderecoErro is null ? endereco : null,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf);

        if (enderecoErro is not null || validacao.IsFailure)
        {
            List<FieldError> erros = [.. validacao.Errors];
            if (enderecoErro is not null)
            {
                erros.Add(new FieldError("endereco", enderecoErro));
            }

            return Result<Guid>.ValidationFailure(erros);
        }

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

        // Carimbo server-side do display cache da cidade (ADR-0090): com cidade
        // informada, proveniência geo-api + instante atual; sem cidade, ambos nulos.
        bool temCidade = !string.IsNullOrWhiteSpace(command.CidadeCodigoIbge);
        string? cidadeOrigem = temCidade ? ReferenciaCidadeGeo.OrigemGeoApi : null;
        DateTimeOffset? cidadeAtualizadoEm = temCidade ? agora : null;

        // O payload já foi confirmado válido no pré-check acima, com o mesmo
        // endereco resolvido e o mesmo `agora` — Criar não pode falhar de novo.
        Instituicao instituicao = Instituicao.Criar(
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
            command.UnidadeRaizId).Value!;

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
            // de singleton — 409 consistente com o caminho não-race, em vez de
            // deixar o DbUpdateException virar 500 no middleware global. O filtro
            // do `when` garante que outras exceções propagam intactas. Descarta o
            // rastreamento ANTES de devolver: o outbox do Wolverine chama
            // SaveChangesAsync de novo após o handler retornar (ADR-0004), e sem
            // isso a mesma exceção estouraria de novo fora deste catch, virando 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(new DomainError(
                InstituicaoErrorCodes.JaExisteInstituicaoViva,
                "Já existe uma Instituição cadastrada — cada instância da plataforma atende uma única instituição."));
        }

        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(instituicao.Id);
    }
}
