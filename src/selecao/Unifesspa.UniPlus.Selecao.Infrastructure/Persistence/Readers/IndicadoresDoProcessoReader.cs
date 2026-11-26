namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class IndicadoresDoProcessoReader : IIndicadoresDoProcessoReader
{
    private const string Consulta = """
        SELECT
            COUNT(*) FILTER (WHERE situacao = {1})::bigint AS "Inscricoes",
            COUNT(DISTINCT cpf_indice) FILTER (WHERE situacao = {1})::bigint AS "Inscritos",
            COUNT(*) FILTER (WHERE situacao = {2})::bigint AS "Rascunhos"
        FROM selecao.inscricoes
        WHERE processo_seletivo_id = {0}
        """;

    private readonly UniPlusDbContext _contexto;

    public IndicadoresDoProcessoReader(UniPlusDbContext contexto) => _contexto = contexto;

    public async Task<ContagensDoProcesso> LerAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ContagensDoProcesso> resultado = await _contexto.Database
            .SqlQueryRaw<ContagensDoProcesso>(
                Consulta,
                processoSeletivoId,
                SituacaoInscricao.Enviada.Codigo(),
                SituacaoInscricao.Rascunho.Codigo())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return resultado.Single();
    }
}
