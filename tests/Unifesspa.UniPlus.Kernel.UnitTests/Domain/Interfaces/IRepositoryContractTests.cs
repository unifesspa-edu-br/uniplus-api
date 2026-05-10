namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Interfaces;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

public sealed class IRepositoryContractTests
{
    [Fact(DisplayName = "IRepository<T>.ObterPorIdAsync devolve a entidade armazenada quando o Id existe")]
    public async Task ObterPorId_RetornaEntidadeArmazenada()
    {
        DummyRepository repo = new();
        EntidadeDeTeste entidade = new();
        await repo.AdicionarAsync(entidade);

        EntidadeDeTeste? lida = await repo.ObterPorIdAsync(entidade.Id);

        lida.Should().BeSameAs(entidade);
    }

    [Fact(DisplayName = "IRepository<T>.ObterPorIdAsync retorna null quando o Id não existe")]
    public async Task ObterPorId_RetornaNullQuandoNaoExiste()
    {
        DummyRepository repo = new();

        EntidadeDeTeste? lida = await repo.ObterPorIdAsync(Guid.NewGuid());

        lida.Should().BeNull();
    }

    [Fact(DisplayName = "IRepository<T>.ObterTodosAsync devolve coleção read-only com as entidades inseridas")]
    public async Task ObterTodos_RetornaTodasAsEntidades()
    {
        DummyRepository repo = new();
        EntidadeDeTeste a = new();
        EntidadeDeTeste b = new();
        await repo.AdicionarAsync(a);
        await repo.AdicionarAsync(b);

        IReadOnlyList<EntidadeDeTeste> todas = await repo.ObterTodosAsync();

        todas.Should().HaveCount(2)
            .And.Contain(a)
            .And.Contain(b);
    }

    [Fact(DisplayName = "IRepository<T>.Remover apaga a entidade do armazenamento")]
    public async Task Remover_ApagaEntidade()
    {
        DummyRepository repo = new();
        EntidadeDeTeste entidade = new();
        await repo.AdicionarAsync(entidade);

        repo.Remover(entidade);

        EntidadeDeTeste? lida = await repo.ObterPorIdAsync(entidade.Id);
        lida.Should().BeNull();
    }

    [Fact(DisplayName = "IRepository<T>.Atualizar marca a entidade como modificada (registro de chamada)")]
    public void Atualizar_RegistraChamada()
    {
        DummyRepository repo = new();
        EntidadeDeTeste entidade = new();

        repo.Atualizar(entidade);

        repo.Atualizados.Should().ContainSingle().Which.Should().BeSameAs(entidade);
    }

    private sealed class EntidadeDeTeste : EntityBase
    {
    }

    private sealed class DummyRepository : IRepository<EntidadeDeTeste>
    {
        private readonly Dictionary<Guid, EntidadeDeTeste> _store = [];
        private readonly List<EntidadeDeTeste> _atualizados = [];

        public IReadOnlyList<EntidadeDeTeste> Atualizados => _atualizados;

        public Task<EntidadeDeTeste?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.TryGetValue(id, out EntidadeDeTeste? e) ? e : null);

        public Task<IReadOnlyList<EntidadeDeTeste>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EntidadeDeTeste>>([.. _store.Values]);

        public Task AdicionarAsync(EntidadeDeTeste entity, CancellationToken cancellationToken = default)
        {
            _store[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Atualizar(EntidadeDeTeste entity) => _atualizados.Add(entity);

        public void Remover(EntidadeDeTeste entity) => _store.Remove(entity.Id);
    }
}
