namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Governance.Contracts;

internal sealed class LeitorDeUnidade : ILeitorDeUnidade
{
    private readonly IUnidadeReader _reader;

    public LeitorDeUnidade(IUnidadeReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _reader = reader;
    }

    public Task<UnidadeView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _reader.ObterPorIdAsync(id, cancellationToken);
}
