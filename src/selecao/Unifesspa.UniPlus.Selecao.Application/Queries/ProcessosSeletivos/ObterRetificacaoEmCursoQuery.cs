namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// A sessão editorial em curso do processo, ou <see langword="null"/> quando não há
/// nenhuma (ADR-0110 D3). É por aqui que o cliente relê o <c>ETag</c> depois de um
/// <b>412</b>.
/// </summary>
public sealed record ObterRetificacaoEmCursoQuery(Guid ProcessoSeletivoId)
    : IQuery<RetificacaoEmCursoDto?>;

public static class ObterRetificacaoEmCursoQueryHandler
{
    public static async Task<RetificacaoEmCursoDto?> Handle(
        ObterRetificacaoEmCursoQuery query,
        IRetificacaoEmCursoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        return await reader
            .ObterAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Leitura da sessão editorial — <c>AsNoTracking</c> e <b>sem lock</b>: consultar o
/// rascunho não é mutar o agregado, e tomar o <c>FOR UPDATE</c> da raiz para ler faria
/// duas consultas concorrentes se serializarem à toa.
/// </summary>
public interface IRetificacaoEmCursoReader
{
    Task<RetificacaoEmCursoDto?> ObterAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);
}
