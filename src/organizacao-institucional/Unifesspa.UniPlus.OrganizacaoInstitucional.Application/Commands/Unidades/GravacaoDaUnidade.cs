namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// A gravação da Unidade já conferida: grava e invalida o cache do reader cross-módulo (ADR-0056) só depois
/// de a gravação ter sucesso.
/// </summary>
public sealed class GravacaoDaUnidade
{
    private readonly IUnidadeRepository _unidades;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUnidadeCacheInvalidator _cache;

    public GravacaoDaUnidade(IUnidadeRepository unidades, IUnitOfWork unitOfWork, IUnidadeCacheInvalidator cache)
    {
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cache);

        _unidades = unidades;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    internal async Task<Result<Guid>> GravarNovaAsync(Unidade nova, CancellationToken cancellationToken)
    {
        await _unidades.AdicionarAsync(nova, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await _cache.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(nova.Id);
    }

    internal async Task<Result> GravarAtualizacaoAsync(
        Unidade unidade, AtualizarUnidadeCommand command, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result aplicada = AplicarAtualizacao(unidade, command, agora);
        if (aplicada.IsFailure)
        {
            return aplicada;
        }

        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await _cache.InvalidarAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    internal async Task GravarRemocaoAsync(Unidade unidade, CancellationToken cancellationToken)
    {
        // SoftDeleteInterceptor converte EntityState.Deleted em soft-delete
        // (Unidade é ISoftDeletable), preenchendo DeletedBy/DeletedAt a partir de
        // IUserContext + TimeProvider. O Historico carregado por ObterPorIdAsync
        // NÃO é cascateado (FK Restrict): a trilha append-only de
        // identificadores é preservada mesmo no soft-delete da Unidade.
        _unidades.Remover(unidade);

        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await _cache.InvalidarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Result AplicarAtualizacao(Unidade unidade, AtualizarUnidadeCommand command, DateTimeOffset agora) =>
        unidade.Atualizar(
            command.Nome,
            new MudancaDeIdentificadores(
                command.Identificadores(),
                DateOnly.FromDateTime(agora.UtcDateTime),
                command.MotivoMudancaIdentificador),
            command.Cidade(),
            command.VigenciaFim,
            command.UnidadeSuperiorId,
            command.Tipo,
            command.UnidadeAcademica);
}
