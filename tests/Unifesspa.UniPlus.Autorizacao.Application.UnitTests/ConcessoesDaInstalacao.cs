namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// O repositório de concessões visto pela instalação: quantos gestores já estão ativos, o perfil de gestor a conceder e as
/// concessões adicionadas. Anota, na ordem, cada trava tomada, junto das travas de pessoa de <see cref="PapeisDeTeste"/>.
/// </summary>
internal sealed class ConcessoesDaInstalacao(int gestoresAtivos, List<string> travas) : IConcessaoRepository
{
    public List<Concessao> Adicionadas { get; } = [];

    public Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default)
    {
        travas.Add("gestores");
        return Task.CompletedTask;
    }

    public Task<int> ContarGestoresAtivosAsync(Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        Task.FromResult(gestoresAtivos);

    public Task<Perfil?> ObterParaConcederAsync(Guid perfilId, CancellationToken cancellationToken = default)
    {
        travas.Add("perfil");
        return Task.FromResult<Perfil?>(
            PerfisDeTeste.Vigente("Gestor de acessos", "Governa as concessões de acesso.", ["selecao:processos:consultar"], new DoublesDaInstalacao.CatalogoInstitucional()));
    }

    public Task AdicionarAsync(Concessao entity, CancellationToken cancellationToken = default)
    {
        Adicionadas.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TitularComEmail>> ListarTitularesAtivosComEmailAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();

    public Task<IReadOnlyList<Concessao>> ObterParaMigrarAsync(Guid perfilId, Guid versaoId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

    public Task<Concessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

    public Task<Concessao?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

    public Task<IReadOnlyList<Concessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();

    public void Atualizar(Concessao entity) => throw new InvalidOperationException();

    public void Remover(Concessao entity) => throw new InvalidOperationException();

    public Task<ConcessaoComPerfil?> ObterComPerfilAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

    public Task<Concessao?> ObterNaoRevogadaIgualAsync(Identidade beneficiario, Guid perfilId, EscopoDaConcessao escopo, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();

    public Task<bool> TemConcessaoAtivaDoPerfilAsync(Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();

    public Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        FiltroDeConcessoes filtro, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();

    public Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
}
