namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso.ApoioDaPersistenciaDeOfertaCurso;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class OfertaCursoLeituraPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDeOfertaCurso _apoio;

    public OfertaCursoLeituraPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDeOfertaCurso(fixture);
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e tira a oferta das leituras via query filter")]
    public async Task SoftDelete_PreservaTrilha()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso oferta = NovaOferta(cursoId, localId, NovaUnidade());

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.OfertasCurso.Add(oferta);
            await ctx.SaveChangesAsync();
        }

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            OfertaCurso tracked = await ctx.OfertasCurso.SingleAsync(o => o.Id == oferta.Id);
            ctx.OfertasCurso.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        (await readCtx.OfertasCurso.AnyAsync(o => o.Id == oferta.Id))
            .Should().BeFalse("o soft-delete tira a oferta das leituras");

        OfertaCurso excluida = await readCtx.OfertasCurso
            .IgnoreQueryFilters().SingleAsync(o => o.Id == oferta.Id);
        excluida.IsDeleted.Should().BeTrue();
        excluida.DeletedBy.Should().Be(AdminB);
    }

    [Fact(DisplayName = "ReferenciadoPorOfertaCursoVivaAsync: true com oferta viva; false após soft-delete da oferta")]
    public async Task ReferenciadoPorOfertaCursoViva_ReflecteVida()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso oferta = NovaOferta(cursoId, localId, NovaUnidade());

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.OfertasCurso.Add(oferta);
            await ctx.SaveChangesAsync();
        }

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            var cursoRepository = new CursoRepository(ctx);
            (await cursoRepository.ReferenciadoPorOfertaCursoVivaAsync(cursoId, CancellationToken.None))
                .Should().BeTrue("há oferta viva referenciando o curso — a remoção do curso deve bloquear (CA-05)");
            (await cursoRepository.ReferenciadoPorOfertaCursoVivaAsync(Guid.CreateVersion7(), CancellationToken.None))
                .Should().BeFalse("curso sem oferta não é referenciado");
        }

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            OfertaCurso tracked = await ctx.OfertasCurso.SingleAsync(o => o.Id == oferta.Id);
            ctx.OfertasCurso.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var repositoryAposRemocao = new CursoRepository(readCtx);
        (await repositoryAposRemocao.ReferenciadoPorOfertaCursoVivaAsync(cursoId, CancellationToken.None))
            .Should().BeFalse("o soft-delete da oferta libera o curso para remoção");
    }

    [Fact(DisplayName = "ListarPaginadoAsync filtra por cursoId e o recorte respeita o cursor bidirecional (itens + âncoras)")]
    public async Task ListarPaginado_FiltraPorCursoId_RespeitaCursorBidirecional()
    {
        // issue #755: o filtro entra no IQueryable ANTES do keyset — itens, prev e
        // next devem ficar todos dentro do recorte do curso A, ignorando o curso B.
        (Guid cursoA, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Guid cursoB = await _apoio.SemearCursoAsync();

        // Intercala a criação (A, B, A, B, A) para provar que o filtro isola o curso.
        await _apoio.SemearOfertasAsync(
            (cursoA, localId), (cursoB, localId), (cursoA, localId), (cursoB, localId), (cursoA, localId));

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var repository = new OfertaCursoRepository(readCtx);

        // Ordem canônica do banco (uuid) das ofertas de A — base para fatiar as páginas.
        // Derivada do próprio banco: evita depender da semântica de Guid.CompareTo no cliente.
        List<Guid> ordemA = await readCtx.OfertasCurso.AsNoTracking()
            .Where(o => o.CursoId == cursoA)
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .ToListAsync();
        ordemA.Should().HaveCount(3);

        // Dentro do recorte de um curso, nome e código são constantes e o Id é a
        // única coluna que move o seek — por isso a ordem esperada é a de Id.
        (IReadOnlyList<OfertaCurso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo, int? Total) pagina1 =
            await repository.ListarPaginadoAsync(
                OrdenacaoPadraoDeOferta, busca: null, afterSortKey: null, afterId: null, limit: 2, PaginationDirection.Next, cursoA, includeTotal: false, CancellationToken.None);
        pagina1.Itens.Select(o => o.Id).Should().Equal(ordemA[0], ordemA[1]);
        pagina1.Itens.Should().OnlyContain(o => o.CursoId == cursoA);
        pagina1.Anterior.Should().BeNull();
        pagina1.Proximo!.Value.Id.Should().Be(ordemA[1]);

        // Página 2 (Next a partir da 2ª): a 3ª de A; sem próximo; com anterior.
        (IReadOnlyList<OfertaCurso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo, int? Total) pagina2 =
            await repository.ListarPaginadoAsync(
                OrdenacaoPadraoDeOferta, busca: null, pagina1.Proximo!.Value.SortKey, ordemA[1], limit: 2, PaginationDirection.Next, cursoA, includeTotal: false, CancellationToken.None);
        pagina2.Itens.Select(o => o.Id).Should().Equal(ordemA[2]);
        pagina2.Itens.Should().OnlyContain(o => o.CursoId == cursoA);
        pagina2.Proximo.Should().BeNull();
        pagina2.Anterior!.Value.Id.Should().Be(ordemA[2]);

        // Prev a partir da 3ª (limit 2): as 2 anteriores de A, já em ordem ascendente.
        (IReadOnlyList<OfertaCurso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo, int? Total) anterior =
            await repository.ListarPaginadoAsync(
                OrdenacaoPadraoDeOferta, busca: null, pagina2.Anterior!.Value.SortKey, ordemA[2], limit: 2, PaginationDirection.Prev, cursoA, includeTotal: false, CancellationToken.None);
        anterior.Itens.Select(o => o.Id).Should().Equal(ordemA[0], ordemA[1]);
        anterior.Itens.Should().OnlyContain(o => o.CursoId == cursoA);

        // Curso sem ofertas → recorte vazio.
        (IReadOnlyList<OfertaCurso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo, int? Total) vazio =
            await repository.ListarPaginadoAsync(
                OrdenacaoPadraoDeOferta, busca: null, afterSortKey: null, afterId: null, limit: 50, PaginationDirection.Next, Guid.CreateVersion7(), includeTotal: false, CancellationToken.None);
        vazio.Itens.Should().BeEmpty();
    }

    [Fact(DisplayName = "O reader acompanha a oferta do grupo de área do ENEM do curso, ou de nulo quando o curso não o declara")]
    public async Task Reader_ExpoeGrupoAreaEnemDoCurso()
    {
        Curso comGrupo = Curso.Criar(CodigoUnico(), "Medicina", "Bacharelado", "Graduação", GrupoCurso.SaudeEBiologicas).Value!;
        Curso semGrupo = Curso.Criar(CodigoUnico(), "Pedagogia", "Licenciatura", "Graduação", null).Value!;
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;
        OfertaCurso ofertaComGrupo = NovaOferta(comGrupo.Id, local.Id, NovaUnidade());
        OfertaCurso ofertaSemGrupo = NovaOferta(semGrupo.Id, local.Id, NovaUnidade());

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Cursos.AddRange(comGrupo, semGrupo);
            ctx.LocaisOferta.Add(local);
            ctx.OfertasCurso.AddRange(ofertaComGrupo, ofertaSemGrupo);
            await ctx.SaveChangesAsync();
        }

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new OfertaCursoReader(readCtx);

        (await reader.ObterPorIdAsync(ofertaComGrupo.Id))!.GrupoAreaEnem.Should().Be(new GrupoAreaEnemView("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas"));
        (await reader.ObterPorIdAsync(ofertaSemGrupo.Id))!.GrupoAreaEnem.Should().BeNull();

        IReadOnlyList<OfertaCursoView> vivas = await reader.ListarVivasAsync();
        vivas.Single(v => v.Id == ofertaComGrupo.Id).GrupoAreaEnem.Should().Be(new GrupoAreaEnemView("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas"));
        vivas.Single(v => v.Id == ofertaSemGrupo.Id).GrupoAreaEnem.Should().BeNull();
    }
}
