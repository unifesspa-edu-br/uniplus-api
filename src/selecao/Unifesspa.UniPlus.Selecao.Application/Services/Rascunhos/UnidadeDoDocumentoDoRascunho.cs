namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A unidade que o documento do rascunho nomeia e o acesso a ela: o código resolve para a unidade viva, e a pessoa precisa
/// poder configurar processos nela. O código que não resolve e a unidade sem permissão dão a mesma recusa, para o cadastro
/// de unidades não ser sondado por quem não tem acesso.
/// </summary>
public sealed class UnidadeDoDocumentoDoRascunho(IUnidadeReader unidades, IVerificadorDeAcesso verificador)
{
    private static readonly DomainError ForaDaPermissao = new(
        RascunhoDeProcessoSeletivoErrorCodes.UnidadeForaDaPermissao,
        "Você não tem permissão para configurar processos seletivos na unidade informada no documento.");

    /// <summary>A unidade viva que tem hoje o código, ou <see langword="null"/> quando nenhuma o tem.</summary>
    public Task<UnidadeView?> ResolverAsync(string codigo, CancellationToken cancellationToken) =>
        unidades.ObterPorCodigoAsync(codigo, cancellationToken);

    /// <summary>Resolve o código e exige a permissão de configurar na unidade: a decisão da criação, em que não há linha de dono.</summary>
    public async Task<Result<UnidadeView>> ResolverEExigirAsync(string codigo, CancellationToken cancellationToken) =>
        await ExigirAsync(await ResolverAsync(codigo, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// A unidade que o rascunho passa a ter. Manter a unidade atual não decide o acesso de novo, porque a trava já o decidiu
    /// sobre ela; mudar de unidade exige a permissão na nova.
    /// </summary>
    public async Task<Result<Guid>> ExigirParaTrocaAsync(UnidadeView? nova, Guid unidadeAtual, CancellationToken cancellationToken)
    {
        if (nova?.Id == unidadeAtual)
        {
            return Result<Guid>.Success(unidadeAtual);
        }

        Result<UnidadeView> exigida = await ExigirAsync(nova, cancellationToken).ConfigureAwait(false);
        return exigida.IsFailure
            ? Result<Guid>.ValidationFailure(exigida.Errors)
            : Result<Guid>.Success(exigida.Value!.Id);
    }

    private async Task<Result<UnidadeView>> ExigirAsync(UnidadeView? unidade, CancellationToken cancellationToken)
    {
        ResultadoDoAcesso acesso = await AcessoAoProcesso
            .ExigirNaUnidadeAsync(
                verificador, unidade?.Id, PermissaoSelecao.ProcessosConfigurar,
                id => AcessoAoProcesso.ResourceDoProcesso(id, processoId: null), cancellationToken)
            .ConfigureAwait(false);

        return acesso switch
        {
            ResultadoDoAcesso.Permitido => Result<UnidadeView>.Success(unidade!),
            ResultadoDoAcesso.Negado => Result<UnidadeView>.Failure(ForaDaPermissao),
            _ => Result<UnidadeView>.Failure(AcessoAoProcesso.Recusa(acesso)),
        };
    }
}
