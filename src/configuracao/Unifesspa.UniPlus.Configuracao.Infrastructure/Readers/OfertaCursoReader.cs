namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using System.Text;

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
/// Implementação de <see cref="IOfertaCursoReader"/> (ADR-0056): leitura direta
/// do banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — mesmo padrão do <c>ModalidadeReader</c>: o consumidor
/// congela por valor o que precisar (ADR-0061), dispensando releitura quente.
/// Ordena por <c>Id</c> ascendente.
/// </summary>
internal sealed class OfertaCursoReader : IOfertaCursoReader
{
    private readonly UniPlusDbContext _dbContext;

    public OfertaCursoReader(UniPlusDbContext dbContext)
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

        Dictionary<Guid, CursoDaOferta> cursos = await ObterCursosAsync(
            [.. entidades.Select(o => o.CursoId).Distinct()], cancellationToken).ConfigureAwait(false);

        return [.. entidades.Select(o => ParaView(o, cursos.GetValueOrDefault(o.CursoId)))];
    }

    public async Task<OfertaCursoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        OfertaCurso? entidade = await _dbContext.OfertasCurso
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : await ParaViewComCursoAsync(entidade, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OfertaCursoView> ParaViewComCursoAsync(OfertaCurso entidade, CancellationToken cancellationToken)
    {
        Dictionary<Guid, CursoDaOferta> cursos = await ObterCursosAsync(
            [entidade.CursoId], cancellationToken).ConfigureAwait(false);

        return ParaView(entidade, cursos.GetValueOrDefault(entidade.CursoId));
    }

    public async Task<OfertaCursoView?> ObterVivaPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Trim + NFC antes da query, como no leitor de modalidade: o EF não traduz normalização Unicode para SQL e o
        // cadastro guarda o código já normalizado. Código fora do formato não tem oferta viva, e a consulta é dispensável.
        Result<CodigoOfertaCurso> codigoResult = CodigoOfertaCurso.Criar(codigo);

        return codigoResult.IsFailure ? null : await ObterPorCodigoInformadoAsync(codigoResult.Value!.Valor, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OfertaCursoView?> ObterPorCodigoInformadoAsync(string codigo, CancellationToken cancellationToken)
    {
        OfertaCurso? entidade = await _dbContext.OfertasCurso
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Codigo == codigo, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : await ParaViewComCursoAsync(entidade, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// O nome, o grau e o grupo de área do ENEM de cada curso pedido. A oferta não navega até o
    /// curso, e o consumidor precisa deles para congelá-los junto da oferta; curso ausente da
    /// leitura deixa a oferta sem nome de curso e sem grupo, nunca fora da resposta.
    /// </summary>
    private Task<Dictionary<Guid, CursoDaOferta>> ObterCursosAsync(
        List<Guid> cursoIds,
        CancellationToken cancellationToken) =>
        _dbContext.Cursos
            .AsNoTracking()
            .Where(c => cursoIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Nome, c.Grau, c.GrupoAreaEnem })
            .ToDictionaryAsync(c => c.Id, c => new CursoDaOferta(c.Nome, c.Grau, c.GrupoAreaEnem), cancellationToken);

    private static OfertaCursoView ParaView(OfertaCurso o, CursoDaOferta? curso)
    {
        UnidadeOfertante unidade = o.UnidadeOfertante;
        GrupoCurso? grupoAreaEnem = curso?.GrupoAreaEnem;

        return new OfertaCursoView(
            o.Id, o.Codigo, o.CursoId, o.LocalOfertaId, unidade.OrigemId, unidade.Sigla, unidade.Nome, unidade.Tipo,
            o.ProgramaDeOferta.Codigo(), o.FormatoPedagogico.Codigo(),
            o.RegimeDeFuncionamento.Codigo(), o.RegimeDeTurno.Codigo(),
            [.. TurnosOferta.OrdenarCanonicamente(o.Turnos).Select(static turno => turno.Codigo())],
            o.CodigoEmec, o.CodigoNoSistemaAcademico, o.VagasAnuaisAutorizadas, o.BaseLegal, o.AtoAutorizacaoMec,
            grupoAreaEnem is null ? null : new GrupoAreaEnemView(grupoAreaEnem.Codigo, grupoAreaEnem.Rotulo),
            curso?.Nome ?? string.Empty, curso?.Grau ?? string.Empty);
    }

    private sealed record CursoDaOferta(string Nome, string Grau, GrupoCurso? GrupoAreaEnem);
}
