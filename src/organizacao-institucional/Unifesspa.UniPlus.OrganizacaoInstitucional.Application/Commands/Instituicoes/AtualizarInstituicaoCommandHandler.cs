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
/// Handler do <see cref="AtualizarInstituicaoCommand"/>. Valida antes de I/O só
/// o que é determinável sem o registro persistido — os cinco campos
/// obrigatórios, a referência de cidade e a coerência do endereço com a cidade
/// do próprio payload — e só então busca a Instituição por Id (validação
/// sempre vence 404). A resolução final do endereço (preservando o instante do
/// display cache quando o conteúdo não muda) só é possível depois do fetch, mas
/// o formato já foi confirmado válido no pré-check com o mesmo payload e o
/// mesmo instante — não pode falhar de novo.
/// </summary>
public static class AtualizarInstituicaoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarInstituicaoCommand command,
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

        (DomainError? enderecoErroPreCheck, ReferenciaEnderecoGeo? enderecoPreCheck) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, existente: null, agora);

        Result validacaoPreCheck = Instituicao.ValidarCampos(
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
            enderecoErroPreCheck is null ? enderecoPreCheck : null,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf);

        List<FieldError> errosPreCheck = [.. validacaoPreCheck.Errors];
        if (enderecoErroPreCheck is not null)
        {
            errosPreCheck.Add(new FieldError("endereco", enderecoErroPreCheck));
        }

        if (errosPreCheck.Count > 0)
        {
            return Result.ValidationFailure(errosPreCheck);
        }

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
            ? (cidadeMudou ? agora : instituicao.CidadeDisplayAtualizadoEm)
            : null;

        // Resolve de novo com o Endereco atual (para preservar o instante do
        // display cache quando o conteúdo não muda) — o formato já foi confirmado
        // válido no pré-check acima, com o mesmo payload e o mesmo `agora`, então
        // esta chamada não pode falhar por formato.
        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, instituicao.Endereco, agora);

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
            enderecoErro is null ? endereco : null,
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
