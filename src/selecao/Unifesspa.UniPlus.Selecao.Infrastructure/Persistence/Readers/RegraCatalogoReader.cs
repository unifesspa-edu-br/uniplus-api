namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Implementação de <see cref="IRegraCatalogoReader"/>: leitura direta do
/// <c>regras_do_catalogo</c> (<c>AsNoTracking</c>), sem cache — o catálogo é de
/// baixo volume (dezenas de regras), imutável por versão, e o congelamento por
/// valor no consumidor (<see cref="Domain.ValueObjects.ReferenciaRegra"/> +
/// snapshot, ADR-0061) dispensa releitura quente (mesmo padrão dos readers de
/// reference data do módulo Configuração).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI na registration de Infrastructure do módulo Seleção.")]
internal sealed class RegraCatalogoReader : IRegraCatalogoReader
{
    private readonly UniPlusDbContext _dbContext;

    public RegraCatalogoReader(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<RegraCatalogo?> ObterAsync(
        string codigo,
        string versao,
        CancellationToken cancellationToken = default)
    {
        // Nenhuma regra tem código ou versão nulos, nem com o caractere nulo, que não cabe em
        // coluna de texto do Postgres e faria o parâmetro ser recusado com erro: a consulta
        // nem sai. Há chamador que repassa a versão do payload sem exigi-la.
        if (codigo?.Contains('\0', StringComparison.Ordinal) != false || versao?.Contains('\0', StringComparison.Ordinal) != false)
        {
            return null;
        }

        return await _dbContext.RolDeRegras
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Codigo == codigo && r.Versao == versao, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<RegraCatalogo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        TipoRegra? tipo,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        List<RegraCatalogo> ordenadas = await ListarOrdenadasAsync(tipo, cancellationToken).ConfigureAwait(false);
        int posicao = afterId is { } id ? ordenadas.FindIndex(r => r.Id == id) : 0;
        if (posicao < 0)
        {
            return ([], null, null);
        }

        (List<RegraCatalogo> pagina, int inicio) = RecortarPagina(ordenadas, afterId, posicao, limit, direction);
        return CursoresDaPagina(ordenadas, pagina, inicio);
    }

    private async Task<List<RegraCatalogo>> ListarOrdenadasAsync(TipoRegra? tipo, CancellationToken cancellationToken)
    {
        IQueryable<RegraCatalogo> filtrada = _dbContext.RolDeRegras.AsNoTracking();
        if (tipo is { } valor)
        {
            filtrada = filtrada.Where(r => r.Tipo == valor);
        }

        // A comparação ordinal mantém a mesma ordem independentemente da collation do banco.
        List<RegraCatalogo> ordenadas = await filtrada.ToListAsync(cancellationToken).ConfigureAwait(false);
        ordenadas.Sort(static (a, b) => Comparar(ChaveDe(a), ChaveDe(b)));
        return ordenadas;
    }

    private static (List<RegraCatalogo> Pagina, int Inicio) RecortarPagina(
        List<RegraCatalogo> ordenadas, Guid? afterId, int posicao, int limit, PaginationDirection direction)
    {
        if (afterId is null)
        {
            return ([.. ordenadas.Take(limit)], 0);
        }

        if (direction == PaginationDirection.Prev)
        {
            int quantos = Math.Min(limit, posicao);
            int inicioAnterior = posicao - quantos;
            return ([.. ordenadas.Skip(inicioAnterior).Take(quantos)], inicioAnterior);
        }

        int inicio = posicao + 1;
        return ([.. ordenadas.Skip(inicio).Take(limit)], inicio);
    }

    private static (IReadOnlyList<RegraCatalogo>, Guid?, Guid?) CursoresDaPagina(
        List<RegraCatalogo> ordenadas, List<RegraCatalogo> pagina, int inicio)
    {
        if (pagina.Count == 0)
        {
            return ([], null, null);
        }

        bool haAnterior = inicio > 0;
        bool haProxima = inicio + pagina.Count < ordenadas.Count;
        return (pagina, haAnterior ? pagina[0].Id : null, haProxima ? pagina[^1].Id : null);
    }

    /// <summary>
    /// A chave de ordenação do catálogo: tipo (no código de wire que está na coluna), código e
    /// versão.
    /// </summary>
    /// <remarks>
    /// O tipo entra pelo código canônico, e não pelo valor do enum: ordenar pelo enum usaria a
    /// posição em que os membros foram declarados em C#, que não tem relação nenhuma com a
    /// ordem alfabética dos códigos que a API devolve e aceita como filtro.
    /// </remarks>
    private sealed record ChaveDeOrdem(string Tipo, string Codigo, string Versao);

    private static ChaveDeOrdem ChaveDe(RegraCatalogo regra) =>
        new(regra.Tipo.Codigo(), regra.Codigo, regra.Versao);

    /// <summary>
    /// Comparação lexicográfica da tripla, por ponto de código
    /// (<see cref="StringComparer.Ordinal"/>). É a mesma comparação que ordena a lista e que
    /// posiciona o cursor — se as duas divergissem, itens de fronteira sumiriam ou se
    /// repetiriam ao virar a página.
    /// </summary>
    private static int Comparar(ChaveDeOrdem a, ChaveDeOrdem b)
    {
        int porTipo = StringComparer.Ordinal.Compare(a.Tipo, b.Tipo);
        if (porTipo != 0)
        {
            return porTipo;
        }

        int porCodigo = StringComparer.Ordinal.Compare(a.Codigo, b.Codigo);
        return porCodigo != 0 ? porCodigo : StringComparer.Ordinal.Compare(a.Versao, b.Versao);
    }

    public async Task<IReadOnlyList<RegraCatalogo>> ListarPorTipoAsync(
        TipoRegra tipo,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RolDeRegras
            .AsNoTracking()
            .Where(r => r.Tipo == tipo)
            .OrderBy(r => r.Codigo)
            .ThenBy(r => r.Versao)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
