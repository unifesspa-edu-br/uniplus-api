namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A desativação e a reativação do perfil, com motivo. Desativar não revoga concessão alguma nem cancela versão: só
/// impede pedidos e versões novos. Reativar exige outra pessoa. A permissão de manter perfis se confere na borda; a
/// regra vive no agregado, e o instante se lê sob a trava do perfil.
/// </summary>
public sealed class AtivacaoDoPerfil
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly AutoriaDaOperacao _autoria;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;

    public AtivacaoDoPerfil(OperacaoSobreOPerfil operacao, AutoriaDaOperacao autoria, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(catalogo);

        _operacao = operacao;
        _autoria = autoria;
        _catalogo = catalogo;
    }

    public Task<Result> DesativarAsync(Guid perfilId, string? motivo, CancellationToken cancellationToken) =>
        ExecutarAsync(perfilId, (autor, perfil, agora) => perfil.Desativar(autor, motivo, agora, _catalogo), cancellationToken);

    public Task<Result> ReativarAsync(Guid perfilId, string? motivo, CancellationToken cancellationToken) =>
        ExecutarAsync(perfilId, (autor, perfil, agora) => perfil.Reativar(autor, motivo, agora, _catalogo), cancellationToken);

    private async Task<Result> ExecutarAsync(
        Guid perfilId, Func<Identidade, Perfil, DateTimeOffset, Result> transicao, CancellationToken cancellationToken) =>
        _autoria.Autor is not { } autor
            ? Result.Failure(PerfilHandlerErros.AutorNaoIdentificado())
            : await _operacao
                .ExecutarAsync(perfilId, (perfil, contexto) => transicao(autor, perfil, contexto.Agora), cancellationToken)
                .ConfigureAwait(false);
}
