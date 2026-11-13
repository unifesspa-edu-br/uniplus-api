namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Agrega inscrições, pessoas e rascunhos sem materializar registros individuais.</summary>
public sealed class IndicadoresDoProcessoReader : IIndicadoresDoProcessoReader
{
    private readonly SelecaoDbContext _context;

    public IndicadoresDoProcessoReader(SelecaoDbContext context) => _context = context;

    public async Task<ContagensDoProcesso> LerAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COUNT(*) FILTER (WHERE situacao = 2)::bigint AS "Inscricoes",
                COUNT(DISTINCT cpf_indice) FILTER (WHERE situacao = 2)::bigint AS "Inscritos",
                COUNT(*) FILTER (WHERE situacao = 1)::bigint AS "Rascunhos"
            FROM selecao.inscricoes
            WHERE processo_seletivo_id = {0}
            """;

        IReadOnlyList<ContagensDoProcesso> resultado = await _context.Database
            .SqlQueryRaw<ContagensDoProcesso>(sql, processoSeletivoId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return resultado.Single();
    }
}
