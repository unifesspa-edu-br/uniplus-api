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
            return Recusada(PerfilHandlerErros.AutorNaoIdentificado());
        }

        return CategoriaInvalida(dados)
            ? Recusada(PerfilErros.RemocaoInvalida())
            : await RemoverSobATravaAsync(perfilId, dados, autoria, cancellationToken).ConfigureAwait(false);
    }

    private static bool CategoriaInvalida(RemoverPermissoesRequest dados) =>
        dados.Categoria is not null && CategoriaDaRemocaoCodigo.TentarLer(dados.Categoria) is null;

    private static GravacaoDaOperacao<PerfilDto> Recusada(DomainError erro) =>
        GravacaoDaOperacao<PerfilDto>.Sem(Result<PerfilDto>.Failure(erro));

    private Task<GravacaoDaOperacao<PerfilDto>> RemoverSobATravaAsync(
        Guid perfilId, RemoverPermissoesRequest dados, AutoriaDaVersao autoria, CancellationToken cancellationToken) =>
        _operacao.ExecutarComAvisosAsync(
            perfilId,
            (perfil, contexto) => RemoverDoPerfilAsync(perfil, Pedido(dados, autoria, contexto.Agora), cancellationToken),
            cancellationToken);

    private static PedidoDeRemocao Pedido(RemoverPermissoesRequest dados, AutoriaDaVersao autoria, DateTimeOffset agora) =>
        new(
            [.. (dados.Permissoes ?? []).Distinct(StringComparer.Ordinal)],
            MotivosDaVersao.Normalizar(dados.Motivo),
            CategoriaDaRemocaoCodigo.TentarLer(dados.Categoria),
            autoria,
            agora);

    private async Task<DesfechoDaOperacao<PerfilDto>> RemoverDoPerfilAsync(
        Perfil perfil, PedidoDeRemocao pedido, CancellationToken cancellationToken) =>
        await _efeitos.AplicarAsync(perfil, perfil.Remover(pedido), pedido.Autoria.Pessoa, cancellationToken).ConfigureAwait(false);
}
