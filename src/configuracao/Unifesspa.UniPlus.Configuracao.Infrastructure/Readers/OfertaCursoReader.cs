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

        return [.. entidades.Select(ParaView)];
    }

    public async Task<OfertaCursoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        OfertaCurso? entidade = await _dbContext.OfertasCurso
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static OfertaCursoView ParaView(OfertaCurso o)
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
            o.Turno is { } turno ? TurnosOferta.ParaTokenCanonico(turno) : null,
            o.EMecCodigo,
            o.CodigoSga,
            o.VagasAnuaisAutorizadas,
            o.BaseLegal,
            o.AtoAutorizacaoMec);
    }
}
