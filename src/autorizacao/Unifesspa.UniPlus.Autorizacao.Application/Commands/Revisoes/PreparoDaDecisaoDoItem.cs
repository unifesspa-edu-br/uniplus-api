namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que confirmar e revogar um item têm em comum antes de decidir: quem decide, a revisão, a concessão e a governança
/// que decide o item. Sem nenhuma permissão da revisão a recusa é 403; com a da outra governança, é de item. A identidade
/// incompleta responde antes de tudo.
/// </summary>
/// <remarks>
/// A ordem de travas é única: a revisão, o papel de Representante do controlador, a pessoa do beneficiário, quando a
/// concessão é de perfil de governança, e a concessão. A condição de Representante do beneficiário é lida depois delas,
/// no instante da decisão, e não muda até o fim da transação: todo caminho que concede ou revoga a concessão de
/// Representante toma a mesma trava do papel (ADR-0162). Quem só consulta as revisões é recusado antes de qualquer trava.
/// </remarks>
public sealed class PreparoDaDecisaoDoItem
{
    private readonly IRevisaoDeAcessosRepository _revisoes;
    private readonly IConcessaoRepository _concessoes;
    private readonly IPapelDoRepresentante _papel;
    private readonly IIncompatibilidadeDosPapeis _papeis;
    private readonly TimeProvider _clock;

    public PreparoDaDecisaoDoItem(
        IRevisaoDeAcessosRepository revisoes,
        IConcessaoRepository concessoes,
        IPapelDoRepresentante papel,
        IIncompatibilidadeDosPapeis papeis,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(papel);
        ArgumentNullException.ThrowIfNull(papeis);
        ArgumentNullException.ThrowIfNull(clock);

        _revisoes = revisoes;
        _concessoes = concessoes;
        _papel = papel;
        _papeis = papeis;
        _clock = clock;
    }

    internal async Task<Result<DecisaoDoItem>> PrepararAsync(
        Guid revisaoId, Guid itemId, IVerificadorDeAcesso verificador, Identidade? decisor, CancellationToken cancellationToken)
    {
        AcessoDaRevisao? acesso = await AcessoDaRevisao.ConsultarAsync(verificador, revisaoId, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<DecisaoDoItem>.Failure(RevisaoHandlerErros.IdentidadeIncompleta());
        }

        if (decisor is null)
        {
            return await RecusarAsync(acesso, verificador, RevisaoHandlerErros.AutorNaoIdentificado(), cancellationToken).ConfigureAwait(false);
        }

        return acesso.DecideAlgum
            ? await CarregarAsync(new Pedido(revisaoId, itemId, verificador, acesso, decisor), cancellationToken).ConfigureAwait(false)
            : await RecusarAsync(acesso, verificador, RevisaoHandlerErros.SemPermissao(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<DecisaoDoItem>> RecusarAsync(
        AcessoDaRevisao acesso, IVerificadorDeAcesso verificador, DomainError recusa, CancellationToken cancellationToken)
    {
        await acesso.RegistrarDaDecisaoAsync(verificador, cancellationToken).ConfigureAwait(false);

        return Result<DecisaoDoItem>.Failure(recusa);
    }

    private async Task<Result<DecisaoDoItem>> CarregarAsync(Pedido pedido, CancellationToken cancellationToken)
    {
        RevisaoDeAcessos? revisao = await _revisoes.ObterParaAlterarAsync(pedido.RevisaoId, cancellationToken).ConfigureAwait(false);
        ItemDaRevisao? item = revisao?.Itens.FirstOrDefault(i => i.Id == pedido.ItemId);
        if (revisao is null || item is null)
        {
            DomainError recusa = revisao is null ? RevisaoHandlerErros.NaoEncontrada() : RevisaoHandlerErros.ItemNaoEncontrado();
            return await RecusarAsync(pedido.Acesso, pedido.Verificador, recusa, cancellationToken).ConfigureAwait(false);
        }

        Concessao? concessao = await TravarConcessaoAsync(item.ConcessaoId, cancellationToken).ConfigureAwait(false);

        return concessao is null
            ? await RecusarAsync(pedido.Acesso, pedido.Verificador, RevisaoHandlerErros.ItemNaoEncontrado(), cancellationToken).ConfigureAwait(false)
            : await ConferirQuemDecideAsync(pedido, new ItemCarregado(revisao, item, concessao), cancellationToken).ConfigureAwait(false);
    }

    // O beneficiário de uma concessão não muda: a leitura sem trava basta para saber que pessoa travar.
    private async Task<Concessao?> TravarConcessaoAsync(Guid concessaoId, CancellationToken cancellationToken)
    {
        await _papel.TravarAsync(cancellationToken).ConfigureAwait(false);
        if (await _concessoes.ObterComPerfilAsync(concessaoId, cancellationToken).ConfigureAwait(false) is not { } lida)
        {
            return null;
        }

        await _papeis.TravarBeneficiarioAsync(lida.Concessao, cancellationToken).ConfigureAwait(false);

        return await _concessoes.ObterParaAlterarAsync(concessaoId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<DecisaoDoItem>> ConferirQuemDecideAsync(Pedido pedido, ItemCarregado carregado, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        Identidade beneficiario = new(carregado.Concessao.BeneficiarioEmissor, carregado.Concessao.BeneficiarioSub);
        bool representante = await _papel.EVigenteAsync(beneficiario, agora, cancellationToken).ConfigureAwait(false);
        DecisorDoItem decisor = new(pedido.Decisor, representante);

        await AcessoDaRevisao.RegistrarAsync(pedido.Verificador, [pedido.Acesso.DecisaoDe(decisor.Como)], cancellationToken).ConfigureAwait(false);

        return pedido.Acesso.Decide(decisor.Como)
            ? Result<DecisaoDoItem>.Success(new DecisaoDoItem(carregado.Revisao, carregado.Item, carregado.Concessao, decisor, agora))
            : Result<DecisaoDoItem>.Failure(RevisaoHandlerErros.DecisorNaoDecideOItem());
    }

    private sealed record ItemCarregado(RevisaoDeAcessos Revisao, ItemDaRevisao Item, Concessao Concessao);

    private sealed record Pedido(Guid RevisaoId, Guid ItemId, IVerificadorDeAcesso Verificador, AcessoDaRevisao Acesso, Identidade Decisor);
}
