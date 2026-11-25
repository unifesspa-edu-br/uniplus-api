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

/// <summary>
/// Verifica a ida e volta da cópia gravada e as restrições que dependem do PostgreSQL.
/// </summary>
/// <remarks>
/// A cópia da unidade ofertante já chega congelada no VO — a persistência não
/// precisa de Unidade real (o schema <c>organizacao</c> nem existe nesta fixture);
/// o congelamento com Unidade real é coberto nos endpoint tests.
/// </remarks>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class OfertaCursoPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDeOfertaCurso _apoio;

    public OfertaCursoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDeOfertaCurso(fixture);
    }

    [Fact(DisplayName = "Criar persiste os campos, a cópia da unidade ofertante faz ida e volta e a auditoria é carimbada")]
    public async Task Insert_Completa_PersisteComIdaEVoltaDoOwned()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        UnidadeOfertante unidade = NovaUnidade();
        OfertaCurso oferta = NovaOferta(cursoId, localId, unidade);
        await _apoio.GravarAsync(oferta);
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        OfertaCurso persistida = await readCtx.OfertasCurso.SingleAsync(o => o.Id == oferta.Id);

        ConferirIdentidadeDaOfertaPersistida(persistida, oferta);
        ConferirDeclaracaoDaOfertaPersistida(persistida);
        await ConferirLeituraPeloReaderAsync(new OfertaCursoReader(readCtx), oferta);
    }

    [Fact(DisplayName = "O índice único parcial recusa o mesmo código em duas ofertas vivas e o libera após a remoção")]
    public async Task Insert_CodigoRepetido_RecusaEntreVivasELiberaAposRemocao()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso primeira = NovaOferta(cursoId, localId, NovaUnidade());
        await _apoio.GravarAsync(primeira);

        await ConferirRecusaDoCodigoRepetidoAsync(OfertaComCodigo(cursoId, localId, primeira.Codigo));
        await _apoio.RemoverAsync(primeira.Id);

        await _apoio.GravarAsync(OfertaComCodigo(cursoId, localId, primeira.Codigo));
    }

    [Fact(DisplayName = "O CHECK do banco recusa código fora do formato em insert cru")]
    public async Task Insert_CodigoForaDoFormato_RecusadoPeloCheck()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso oferta = NovaOferta(cursoId, localId, NovaUnidade());
        typeof(OfertaCurso).GetProperty(nameof(OfertaCurso.Codigo))!.SetValue(oferta, "minusculo");

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.OfertasCurso.Add(oferta);

        DbUpdateException violacao = (await FluentActions.Awaiting(() => ctx.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>()).Which;
        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(violacao).Should().Be("ck_ofertas_de_curso_codigo_formato");
    }

    [Fact(DisplayName = "Campos opcionais nulos persistem nulos e reidratam como nulos")]
    public async Task Insert_OpcionaisNulos_PersisteNulos()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso oferta = OfertaComCodigo(cursoId, localId, $"OFERTA_{Guid.CreateVersion7():N}".ToUpperInvariant());
        await GravarComoAdministradoraAsync(oferta);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        OfertaCurso persistida = await readCtx.OfertasCurso.SingleAsync(o => o.Id == oferta.Id);

        ConferirOsPadroesEOsOpcionaisAusentes(persistida);
    }

    private async Task GravarComoAdministradoraAsync(OfertaCurso oferta)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.OfertasCurso.Add(oferta);
        await ctx.SaveChangesAsync();
    }

    private static void ConferirOsPadroesEOsOpcionaisAusentes(OfertaCurso persistida)
    {
        persistida.FormatoPedagogico.Should().Be(FormatoPedagogico.Presencial, "default conceitual quando o token está ausente");
        persistida.RegimeDeFuncionamento.Should().Be(RegimeDeFuncionamento.Extensivo);
        persistida.RegimeDeTurno.Should().Be(RegimeDeTurno.Regular);
        persistida.Turnos.Should().Equal(TurnoOferta.Matutino);
        persistida.CodigoEmec.Should().BeNull();
        persistida.CodigoNoSistemaAcademico.Should().BeNull();
        persistida.VagasAnuaisAutorizadas.Should().BeNull();
        persistida.BaseLegal.Should().BeNull();
        persistida.AtoAutorizacaoMec.Should().BeNull();
    }

    [Fact(DisplayName = "Atualizar a coleção de turnos após reidratação é detectado e persistido")]
    public async Task Atualizar_TrocaTurnosAposReidratacao_Persiste()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso oferta = NovaOferta(cursoId, localId, NovaUnidade());

        await PersistirOfertaAsync(oferta);
        await AtualizarTurnosAsync(oferta.Id);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        OfertaCurso persistida = await readCtx.OfertasCurso.SingleAsync(o => o.Id == oferta.Id);

        persistida.RegimeDeTurno.Should().Be(RegimeDeTurno.Regular);
        persistida.Turnos.Should().ContainSingle(
            "a troca do backing field da coleção primitiva precisa ser detectada pelo change tracker")
            .Which.Should().Be(TurnoOferta.Matutino);
    }

    private async Task PersistirOfertaAsync(OfertaCurso oferta)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.OfertasCurso.Add(oferta);
        await ctx.SaveChangesAsync();
    }

    private async Task AtualizarTurnosAsync(Guid ofertaId)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        OfertaCurso rastreada = await ctx.OfertasCurso.SingleAsync(o => o.Id == ofertaId);

        Result atualizacao = rastreada.Atualizar(new DeclaracaoDaOfertaDeCurso(
            "OFERTA_EDITADA", "PARFOR", "SEMIPRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"],
            "123456", "ENG-01", 40, "Decreto 6.755/2009", "Portaria MEC 9/2009"));
        atualizacao.IsSuccess.Should().BeTrue(atualizacao.Error?.Message);

        await ctx.SaveChangesAsync();
    }

    private static OfertaCurso OfertaComCodigo(Guid cursoId, Guid localId, string codigo) =>
        OfertaCurso.Criar(
            cursoId, localId, NovaUnidade(),
            new DeclaracaoDaOfertaDeCurso(codigo, "REGULAR", null, "EXTENSIVO", "REGULAR", ["MATUTINO"])).Value!;

    private async Task ConferirRecusaDoCodigoRepetidoAsync(OfertaCurso repetida)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.OfertasCurso.Add(repetida);

        DbUpdateException violacao = (await FluentActions.Awaiting(() => ctx.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>()).Which;

        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(violacao).Should().Be(RestricoesDoBanco.OfertaDeCursoCodigoAtivos);
    }

    private static void ConferirIdentidadeDaOfertaPersistida(OfertaCurso persistida, OfertaCurso oferta)
    {
        persistida.Codigo.Should().Be(oferta.Codigo);
        persistida.CursoId.Should().Be(oferta.CursoId);
        persistida.LocalOfertaId.Should().Be(oferta.LocalOfertaId);
        persistida.UnidadeOfertante.Should().Be(oferta.UnidadeOfertante, "o owned type reidrata o snapshot por igualdade estrutural");
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();
    }

    private static void ConferirDeclaracaoDaOfertaPersistida(OfertaCurso persistida)
    {
        persistida.ProgramaDeOferta.Should().Be(ProgramaDeOferta.Parfor);
        persistida.FormatoPedagogico.Should().Be(FormatoPedagogico.Semipresencial);
        persistida.RegimeDeFuncionamento.Should().Be(RegimeDeFuncionamento.Intensivo);
        persistida.RegimeDeTurno.Should().Be(RegimeDeTurno.Integral);
        persistida.Turnos.Should().Equal(TurnoOferta.Vespertino, TurnoOferta.Noturno);
        persistida.CodigoEmec.Should().Be("123456");
        persistida.CodigoNoSistemaAcademico.Should().Be("ENG-01");
        persistida.VagasAnuaisAutorizadas.Should().Be(40);
        persistida.BaseLegal.Should().Be("Decreto 6.755/2009");
        persistida.AtoAutorizacaoMec.Should().Be("Portaria MEC 9/2009");
    }

    private static async Task ConferirLeituraPeloReaderAsync(OfertaCursoReader reader, OfertaCurso oferta)
    {
        OfertaCursoView? view = await reader.ObterPorIdAsync(oferta.Id);
        view.Should().NotBeNull();
        ConferirViewDaOferta(view!, oferta);

        (await reader.ListarVivasAsync()).Should().Contain(v => v.Id == oferta.Id);
        (await reader.ObterVivaPorCodigoAsync($"  {oferta.Codigo}  "))!.Id.Should().Be(oferta.Id, "a busca descarta os espaços das pontas");
        (await reader.ObterVivaPorCodigoAsync(oferta.Codigo.ToLowerInvariant())).Should().BeNull("o código fora do formato não tem oferta");
    }

    private static void ConferirViewDaOferta(OfertaCursoView view, OfertaCurso oferta)
    {
        view.Codigo.Should().Be(oferta.Codigo);
        view.CursoId.Should().Be(oferta.CursoId);
        view.LocalOfertaId.Should().Be(oferta.LocalOfertaId);
        view.UnidadeOfertanteOrigemId.Should().Be(oferta.UnidadeOfertante.OrigemId);
        view.UnidadeOfertanteSigla.Should().Be(oferta.UnidadeOfertante.Sigla);
        view.ProgramaDeOferta.Should().Be("PARFOR");
        view.FormatoPedagogico.Should().Be("SEMIPRESENCIAL");
        view.RegimeDeFuncionamento.Should().Be("INTENSIVO");
        view.RegimeDeTurno.Should().Be("INTEGRAL");
        view.Turnos.Should().Equal("VESPERTINO", "NOTURNO");
    }
}
