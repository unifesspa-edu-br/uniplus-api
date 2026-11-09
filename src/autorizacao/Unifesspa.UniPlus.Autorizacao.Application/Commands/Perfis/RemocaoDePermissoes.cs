namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A retirada de permissões da versão em vigor, que vale na hora. A permissão de manter perfis é conferida na borda; a
/// regra vive no agregado, e aqui se traduz o pedido e se aplicam os efeitos e os avisos da troca.
/// </summary>
public sealed class RemocaoDePermissoes
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly EfeitosDoDesfechoDaVersao _efeitos;
    private readonly AutoriaDaOperacao _autoria;

    public RemocaoDePermissoes(OperacaoSobreOPerfil operacao, EfeitosDoDesfechoDaVersao efeitos, AutoriaDaOperacao autoria)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(efeitos);
        ArgumentNullException.ThrowIfNull(autoria);

        _operacao = operacao;
        _efeitos = efeitos;
        _autoria = autoria;
    }

    public async Task<GravacaoDaOperacao<PerfilDto>> RemoverAsync(
        Guid perfilId, RemoverPermissoesRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        if (await _autoria.DaVersaoAsync(perfilId, cancellationToken).ConfigureAwait(false) is not { } autoria)
        {
            return GravacaoDaOperacao<PerfilDto>.Sem(Result<PerfilDto>.Failure(PerfilHandlerErros.AutorNaoIdentificado()));
        }

        Result<PedidoDeRemocao> pedido = Pedir(dados, autoria);
        if (pedido.IsFailure)
        {
            return GravacaoDaOperacao<PerfilDto>.Sem(Result<PerfilDto>.ValidationFailure(pedido.Errors));
        }

        return await _operacao
            .ExecutarComAvisosAsync(perfilId, perfil => RemoverDoPerfilAsync(perfil, pedido.Value!, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    // A categoria é opcional, mas a informada tem de ser uma das conhecidas.
    private Result<PedidoDeRemocao> Pedir(RemoverPermissoesRequest dados, AutoriaDaVersao autoria)
    {
        CategoriaDaRemocao? categoria = CategoriaDaRemocaoCodigo.TentarLer(dados.Categoria);
        if (dados.Categoria is not null && categoria is null)
        {
            return Result<PedidoDeRemocao>.Failure(PerfilErros.RemocaoInvalida());
        }

        return Result<PedidoDeRemocao>.Success(new PedidoDeRemocao(
            [.. (dados.Permissoes ?? []).Distinct(StringComparer.Ordinal)],
            MotivosDaVersao.Normalizar(dados.Motivo),
            categoria,
            autoria,
            _autoria.Agora));
    }

    private async Task<DesfechoDaOperacao<PerfilDto>> RemoverDoPerfilAsync(
        Perfil perfil, PedidoDeRemocao pedido, CancellationToken cancellationToken) =>
        await _efeitos.AplicarAsync(perfil, perfil.Remover(pedido), pedido.Autoria.Pessoa, cancellationToken).ConfigureAwait(false);
}
