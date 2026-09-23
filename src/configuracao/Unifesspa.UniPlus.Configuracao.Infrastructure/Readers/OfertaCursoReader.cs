namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IOfertaCursoReader"/> (ADR-0056): leitura direta
/// do banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — mesmo padrão do <c>ModalidadeReader</c>: o consumidor
/// congela por valor o que precisar (ADR-0061), dispensando releitura quente.
/// Ordena por <c>Id</c> ascendente.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class OfertaCursoReader : IOfertaCursoReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public OfertaCursoReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OfertaCursoView>> ListarVivasAsync(
        CancellationToken cancellationToken = default)
    {
        List<OfertaCurso> entidades = await _dbContext.OfertasCurso
            .AsNoTracking()
            .OrderBy(o => o.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, GrupoCurso?> gruposPorCurso = await ObterGruposDosCursosAsync(
            [.. entidades.Select(o => o.CursoId).Distinct()], cancellationToken).ConfigureAwait(false);

        return [.. entidades.Select(o => ParaView(o, gruposPorCurso.GetValueOrDefault(o.CursoId)))];
    }

    public async Task<OfertaCursoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        OfertaCurso? entidade = await _dbContext.OfertasCurso
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entidade is null)
        {
            return null;
        }

        Dictionary<Guid, GrupoCurso?> gruposPorCurso = await ObterGruposDosCursosAsync(
            [entidade.CursoId], cancellationToken).ConfigureAwait(false);

        return ParaView(entidade, gruposPorCurso.GetValueOrDefault(entidade.CursoId));
    }

    /// <summary>
    /// O grupo de área do ENEM de cada curso pedido. A oferta não navega até o curso, e
    /// o consumidor precisa do grupo para congelá-lo junto da oferta; curso ausente da
    /// leitura deixa a oferta sem grupo, nunca fora da resposta.
    /// </summary>
    private Task<Dictionary<Guid, GrupoCurso?>> ObterGruposDosCursosAsync(
        List<Guid> cursoIds,
        CancellationToken cancellationToken) =>
        _dbContext.Cursos
            .AsNoTracking()
            .Where(c => cursoIds.Contains(c.Id))
            .Select(c => new { c.Id, c.GrupoAreaEnem })
            .ToDictionaryAsync(c => c.Id, c => c.GrupoAreaEnem, cancellationToken);

    private static OfertaCursoView ParaView(OfertaCurso o, GrupoCurso? grupoAreaEnem)
    {
        UnidadeOfertante unidade = o.UnidadeOfertante;

        return new OfertaCursoView(
            o.Id,
            o.CursoId,
            o.LocalOfertaId,
            unidade.OrigemId,
            unidade.Sigla,
            unidade.Nome,
            unidade.Tipo,
            ProgramasDeOferta.ParaTokenCanonico(o.ProgramaDeOferta),
            FormatosPedagogicos.ParaTokenCanonico(o.FormatoPedagogico),
            RegimesDeFuncionamento.ParaTokenCanonico(o.RegimeDeFuncionamento),
            RegimesDeTurno.ParaTokenCanonico(o.RegimeDeTurno),
            [.. TurnosOferta.OrdenarCanonicamente(o.Turnos).Select(TurnosOferta.ParaTokenCanonico)],
            o.EMecCodigo,
            o.CodigoSga,
            o.VagasAnuaisAutorizadas,
            o.BaseLegal,
            o.AtoAutorizacaoMec,
            grupoAreaEnem is null ? null : new GrupoAreaEnemView(grupoAreaEnem.Codigo, grupoAreaEnem.Rotulo));
    }
}
