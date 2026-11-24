namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Implementação de <see cref="ITipoDocumentoReader"/> (ADR-0056): leitura direta
/// do banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — o cadastro é de baixo volume e baixa frequência de
/// leitura viva; o congelamento por valor no consumidor (ADR-0061) dispensa
/// releitura quente (mesmo padrão do <c>PesoAreaEnemReader</c>).
/// </summary>
internal sealed class TipoDocumentoReader : ITipoDocumentoReader
{
    private readonly UniPlusDbContext _dbContext;

    public TipoDocumentoReader(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TipoDocumentoView>> ListarVivosAsync(
        CancellationToken cancellationToken = default)
    {
        List<TipoDocumento> entidades = await _dbContext.TiposDocumento
            .AsNoTracking()
            .OrderBy(t => t.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<TipoDocumentoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        TipoDocumento? entidade = await _dbContext.TiposDocumento
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    public async Task<IReadOnlyList<TipoDocumentoView>> ListarVivosPorCodigosAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        List<CodigoTipoDocumento> codigosDoLote = CodigosValidos(codigos);
        List<TipoDocumento> entidades = await _dbContext.TiposDocumento
            .AsNoTracking()
            .Where(tipo => codigosDoLote.Contains(tipo.Codigo))
            .OrderBy(tipo => tipo.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    // Valores inválidos não podem estar gravados e o conversor os recusaria na consulta.
    private static List<CodigoTipoDocumento> CodigosValidos(IReadOnlyCollection<string> codigos) =>
    [.. codigos
        .Where(CodigoTipoDocumento.EhValido)
        .Select(codigo => CodigoTipoDocumento.Criar(codigo).Value!)
        .Distinct()];

    // O código tem formato fechado (CodigoTipoDocumento): só letras maiúsculas, dígitos e sublinhado, o que dispensa
    // normalização Unicode. Um código fora do formato não pode estar gravado, então a busca responde "não encontrado" sem
    // consultar o banco, em vez de estourar no conversor ao traduzir um valor que o value object recusaria.
    public async Task<TipoDocumentoView?> ObterVivoPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        if (CodigoTipoDocumento.Criar(codigo).Value is not { } codigoDoTipo)
        {
            return null;
        }

        TipoDocumento? entidade = await _dbContext.TiposDocumento
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Codigo == codigoDoTipo, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static TipoDocumentoView ParaView(TipoDocumento t) =>
        new(
            t.Id,
            t.Codigo.Valor,
            t.Nome,
            t.Categoria,
            t.ClassificacaoProtecao,
            t.FinalidadeTratamento,
            t.HipoteseLegal.Codigo(),
            t.Aprovacao.EstaAprovada);
}
