namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>Decifra e cifra os cursores com o mesmo codificador do host sob teste, para inspecionar e forjar continuações.</summary>
public sealed class CursoresDeTeste
{
    private readonly IServiceProvider _servicos;

    public CursoresDeTeste(IServiceProvider servicos)
    {
        ArgumentNullException.ThrowIfNull(servicos);

        _servicos = servicos;
    }

    public async Task<ConteudoDoCursor> DecifrarAsync(string link)
    {
        CursorPayload payload = await PayloadAsync(LinkDePaginacao.Cursor(link)).ConfigureAwait(false);

        return ConteudoDoCursor.De(payload);
    }

    /// <summary>Devolve o link com o cursor refeito: o payload do cursor original passa pela alteração e é cifrado de novo.</summary>
    public async Task<string> AlterarAsync(string link, Func<CursorPayload, CursorPayload> alteracao)
    {
        ArgumentNullException.ThrowIfNull(alteracao);

        CursorPayload original = await PayloadAsync(LinkDePaginacao.Cursor(link)).ConfigureAwait(false);
        await using AsyncServiceScope escopo = _servicos.CreateAsyncScope();
        string novo = await escopo.ServiceProvider.GetRequiredService<CursorEncoder>().EncodeAsync(alteracao(original)).ConfigureAwait(false);

        return LinkDePaginacao.ComCursor(link, novo);
    }

    private async Task<CursorPayload> PayloadAsync(string cursor)
    {
        await using AsyncServiceScope escopo = _servicos.CreateAsyncScope();
        CursorDecodeResult resultado = await escopo.ServiceProvider.GetRequiredService<CursorEncoder>().TryDecodeAsync(cursor).ConfigureAwait(false);

        return resultado.Payload ?? throw new InvalidOperationException("O cursor do link não pôde ser decifrado.");
    }
}
