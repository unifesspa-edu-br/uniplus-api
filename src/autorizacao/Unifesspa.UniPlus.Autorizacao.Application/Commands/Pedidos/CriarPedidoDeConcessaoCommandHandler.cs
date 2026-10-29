namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine.Attributes;

/// <summary>
/// Handler da criação. As violações de campo vencem a consulta da unidade: quem erra o campo não espera a leitura.
/// </summary>
public static class CriarPedidoDeConcessaoCommandHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c> porque o handler injeta o leitor de unidades (Organização Institucional, com o seu
    /// próprio <c>DbContext</c>) junto da unidade de trabalho da Autorização: o detector de transação do Wolverine
    /// encontraria dois <c>DbContext</c> e falharia por ambiguidade no boot. A gravação continua atômica, porque o
    /// handler salva uma única vez, e o pedido não levanta evento de domínio que o outbox precise acompanhar.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<PedidoDeConcessaoDto>> Handle(
        CriarPedidoDeConcessaoCommand command,
        IPedidoDeConcessaoRepository repository,
        IPerfilRepository perfis,
        IUnidadeReader unidades,
        IDominiosInstitucionais dominios,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(dominios);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.PessoaDe(userContext) is not { } solicitante)
        {
            return Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.AutorNaoIdentificado());
        }

        Perfil? perfil = await perfis.ObterPorIdAsync(command.PerfilId, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return Result<PedidoDeConcessaoDto>.Failure(new DomainError(
                PerfilErrorCodes.NaoEncontrado, $"Perfil de acesso {command.PerfilId} não encontrado."));
        }

        EscopoDaConcessao escopo = command.UnidadeId is { } unidadeId
            ? EscopoDaConcessao.DaUnidade(unidadeId)
            : EscopoDaConcessao.DaInstituicao();

        Result<PedidoDeConcessao> criado = PedidoDeConcessao.Criar(
            solicitante, command.BeneficiarioEmail, perfil, escopo, command.Justificativa, dominios, timeProvider.GetUtcNow());
        if (criado.IsFailure)
        {
            return Result<PedidoDeConcessaoDto>.ValidationFailure(criado.Errors);
        }

        if (command.UnidadeId is { } unidade
            && await unidades.ObterPorIdAsync(unidade, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.UnidadeInexistente(unidade));
        }

        PedidoDeConcessao pedido = criado.Value!;
        await repository.AdicionarAsync(pedido, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (PedidoViolacoes.EhPedidoDuplicado(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.PedidoDuplicado());
        }

        return Result<PedidoDeConcessaoDto>.Success(PedidoMapping.ToDto(pedido, perfil.Nome));
    }
}
