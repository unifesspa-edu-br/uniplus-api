namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public static class AtualizarInstituicaoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarInstituicaoCommand command,
        IInstituicaoRepository repository,
        IUnidadeRepository unidadeRepository,
        IUnitOfWork unitOfWork,
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

        Instituicao? instituicao = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (instituicao is null)
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.NaoEncontrada,
                "Instituição não encontrada."));
        }

        DomainError? vinculoInvalido = await InstituicaoUnidadeRaizGuard
            .ValidarAsync(command.UnidadeRaizId, unidadeRepository, cancellationToken)
            .ConfigureAwait(false);
        if (vinculoInvalido is not null)
        {
            return Result.Failure(vinculoInvalido);
        }

        // Só recarimba a proveniência/frescura do display cache quando o trio de
        // cidade efetivamente muda — assim cidade_display_atualizado_em rastreia a
        // última reconciliação da cidade, não qualquer edição de outro campo. Sem
        // cidade no payload, ambos zeram (a entidade também zera o trio).
        bool temCidade = !string.IsNullOrWhiteSpace(command.CidadeCodigoIbge);
        bool cidadeMudou = CidadeReferenciaMudou(command, instituicao);
        string? cidadeOrigem = temCidade
            ? (cidadeMudou ? ReferenciaCidadeGeo.OrigemGeoApi : instituicao.CidadeOrigem)
            : null;
        DateTimeOffset? cidadeAtualizadoEm = temCidade
            ? (cidadeMudou ? timeProvider.GetUtcNow() : instituicao.CidadeDisplayAtualizadoEm)
            : null;

        Result atualizarResult = instituicao.Atualizar(
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
            command.EnderecoSede,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            cidadeOrigem,
            cidadeAtualizadoEm,
            command.UnidadeRaizId);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Indica se o trio de referência de cidade do comando difere do estado
    /// persistido, comparando os valores já normalizados (código/nome aparados,
    /// UF em caixa alta). Cobre transições presente→ausente e ausente→presente.
    /// </summary>
    private static bool CidadeReferenciaMudou(AtualizarInstituicaoCommand command, Instituicao instituicao)
    {
        string? codigo = NormalizarOpcional(command.CidadeCodigoIbge);
        string? nome = NormalizarOpcional(command.CidadeNome);
        string? uf = NormalizarOpcional(command.CidadeUf)?.ToUpperInvariant();

        return !string.Equals(codigo, instituicao.CidadeCodigoIbge, StringComparison.Ordinal)
            || !string.Equals(nome, instituicao.CidadeNome, StringComparison.Ordinal)
            || !string.Equals(uf, instituicao.CidadeUf, StringComparison.Ordinal);
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
