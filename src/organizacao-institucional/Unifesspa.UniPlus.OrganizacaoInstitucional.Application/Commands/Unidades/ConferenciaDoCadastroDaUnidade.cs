namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// As conferências que antecedem a gravação da Unidade, na ordem em que recusam: o payload inteiro
/// (validação sempre vence I/O e 404), depois o que depende do repositório. Slug, sigla e código são únicos
/// entre as Unidades vivas, conferidos nesta ordem; na atualização, só o identificador cujo valor muda. O
/// superior não pode ser a própria Unidade, deve existir e, na atualização, não pode ser descendente da
/// Unidade editada (formaria ciclo na hierarquia); na criação, só a existência é conferida.
/// </summary>
public sealed class ConferenciaDoCadastroDaUnidade
{
    private readonly IUnidadeRepository _unidades;
    private readonly IInstituicaoRepository _instituicoes;

    public ConferenciaDoCadastroDaUnidade(IUnidadeRepository unidades, IInstituicaoRepository instituicoes)
    {
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(instituicoes);

        _unidades = unidades;
        _instituicoes = instituicoes;
    }

    internal async Task<Result<Unidade>> ConferirNovaAsync(CriarUnidadeCommand command, CancellationToken cancellationToken)
    {
        Result validacao = Unidade.ValidarCampos(
            command.Nome, command.Identificadores(), command.Cidade(), TipoDaUnidadeDaEntrada.Ler(command.Tipo), command.Vigencia());
        if (validacao.IsFailure)
        {
            return Result<Unidade>.ValidationFailure(validacao.Errors);
        }

        DomainError? recusa = await RecusaDosConflitosAsync(
            command.Identificadores(), null, command.UnidadeSuperiorId, cancellationToken).ConfigureAwait(false);
        return recusa is null ? NovaUnidade(command) : Result<Unidade>.Failure(recusa);
    }

    internal async Task<Result<Unidade>> ConferirAtualizacaoAsync(
        AtualizarUnidadeCommand command, CancellationToken cancellationToken)
    {
        Result payload = ValidarPayloadDaAtualizacao(command);
        if (payload.IsFailure)
        {
            return Result<Unidade>.ValidationFailure(payload.Errors);
        }

        Unidade? unidade = await _unidades.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        DomainError? recusa = unidade is null
            ? UnidadeHandlerErros.NaoEncontrada()
            : await RecusaDosConflitosAsync(
                command.Identificadores(), unidade, command.UnidadeSuperiorId, cancellationToken).ConfigureAwait(false);
        return recusa is null ? Result<Unidade>.Success(unidade!) : Result<Unidade>.Failure(recusa);
    }

    internal async Task<Result<Unidade>> ConferirRemocaoAsync(Guid unidadeId, CancellationToken cancellationToken)
    {
        Unidade? unidade = await _unidades.ObterPorIdAsync(unidadeId, cancellationToken).ConfigureAwait(false);
        DomainError? recusa = unidade is null
            ? UnidadeHandlerErros.NaoEncontrada()
            : await RecusaDaRemocaoAsync(unidadeId, cancellationToken).ConfigureAwait(false);
        return recusa is null ? Result<Unidade>.Success(unidade!) : Result<Unidade>.Failure(recusa);
    }

    // A coerência entre VigenciaFim e VigenciaInicio exige o valor persistido, então
    // roda de novo dentro de Unidade.Atualizar, depois de buscar a Unidade.
    private static Result ValidarPayloadDaAtualizacao(AtualizarUnidadeCommand command)
    {
        Result campos = Unidade.ValidarCampos(
            command.Nome,
            command.Identificadores(),
            command.Cidade(),
            TipoDaUnidadeDaEntrada.Ler(command.Tipo),
            new VigenciaDaUnidade(DateOnly.MinValue, null));
        Result motivo = UnidadeIdentificadorHistorico.ValidarMotivoMudanca(command.MotivoMudancaIdentificador);

        return campos.IsFailure || motivo.IsFailure
            ? Result.ValidationFailure([.. campos.Errors, .. motivo.Errors])
            : Result.Success();
    }

    private static Result<Unidade> NovaUnidade(CriarUnidadeCommand command) =>
        Unidade.Criar(
            command.Nome,
            command.Identificadores(),
            command.Cidade(),
            command.Vigencia(),
            command.UnidadeSuperiorId,
            CatalogoDe<TipoUnidade>.Obter(command.Tipo!),
            command.UnidadeAcademica);

    private async Task<DomainError?> RecusaDosConflitosAsync(
        IdentificadoresDaUnidade novos, Unidade? atual, Guid? superiorId, CancellationToken cancellationToken) =>
        await RecusaDosIdentificadoresAsync(novos, atual, cancellationToken).ConfigureAwait(false)
        ?? await RecusaDoSuperiorAsync(superiorId, atual?.Id, cancellationToken).ConfigureAwait(false);

