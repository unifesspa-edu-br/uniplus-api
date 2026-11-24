namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// A gravação da Instituição já conferida: grava e invalida o cache do reader cross-módulo (ADR-0056)
/// só depois de a gravação ter sucesso.
/// </summary>
public sealed class GravacaoDaInstituicao
{
    private readonly IInstituicaoRepository _instituicoes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInstituicaoCacheInvalidator _cache;

    public GravacaoDaInstituicao(
        IInstituicaoRepository instituicoes, IUnitOfWork unitOfWork, IInstituicaoCacheInvalidator cache)
    {
        ArgumentNullException.ThrowIfNull(instituicoes);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cache);

        _instituicoes = instituicoes;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    internal async Task<Result<Guid>> GravarNovaAsync(Instituicao nova, CancellationToken cancellationToken)
    {
        await _instituicoes.AdicionarAsync(nova, cancellationToken).ConfigureAwait(false);
        if (await SalvarComoSingletonAsync(cancellationToken).ConfigureAwait(false) is { } conflito)
        {
            return Result<Guid>.Failure(conflito);
        }

        await _cache.InvalidarAsync(cancellationToken).ConfigureAwait(false);
        return Result<Guid>.Success(nova.Id);
    }

    internal async Task<Result> GravarAtualizacaoAsync(
        Instituicao instituicao, AtualizarInstituicaoCommand command, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result aplicada = AplicarAtualizacao(instituicao, command, agora);
        if (aplicada.IsFailure)
        {
            return aplicada;
        }

        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await _cache.InvalidarAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    internal async Task GravarRemocaoAsync(Instituicao instituicao, CancellationToken cancellationToken)
    {
        // SoftDeleteInterceptor converte EntityState.Deleted em soft-delete
        // preenchendo DeletedBy/DeletedAt a partir de IUserContext + TimeProvider.
        _instituicoes.Remover(instituicao);

        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await _cache.InvalidarAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Traduz a corrida entre a conferência de que não há Instituição viva e o INSERT (check-then-act): o
    /// índice único parcial sentinela dispara 23505 e vira o mesmo erro de singleton — 409 consistente com
    /// o caminho sem corrida, em vez de o <c>DbUpdateException</c> virar 500 no middleware global. Outras
    /// exceções propagam intactas. O rastreamento é descartado ANTES de devolver o erro: o outbox do
    /// Wolverine chama <c>SaveChangesAsync</c> de novo depois que o handler retorna (ADR-0004, ADR-0119), e
    /// sem o descarte a mesma exceção estouraria de novo fora do <c>catch</c>, virando 500.
    /// </summary>
    private async Task<DomainError?> SalvarComoSingletonAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (string.Equals(
            ConstraintVioladaNaGravacao.NomeDaConstraintViolada(ex), RestricoesDoBanco.InstituicaoUnicaViva, StringComparison.Ordinal))
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return InstituicaoHandlerErros.JaExisteInstituicaoViva();
        }
    }

    /// <summary>
    /// Resolve o endereço de novo contra o atual, para preservar o instante do display cache quando o
    /// conteúdo não muda, e carimba a cidade contra a gravada. O formato do endereço já foi confirmado
    /// válido na prevalidação, com o mesmo payload e o mesmo instante, então não pode falhar por formato.
    /// </summary>
    private static Result AplicarAtualizacao(Instituicao instituicao, AtualizarInstituicaoCommand command, DateTimeOffset agora)
    {
        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, instituicao.Endereco, agora);

        return instituicao.Atualizar(
            command.Identificacao(),
            command.Cidade().Carimbada(instituicao.CidadeDaSede, agora),
            enderecoErro is null ? endereco : null,
            command.UnidadeRaizId);
    }
}
