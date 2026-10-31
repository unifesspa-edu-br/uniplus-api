namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da criação. As violações de campo vencem a consulta da unidade: quem erra o campo não espera a leitura. Os
/// avisos do pedido saem como mensagens de saída e entram no outbox na mesma transação da gravação.
/// </summary>
public static class CriarPedidoDeConcessaoCommandHandler
{
    public static async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        CriarPedidoDeConcessaoCommand command,
        IPedidoDeConcessaoRepository repository,
        IPerfilRepository perfis,
        ILeitorDeUnidade unidades,
        IDominiosInstitucionais dominios,
        ICatalogoDePermissoesConsultavel catalogo,
        AvisosDoPedido avisos,
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
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.PessoaDe(userContext) is not { } solicitante)
        {
            return Recusa(PedidoHandlerErros.AutorNaoIdentificado());
        }

        Perfil? perfil = await perfis.ObterPorIdAsync(command.PerfilId, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return Recusa(new DomainError(
                PerfilErrorCodes.NaoEncontrado, $"Perfil de acesso {command.PerfilId} não encontrado."));
        }

        EscopoDaConcessao escopo = command.UnidadeId is { } unidadeId
            ? EscopoDaConcessao.DaUnidade(unidadeId)
            : EscopoDaConcessao.DaInstituicao();

        Result<PedidoDeConcessao> criado = PedidoDeConcessao.Criar(
            solicitante, command.BeneficiarioEmail, perfil, escopo, command.Justificativa, dominios, catalogo, timeProvider.GetUtcNow(),
            command.ValidaAteSugerida);
        if (criado.IsFailure)
        {
            return (Result<PedidoDeConcessaoDto>.ValidationFailure(criado.Errors), []);
        }

        if (command.UnidadeId is { } unidade
            && await unidades.ObterPorIdAsync(unidade, cancellationToken).ConfigureAwait(false) is null)
        {
            return Recusa(PedidoHandlerErros.UnidadeInexistente(unidade));
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
            return Recusa(PedidoHandlerErros.PedidoDuplicado());
        }

        IReadOnlyList<EnviarEmail> mensagens = await avisos
            .AvisarAsync(pedido, perfil.Nome, situacaoAnterior: null, cancellationToken)
            .ConfigureAwait(false);

        return (Result<PedidoDeConcessaoDto>.Success(PedidoMapping.ToDto(pedido, perfil)), mensagens);
    }

    private static (Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<PedidoDeConcessaoDto>.Failure(erro), []);
}