    private async Task<DomainError?> RecusaDosIdentificadoresAsync(
        IdentificadoresDaUnidade novos, Unidade? atual, CancellationToken cancellationToken) =>
        await RecusaDoSlugAsync(novos, atual, cancellationToken).ConfigureAwait(false)
        ?? await RecusaDaSiglaAsync(novos, atual, cancellationToken).ConfigureAwait(false)
        ?? await RecusaDoCodigoAsync(novos, atual, cancellationToken).ConfigureAwait(false);

    private async Task<DomainError?> RecusaDoSlugAsync(
        IdentificadoresDaUnidade novos, Unidade? atual, CancellationToken cancellationToken)
    {
        Slug slug = Slug.From(novos.Slug).Value!;
        bool mudou = atual is null || !string.Equals(slug.Valor, atual.Slug.Valor, StringComparison.OrdinalIgnoreCase);

        return mudou && await _unidades.SlugExisteEntreVivosAsync(slug, atual?.Id, cancellationToken).ConfigureAwait(false)
            ? UnidadeHandlerErros.SlugJaExiste(slug)
            : null;
    }

    private async Task<DomainError?> RecusaDaSiglaAsync(
        IdentificadoresDaUnidade novos, Unidade? atual, CancellationToken cancellationToken)
    {
        bool mudou = atual is null || !string.Equals(novos.Sigla, atual.Sigla, StringComparison.OrdinalIgnoreCase);

        return mudou && await _unidades.SiglaExisteEntreVivosAsync(novos.Sigla!, atual?.Id, cancellationToken).ConfigureAwait(false)
            ? UnidadeHandlerErros.SiglaJaExiste(novos.Sigla)
            : null;
    }

    private async Task<DomainError?> RecusaDoCodigoAsync(
        IdentificadoresDaUnidade novos, Unidade? atual, CancellationToken cancellationToken)
    {
        // Código é case-sensitive (o agregado preserva a caixa e o índice único também):
        // Ordinal faz ABC→abc disparar a conferência em vez de estourar no índice.
        bool mudou = atual is null || !string.Equals(novos.Codigo!.Trim(), atual.Codigo, StringComparison.Ordinal);

        return mudou && await _unidades.CodigoExisteEntreVivosAsync(novos.Codigo!, atual?.Id, cancellationToken).ConfigureAwait(false)
            ? UnidadeHandlerErros.CodigoJaExiste(novos.Codigo)
            : null;
    }

    private async Task<DomainError?> RecusaDoSuperiorAsync(Guid? superiorId, Guid? unidadeId, CancellationToken cancellationToken)
    {
        if (superiorId is not { } superior)
        {
            return null;
        }

        return superior == unidadeId
            ? UnidadeHandlerErros.SuperiorIgualAUnidade()
            : await RecusaDaHierarquiaAsync(superior, unidadeId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DomainError?> RecusaDaHierarquiaAsync(Guid superiorId, Guid? unidadeId, CancellationToken cancellationToken)
    {
        if (await _unidades.ObterPorIdAsync(superiorId, cancellationToken).ConfigureAwait(false) is null)
        {
            return UnidadeHandlerErros.SuperiorNaoEncontrado();
        }

        return unidadeId is { } id
            && await _unidades.EhDescendenteAsync(superiorId, id, cancellationToken).ConfigureAwait(false)
            ? UnidadeHandlerErros.SuperiorDescendenteDaUnidade()
            : null;
    }

    /// <remarks>
    /// A conferência da Instituição é check-then-act, simétrica à das subordinadas. Sob concorrência (remoção
    /// desta Unidade × criação/edição de uma Instituição que a escolhe como raiz) ela pode ver <c>false</c>
    /// antes do commit da Instituição, e como a remoção é soft-delete a FK Restrict não barra o estado final
    /// (Instituição viva → Unidade removida). A serialização estrita (lock/isolamento serializável) é controle
    /// de concorrência transversal a todos os cadastros.
    /// </remarks>
    private async Task<DomainError?> RecusaDaRemocaoAsync(Guid unidadeId, CancellationToken cancellationToken)
    {
        if (await _unidades.PossuiSubordinadasVivasAsync(unidadeId, cancellationToken).ConfigureAwait(false))
        {
            return UnidadeHandlerErros.RemocaoBloqueadaPorSubordinadas();
        }

        return await _instituicoes.ExisteComUnidadeRaizAsync(unidadeId, cancellationToken).ConfigureAwait(false)
            ? UnidadeHandlerErros.RemocaoBloqueadaPorInstituicao()
            : null;
    }
}
