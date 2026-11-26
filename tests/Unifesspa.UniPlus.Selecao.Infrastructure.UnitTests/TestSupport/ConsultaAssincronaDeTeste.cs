namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

using System.Linq.Expressions;

/// <summary>Consulta em memória que aceita as operações assíncronas do EF Core (<c>ToListAsync</c>), sem abrir conexão.</summary>
internal sealed class ConsultaAssincronaDeTeste<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public ConsultaAssincronaDeTeste(IEnumerable<T> itens)
        : base(itens)
    {
    }

    public ConsultaAssincronaDeTeste(Expression expressao)
        : base(expressao)
    {
    }

    IQueryProvider IQueryable.Provider => new Provedor(this);

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        ((IEnumerable<T>)this).ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken);

    private sealed class Provedor(IQueryProvider interno) : IQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => new ConsultaAssincronaDeTeste<T>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new ConsultaAssincronaDeTeste<TElement>(expression);

        public object? Execute(Expression expression) => interno.Execute(expression);

        public TResult Execute<TResult>(Expression expression) => interno.Execute<TResult>(expression);
    }
}
