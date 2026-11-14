namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Extensions;

/// <summary>Reader cross-módulo dos tipos de conceito ativos; não expõe tipos desativados.</summary>
internal sealed class TipoConceitoReader : ITipoConceitoReader
{
    private readonly UniPlusDbContext _dbContext;

    public TipoConceitoReader(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<TipoConceitoView?> ObterAtivoPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TipoConceito? tipo = await _dbContext.TiposConceito.AsNoTracking()
            .FirstOrDefaultAsync(tipo => tipo.Id == id && tipo.Ativo, cancellationToken).ConfigureAwait(false);
        return tipo is null ? null : ParaView(tipo);
    }

    public async Task<TipoConceitoView?> ObterAtivoPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // O cadastro guarda o código em NFC e a igualdade do banco é ordinal: a busca normaliza antes da
        // consulta, que o EF não traduziria para SQL. Texto que nem normaliza não pode ser código cadastrado.
        if (!TextoNormalizavel.TentarNormalizar(codigo.Trim(), out string codigoNormalizado))
        {
            return null;
        }

        TipoConceito? tipo = await _dbContext.TiposConceito.AsNoTracking()
            .FirstOrDefaultAsync(tipo => tipo.Codigo == codigoNormalizado && tipo.Ativo, cancellationToken).ConfigureAwait(false);
        return tipo is null ? null : ParaView(tipo);
    }

    private static TipoConceitoView ParaView(TipoConceito tipo) =>
        new(
            tipo.Id, tipo.Codigo, tipo.Nome, tipo.Descricao, NaturezasConceito.ParaContrato(tipo.Natureza),
            tipo.Natureza == NaturezaConceito.Numerico
                ? new EscalaNumericaView(tipo.EscalaMinima!.Value, tipo.EscalaMaxima!.Value, tipo.CasasDecimais!.Value)
                : null,
            [.. tipo.Valores.Select(valor => new ValorCategoricoView(valor.Codigo, valor.Rotulo, valor.ContaComoAprovacao))]);
}
