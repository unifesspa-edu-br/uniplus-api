namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da aprovação. A concessão nasce na última aprovação exigida, na mesma transação: a concessão igual vencida e
/// não revogada é revogada antes, porque o índice único a trata como existente. A concessão ativa igual recusa o pedido.
/// </summary>
public static class AprovarPedidoDeConcessaoCommandHandler
{
    public static async Task<Result<PedidoDeConcessaoDto>> Handle(
        AprovarPedidoDeConcessaoCommand command,
        IPedidoDeConcessaoRepository pedidos,
        IConcessaoRepository concessoes,
        IPerfilRepository perfis,
        IVerificadorDeAcesso verificador,
        CatalogoDaAplicacao catalogo,
        ICatalogoDePermissoesConsultavel catalogoDoDominio,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(catalogoDoDominio);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<DecisaoDaEtapa> preparada = await DecisaoDaEtapa
            .PrepararAsync(command.Id, pedidos, concessoes, verificador, catalogo, IdentidadeDoToken.De(userContext), cancellationToken)
            .ConfigureAwait(false);
        if (preparada.IsFailure)
        {
            return Result<PedidoDeConcessaoDto>.Failure(preparada.Error!);
        }

        (PedidoDeConcessao pedido, Perfil perfil, PerfilAvaliado avaliado, Identidade decisor) = preparada.Value!;
        DateTimeOffset agora = timeProvider.GetUtcNow();

        bool jaTemProtecaoDeDados = pedido.Beneficiario is { } beneficiario
            && await concessoes.TemConcessaoAtivaDoPerfilAsync(beneficiario, PerfisDeSistema.ProtecaoDeDadosId, agora, cancellationToken)
                .ConfigureAwait(false);

        Result<DesfechoDaAprovacao> aprovacao = pedido.Aprovar(decisor, avaliado, new ContextoDaAprovacao(jaTemProtecaoDeDados), agora);
        if (aprovacao.IsFailure)
        {
            // O perfil alterado entre as etapas devolve o pedido ao gestor, e esse estado precisa ser gravado.
            if (aprovacao.Error!.Code == Domain.Errors.PedidoErrorCodes.PerfilAlterado)
            {
                return await SalvarRecusaAsync(unitOfWork, aprovacao.Error!, cancellationToken).ConfigureAwait(false);
            }

            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<PedidoDeConcessaoDto>.ValidationFailure(aprovacao.Errors);
        }

        try
        {
            if (aprovacao.Value == DesfechoDaAprovacao.Concluido)
            {
                Result concedida = await ConcederAsync(pedido, perfil, decisor, concessoes, catalogoDoDominio, agora, cancellationToken)
                    .ConfigureAwait(false);
                if (concedida.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<PedidoDeConcessaoDto>.ValidationFailure(concedida.Errors);
                }
            }

            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ConcessaoViolacoes.EhConcessaoDuplicada(ex))
        {
            // Duas aprovações correram juntas, ou a concessão igual nasceu entre a leitura e a gravação.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.JaTemConcessao());
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.ConflitoDeConcorrencia());
        }

        return Result<PedidoDeConcessaoDto>.Success(
            await RespostaDoPedido.ParaDtoAsync(pedido, perfis, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<Result<PedidoDeConcessaoDto>> SalvarRecusaAsync(
        IAutorizacaoUnitOfWork unitOfWork, DomainError erro, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.ConflitoDeConcorrencia());
        }

        return Result<PedidoDeConcessaoDto>.Failure(erro);
    }

    private static async Task<Result> ConcederAsync(
        PedidoDeConcessao pedido,
        Perfil perfil,
        Identidade decisor,
        IConcessaoRepository concessoes,
        ICatalogoDePermissoesConsultavel catalogo,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        Identidade beneficiario = pedido.Beneficiario!;

        Concessao? igual = await concessoes
            .ObterNaoRevogadaIgualAsync(beneficiario, perfil.Id, pedido.Escopo, cancellationToken)
            .ConfigureAwait(false);
        if (igual?.EstaAtiva(agora) == true)
        {
            return Result.Failure(PedidoHandlerErros.JaTemConcessao());
        }

        // A concessão nova se constrói e valida antes de qualquer gravação: se ela falhar, nada fica gravado.
        Result<Concessao> concessao = Concessao.Conceder(
            beneficiario, perfil, pedido.Escopo, new OrigemDaConcessao(TipoDeOrigem.Pedido, pedido.Id), decisor, agora, catalogo,
            beneficiarioEmail: pedido.BeneficiarioEmail);
        if (concessao.IsFailure)
        {
            return Result.ValidationFailure(concessao.Errors);
        }

        if (igual is not null)
        {
            // A vencida não se encerra sozinha e, enquanto não revogada, o índice único recusa a nova. A revogação e a
            // concessão vão na mesma gravação, que é atômica: a falha de uma não deixa a outra, nem o pedido aprovado,
            // e o EF emite a revogação antes da inserção.
            Result revogada = igual.Revogar(decisor, $"Renovação pelo pedido {pedido.Id}.", agora);
            if (revogada.IsFailure)
            {
                return revogada;
            }
        }

        pedido.RegistrarConcessao(concessao.Value!.Id);
        await concessoes.AdicionarAsync(concessao.Value, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
