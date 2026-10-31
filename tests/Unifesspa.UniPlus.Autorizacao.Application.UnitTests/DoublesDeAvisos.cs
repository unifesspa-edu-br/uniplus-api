namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Dependências neutras do <see cref="AvisosDoPedido"/> para os testes dos handlers que só precisam delas montadas.</summary>
internal static class DoublesDeAvisos
{
    public static AvisosDoPedido Avisos() => new(new SemTitulares(), new SemUnidade(), new LinkDeTeste(), TimeProvider.System);

    internal sealed class PerfilQualquer : IPerfilRepository
    {
        public Task<Perfil?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Perfil?>(Perfil.Criar("Análise de atendimento", "Analisa os pedidos de atendimento.", ["selecao:processos:consultar"], new CatalogoLivre()).Value);

        public Task<IReadOnlyList<Perfil>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarAsync(Perfil entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Perfil entity) => throw new InvalidOperationException();
        public void Remover(Perfil entity) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarAtivosPaginadoAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<Perfil?> ObterParaEditarAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> NomeExisteAsync(string nome, Guid? exceto, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    internal sealed class SemUnidade : ILeitorDeUnidade
    {
        public Task<UnidadeView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<UnidadeView?>(null);
    }

    internal sealed class LinkDeTeste : ILinkDoPedido
    {
        public string Para(Guid pedidoId) => $"https://uniplus.test/configuracao/acessos/pedidos/{pedidoId}";
    }

    internal sealed class SemTitulares : IConcessaoRepository
    {
        public Task<IReadOnlyList<TitularComEmail>> ListarTitularesAtivosComEmailAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TitularComEmail>>([]);

        public Task<Concessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Concessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarAsync(Concessao entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Concessao entity) => throw new InvalidOperationException();
        public void Remover(Concessao entity) => throw new InvalidOperationException();
        public Task<Perfil?> ObterParaConcederAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ConcessaoComPerfil?> ObterComPerfilAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Concessao?> ObterNaoRevogadaIgualAsync(Identidade beneficiario, Guid perfilId, EscopoDaConcessao escopo, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> TemConcessaoAtivaDoPerfilAsync(Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            FiltroDeConcessoes filtro, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> ExisteNaoRevogadaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<int> ContarGestoresAtivosAsync(Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    internal sealed class CatalogoLivre : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => false;
    }
}
