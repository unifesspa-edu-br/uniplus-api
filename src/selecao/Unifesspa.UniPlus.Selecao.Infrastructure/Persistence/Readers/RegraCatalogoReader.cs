namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Implementação de <see cref="IRegraCatalogoReader"/>: leitura direta do
/// <c>rol_de_regras</c> (<c>AsNoTracking</c>), sem cache — o catálogo é de
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
    private readonly SelecaoDbContext _dbContext;

    public RegraCatalogoReader(SelecaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<RegraCatalogo?> ObterAsync(
        string codigo,
        string versao,
        CancellationToken cancellationToken = default)
    {
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
        IQueryable<RegraCatalogo> filtrada = _dbContext.RolDeRegras.AsNoTracking();
        if (tipo is { } valor)
        {
            filtrada = filtrada.Where(r => r.Tipo == valor);
        }

        // A janela é recortada em memória, sobre o conjunto já filtrado por tipo. Duas razões,
        // e nenhuma delas é conveniência:
        //
        // A ordem do contrato é lexicográfica sobre a tripla (tipo, código, versão), e nem
        // `string.Compare` nem `CompareTo` são traduzidos pelo provider — o keyset em SQL
        // exigiria SQL bruto para uma comparação que o Postgres faria sob a collation da
        // instalação, isto é, uma ordem que muda de ambiente para ambiente.
        //
        // `StringComparer.Ordinal` dá a ordem por ponto de código, igual em qualquer lugar. Para
        // um catálogo seed-governado de dezenas de entradas — que este mesmo reader já lê
        // inteiro em ListarPorTipoAsync —, carregar e ordenar aqui custa menos do que uma
        // paginação cuja ordem depende de como o banco foi criado.
        List<RegraCatalogo> ordenadas = await filtrada
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        ordenadas.Sort(static (a, b) => Comparar(ChaveDe(a), ChaveDe(b)));

        int posicaoDaAncora = 0;
        if (afterId is { } id)
        {
            int indice = ordenadas.FindIndex(r => r.Id == id);

            // Uma âncora que não existe mais — ou que o filtro atual exclui — não pode ser lida
            // como "começar do início": devolveria a primeira página sob um cursor que o cliente
            // acredita apontar para o meio, e ele repetiria itens já vistos sem perceber.
            if (indice < 0)
            {
                return ([], null, null);
            }

            posicaoDaAncora = indice;
        }

        bool paraTras = direction == PaginationDirection.Prev;

        List<RegraCatalogo> janela;
        int inicioDaJanela;
        if (afterId is null)
        {
            inicioDaJanela = 0;
            janela = [.. ordenadas.Take(limit)];
        }
        else if (paraTras)
        {
            // Exclusivo da âncora e ancorado à esquerda: a página anterior termina imediatamente
            // antes dela, então é do fim para trás que se conta.
            int quantos = Math.Min(limit, posicaoDaAncora);
            inicioDaJanela = posicaoDaAncora - quantos;
            janela = [.. ordenadas.Skip(inicioDaJanela).Take(quantos)];
        }
        else
        {
            inicioDaJanela = posicaoDaAncora + 1;
            janela = [.. ordenadas.Skip(inicioDaJanela).Take(limit)];
        }

        if (janela.Count == 0)
        {
            return ([], null, null);
        }

        // As âncoras dos dois lados saem da posição real da janela no conjunto ordenado, e não
        // de uma sonda `n+1`: só há lado quando existe pelo menos um item além da fronteira.
        bool haAnterior = inicioDaJanela > 0;
        bool haProxima = inicioDaJanela + janela.Count < ordenadas.Count;

        return (janela, haAnterior ? janela[0].Id : null, haProxima ? janela[^1].Id : null);
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
        new(regra.Tipo.ToCodigo(), regra.Codigo, regra.Versao);

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
