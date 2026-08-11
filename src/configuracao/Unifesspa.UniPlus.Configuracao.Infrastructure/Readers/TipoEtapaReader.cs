namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using System.Text;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>Reader cross-módulo dos tipos ativos; não expõe itens desativados.</summary>
internal sealed class TipoEtapaReader : ITipoEtapaReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TipoEtapaReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TipoEtapaView>> ListarAtivosAsync(CancellationToken cancellationToken = default)
    {
        List<TipoEtapa> tipos = await _dbContext.TiposEtapa.AsNoTracking()
            .Where(tipo => tipo.Ativo).OrderBy(tipo => tipo.Codigo)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. tipos.Select(ParaView)];
    }

    public async Task<TipoEtapaView?> ObterAtivoPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TipoEtapa? tipo = await _dbContext.TiposEtapa.AsNoTracking()
            .FirstOrDefaultAsync(tipo => tipo.Id == id && tipo.Ativo, cancellationToken).ConfigureAwait(false);
        return tipo is null ? null : ParaView(tipo);
    }

    public async Task<TipoEtapaView?> ObterAtivoPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // NFC calculado ANTES da query (não dentro do lambda, que o EF traduziria para SQL sem
        // suporte a normalização Unicode): o cadastro persiste Codigo já em NFC
        // (TipoEtapa.Criar), então um valor de busca em outra forma Unicode nunca bateria por
        // igualdade ordinal sem essa normalização — mesmo comparando o "mesmo" texto.
        string codigoNormalizado = codigo.Trim().Normalize(NormalizationForm.FormC);
        TipoEtapa? tipo = await _dbContext.TiposEtapa.AsNoTracking()
            .FirstOrDefaultAsync(tipo => tipo.Codigo == codigoNormalizado && tipo.Ativo, cancellationToken).ConfigureAwait(false);
        return tipo is null ? null : ParaView(tipo);
    }

    private static TipoEtapaView ParaView(TipoEtapa tipo) =>
        new(tipo.Id, tipo.Codigo, tipo.Nome, tipo.Descricao);
}
