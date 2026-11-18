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

using PaginaDeOfertas = (System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Configuracao.Domain.Entities.OfertaCurso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo, int? Total);

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
        await _apoio.GravarAsync(oferta);
        await _apoio.RemoverAsync(oferta.Id, AdminB);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        (await readCtx.OfertasCurso.AnyAsync(o => o.Id == oferta.Id)).Should().BeFalse("o soft-delete tira a oferta das leituras");
        OfertaCurso excluida = await readCtx.OfertasCurso.IgnoreQueryFilters().SingleAsync(o => o.Id == oferta.Id);

        excluida.IsDeleted.Should().BeTrue();
        excluida.DeletedBy.Should().Be(AdminB);
    }

    [Fact(DisplayName = "ReferenciadoPorOfertaCursoVivaAsync: true com oferta viva; false após soft-delete da oferta")]
    public async Task ReferenciadoPorOfertaCursoViva_ReflecteVida()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso oferta = NovaOferta(cursoId, localId, NovaUnidade());
        await _apoio.GravarAsync(oferta);

        (await CursoReferenciadoAsync(cursoId)).Should().BeTrue("há oferta viva referenciando o curso — a remoção do curso deve bloquear (CA-05)");
        (await CursoReferenciadoAsync(Guid.CreateVersion7())).Should().BeFalse("curso sem oferta não é referenciado");
        await _apoio.RemoverAsync(oferta.Id);

        (await CursoReferenciadoAsync(cursoId)).Should().BeFalse("o soft-delete da oferta libera o curso para remoção");
    }

    [Fact(DisplayName = "ListarPaginadoAsync filtra por cursoId e o recorte respeita o cursor bidirecional (itens + âncoras)")]
    public async Task ListarPaginado_FiltraPorCursoId_RespeitaCursorBidirecional()
    {
        (Guid cursoA, List<Guid> ordemA) = await SemearOfertasIntercaladasDeDoisCursosAsync();
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var repository = new OfertaCursoRepository(readCtx);

        PaginaDeOfertas pagina1 = await PaginaAsync(repository, cursoA, limit: 2);
        ConferirPrimeiraPagina(pagina1, ordemA, cursoA);
        PaginaDeOfertas pagina2 = await PaginaAsync(repository, cursoA, limit: 2, apos: pagina1.Proximo);
        ConferirSegundaPagina(pagina2, ordemA, cursoA);
        PaginaDeOfertas anterior = await PaginaAsync(repository, cursoA, limit: 2, apos: pagina2.Anterior, PaginationDirection.Prev);
        ConferirPaginaAnterior(anterior, ordemA, cursoA);
        (await PaginaAsync(repository, Guid.CreateVersion7(), limit: 50)).Itens.Should().BeEmpty("curso sem ofertas → recorte vazio");
    }

    [Fact(DisplayName = "O reader acompanha a oferta do grupo de área do ENEM do curso, ou de nulo quando o curso não o declara")]
    public async Task Reader_ExpoeGrupoAreaEnemDoCurso()
    {
        (OfertaCurso ofertaComGrupo, OfertaCurso ofertaSemGrupo) = await SemearOfertasDeCursoComESemGrupoAsync();
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new OfertaCursoReader(readCtx);

        (await reader.ObterPorIdAsync(ofertaComGrupo.Id))!.GrupoAreaEnem.Should().Be(GrupoSaudeEBiologicas);
        (await reader.ObterPorIdAsync(ofertaSemGrupo.Id))!.GrupoAreaEnem.Should().BeNull();

        IReadOnlyList<OfertaCursoView> vivas = await reader.ListarVivasAsync();
        vivas.Single(v => v.Id == ofertaComGrupo.Id).GrupoAreaEnem.Should().Be(GrupoSaudeEBiologicas);
        vivas.Single(v => v.Id == ofertaSemGrupo.Id).GrupoAreaEnem.Should().BeNull();
    }

    private static readonly GrupoAreaEnemView GrupoSaudeEBiologicas = new("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas");

    private async Task<bool> CursoReferenciadoAsync(Guid cursoId)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await new CursoRepository(ctx).ReferenciadoPorOfertaCursoVivaAsync(cursoId, CancellationToken.None);
    }

    // issue #755: o filtro entra no IQueryable ANTES do keyset — itens, prev e next devem ficar todos dentro do
    // recorte do curso A, ignorando o curso B. A criação é intercalada (A, B, A, B, A) para provar que o filtro
    // isola o curso; a ordem esperada é a do banco (uuid), derivada dele para não depender de Guid.CompareTo no cliente.
    private async Task<(Guid CursoA, List<Guid> OrdemDeA)> SemearOfertasIntercaladasDeDoisCursosAsync()
    {
        (Guid cursoA, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Guid cursoB = await _apoio.SemearCursoAsync();
        await _apoio.SemearOfertasAsync(
            (cursoA, localId), (cursoB, localId), (cursoA, localId), (cursoB, localId), (cursoA, localId));

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        List<Guid> ordemA = await readCtx.OfertasCurso.AsNoTracking()
            .Where(o => o.CursoId == cursoA)
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .ToListAsync();
        ordemA.Should().HaveCount(3);

        return (cursoA, ordemA);
    }

    private static Task<PaginaDeOfertas> PaginaAsync(
        OfertaCursoRepository repository,
        Guid cursoId,
        int limit,
        (string SortKey, Guid Id)? apos = null,
        PaginationDirection direcao = PaginationDirection.Next) =>
        repository.ListarPaginadoAsync(
            OrdenacaoPadraoDeOferta, busca: null, apos?.SortKey, apos?.Id, limit, direcao, cursoId, includeTotal: false, CancellationToken.None);

    // Dentro do recorte de um curso, nome e código são constantes e o Id é a única coluna que move o seek —
    // por isso a ordem esperada é a de Id.
    private static void ConferirPrimeiraPagina(PaginaDeOfertas pagina, List<Guid> ordemA, Guid cursoA)
    {
        pagina.Itens.Select(o => o.Id).Should().Equal(ordemA[0], ordemA[1]);
        pagina.Itens.Should().OnlyContain(o => o.CursoId == cursoA);
        pagina.Anterior.Should().BeNull();
        pagina.Proximo!.Value.Id.Should().Be(ordemA[1]);
    }

    // Página 2 (Next a partir da 2ª): a 3ª de A; sem próximo; com anterior.
    private static void ConferirSegundaPagina(PaginaDeOfertas pagina, List<Guid> ordemA, Guid cursoA)
    {
        pagina.Itens.Select(o => o.Id).Should().Equal(ordemA[2]);
        pagina.Itens.Should().OnlyContain(o => o.CursoId == cursoA);
        pagina.Proximo.Should().BeNull();
        pagina.Anterior!.Value.Id.Should().Be(ordemA[2]);
    }

    // Prev a partir da 3ª (limit 2): as 2 anteriores de A, já em ordem ascendente.
    private static void ConferirPaginaAnterior(PaginaDeOfertas pagina, List<Guid> ordemA, Guid cursoA)
    {
        pagina.Itens.Select(o => o.Id).Should().Equal(ordemA[0], ordemA[1]);
        pagina.Itens.Should().OnlyContain(o => o.CursoId == cursoA);
    }

    private async Task<(OfertaCurso ComGrupo, OfertaCurso SemGrupo)> SemearOfertasDeCursoComESemGrupoAsync()
    {
        Curso comGrupo = Curso.Criar(CodigoUnico(), "Medicina", "Bacharelado", "Graduação", GrupoCurso.SaudeEBiologicas).Value!;
        Curso semGrupo = Curso.Criar(CodigoUnico(), "Pedagogia", "Licenciatura", "Graduação", null).Value!;
        LocalOferta local = CampusSedeEmMaraba();
        OfertaCurso ofertaComGrupo = NovaOferta(comGrupo.Id, local.Id, NovaUnidade());
        OfertaCurso ofertaSemGrupo = NovaOferta(semGrupo.Id, local.Id, NovaUnidade());

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Cursos.AddRange(comGrupo, semGrupo);
        ctx.LocaisOferta.Add(local);
        ctx.OfertasCurso.AddRange(ofertaComGrupo, ofertaSemGrupo);
        await ctx.SaveChangesAsync();

        return (ofertaComGrupo, ofertaSemGrupo);
    }

    private static LocalOferta CampusSedeEmMaraba() =>
        LocalOferta.Criar(
            TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;
}
