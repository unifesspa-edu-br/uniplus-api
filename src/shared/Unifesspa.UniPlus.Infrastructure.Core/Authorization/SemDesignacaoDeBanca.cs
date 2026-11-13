namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Porta das designações enquanto o modelo de designação não existe: ninguém está designado, e a recusa é a resposta
/// segura. Provisória: não prova que o acesso pela banca funciona, e a liberação dos atos da banca depende da
/// implementação real.
/// </summary>
internal sealed class SemDesignacaoDeBanca : IDesignacaoDeBanca
{
    public Task<bool> TemDesignacaoVigenteAsync(
        string emissor, string subject, Guid processoId, VinculosDaBanca banca, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> ExisteDesignacaoVigenteAsync(Guid bancaId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
