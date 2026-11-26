namespace Unifesspa.UniPlus.Ingresso.IntegrationTests.Convocacoes;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.Enums;
using Unifesspa.UniPlus.Ingresso.Domain.ValueObjects;
using Unifesspa.UniPlus.Ingresso.TestSupport.Cenarios;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;

[Collection(IngressoComPostgres.Name)]
public sealed class ConvocacaoPersistenceTests(MonolitoPostgresFixture fixture)
{
    private static readonly TimeProvider Relogio = new RelogioFixo();

    [Fact]
    public async Task Convocacoes_MesmaOfertaRecebida_PersistemCopiasIndependentes()
    {
        Chamada chamada = NovaChamada();
        CopiaDaOfertaDeCurso oferta = new CopiaDaOfertaDeCursoDeTeste().Construir();
        Convocacao primeira = NovaConvocacao(chamada.Id, oferta, 1, Random.Shared.Next(1, 49999));
        Convocacao segunda = NovaConvocacao(chamada.Id, oferta, 2, Random.Shared.Next(50000, 99998));
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        contexto.Set<Chamada>().Add(chamada);
        await GravarELimparAsync(contexto, primeira, segunda);

        ConferirOfertas(await contexto.Set<Convocacao>().AsNoTracking().Where(c => c.ChamadaId == chamada.Id).Select(c => c.OfertaCurso).ToArrayAsync());
    }

    [Fact]
    public async Task Matriculas_MesmaOfertaRecebida_PersistemCopiasIndependentes()
    {
        CopiaDaOfertaDeCurso oferta = new CopiaDaOfertaDeCursoDeTeste().Construir();
        Guid candidatoId = Guid.NewGuid();
        Matricula primeira = Matricula.Criar(Guid.NewGuid(), candidatoId, oferta);
        Matricula segunda = Matricula.Criar(Guid.NewGuid(), candidatoId, oferta);
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await GravarELimparAsync(contexto, primeira, segunda);

        ConferirOfertas(await contexto.Set<Matricula>().AsNoTracking().Where(m => m.CandidatoId == candidatoId).Select(m => m.OfertaCurso).ToArrayAsync());
    }

    [Fact(DisplayName = "Convocação gravada é lida com protocolo e status, sem eventos de domínio pendentes")]
    public async Task Convocacao_GravadaELida_MantemProtocoloEStatusESemEventos()
    {
        Chamada chamada = NovaChamada();
        Convocacao gravada = NovaConvocacao(chamada.Id);
        await GravarAsync(chamada, gravada);

        Convocacao lida = await LerAsync(gravada.Id);

        lida.Protocolo.Valor.Should().Be(gravada.Protocolo.Valor);
        lida.Status.Should().Be(StatusConvocacao.Pendente);
        ConferirOferta(lida.OfertaCurso);
        lida.DomainEvents.Should().BeEmpty("a materialização não registra o evento de candidato convocado de novo");
    }

    [Fact]
    public async Task Matricula_GravadaELida_PreservaOsCincoCamposDaOferta()
    {
        Matricula gravada = Matricula.Criar(Guid.NewGuid(), Guid.NewGuid(), new CopiaDaOfertaDeCursoDeTeste().Construir());
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        contexto.Set<Matricula>().Add(gravada);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        Matricula lida = await contexto.Set<Matricula>().AsNoTracking().SingleAsync(item => item.Id == gravada.Id);
        ConferirOferta(lida.OfertaCurso);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Inserir_CodigoInvalido_RestricaoRecusa(bool convocacao)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Chamada chamada = NovaChamada();
        contexto.Set<Chamada>().Add(chamada);
        await contexto.SaveChangesAsync();
        Func<Task> inserir = () => InserirInvalidoAsync(contexto, convocacao, chamada.Id);
        PostgresException erro = (await inserir.Should().ThrowAsync<PostgresException>()).Which;
        erro.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        erro.ConstraintName.Should().Be(convocacao ? "ck_convocacoes_oferta_curso_codigo_formato" : "ck_matriculas_oferta_curso_codigo_formato");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ler_CodigoInvalido_FalhaExplicitamente(bool convocacao)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await using IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync();
        Chamada chamada = NovaChamada();
        contexto.Set<Chamada>().Add(chamada);
        await contexto.SaveChangesAsync();
        await RemoverRestricaoAsync(contexto, convocacao);
        await InserirInvalidoAsync(contexto, convocacao, chamada.Id);
        Func<Task> ler = () => LerInvalidoAsync(contexto, convocacao);
        (await ler.Should().ThrowAsync<InvalidOperationException>()).Which.Message
            .Should().Be("Código de cadastro no banco está fora do formato esperado.");
        await transacao.RollbackAsync();
    }

    private static async Task GravarELimparAsync<T>(UniPlusDbContext contexto, params T[] entidades)
        where T : class
    {
        contexto.Set<T>().AddRange(entidades);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
    }

    private static void ConferirOfertas(CopiaDaOfertaDeCurso[] ofertas)
    {
        ofertas.Should().HaveCount(2);
        ofertas[0].Should().NotBeSameAs(ofertas[1]);
        foreach (CopiaDaOfertaDeCurso oferta in ofertas)
        {
            ConferirOferta(oferta);
        }
    }

    private static void ConferirOferta(CopiaDaOfertaDeCurso oferta)
    {
        oferta.Codigo.Should().Be("ENGENHARIA_CIVIL_MARABA");
        oferta.CursoNome.Should().Be("Engenharia Civil");
        oferta.CursoGrau.Should().Be("Bacharelado");
        oferta.UnidadeOfertanteNome.Should().Be("Unidade de Marabá");
        oferta.Turnos.Should().Equal("MATUTINO");
        Action alterarTurno = () => ((IList<string>)oferta.Turnos)[0] = "NOTURNO";
        alterarTurno.Should().Throw<NotSupportedException>();
        oferta.Turnos.Should().Equal("MATUTINO");
    }

    private static Task<int> RemoverRestricaoAsync(UniPlusDbContext contexto, bool convocacao) => convocacao
        ? contexto.Database.ExecuteSqlRawAsync("ALTER TABLE ingresso.convocacoes DROP CONSTRAINT ck_convocacoes_oferta_curso_codigo_formato")
        : contexto.Database.ExecuteSqlRawAsync("ALTER TABLE ingresso.matriculas DROP CONSTRAINT ck_matriculas_oferta_curso_codigo_formato");

    private static Task<int> InserirInvalidoAsync(UniPlusDbContext contexto, bool convocacao, Guid chamadaId) => convocacao
        ? contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ingresso.convocacoes (id, chamada_id, inscricao_id, candidato_id, protocolo, status, posicao,
                oferta_curso_codigo, oferta_curso_nome, oferta_curso_grau, oferta_unidade_ofertante_nome, oferta_turnos, created_at, is_deleted)
            VALUES ({Guid.NewGuid()}, {chamadaId}, {Guid.NewGuid()}, {Guid.NewGuid()}, {$"{Guid.NewGuid()}"}, 'PENDENTE', 1,
                'invalido', 'Engenharia Civil', 'Bacharelado', 'Unidade de Marabá', ARRAY['MATUTINO'], {Relogio.GetUtcNow()}, false)
            """)
        : contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ingresso.matriculas (id, convocacao_id, candidato_id, status, oferta_curso_codigo, oferta_curso_nome,
                oferta_curso_grau, oferta_unidade_ofertante_nome, oferta_turnos, created_at, is_deleted)
            VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, 'PENDENTE', 'invalido', 'Engenharia Civil',
                'Bacharelado', 'Unidade de Marabá', ARRAY['MATUTINO'], {Relogio.GetUtcNow()}, false)
            """);

    private static async Task LerInvalidoAsync(UniPlusDbContext contexto, bool convocacao)
    {
        if (convocacao)
        {
            await contexto.Set<Convocacao>().AsNoTracking().Where(item => item.OfertaCurso.Codigo == "invalido").ToListAsync();
        }
        else
        {
            await contexto.Set<Matricula>().AsNoTracking().Where(item => item.OfertaCurso.Codigo == "invalido").ToListAsync();
        }
    }

    private static Chamada NovaChamada()
    {
        DateTimeOffset publicacao = Relogio.GetUtcNow();

        return Chamada.Criar(Guid.NewGuid(), numero: 1, publicacao, publicacao.AddDays(5));
    }

    private static Convocacao NovaConvocacao(Guid chamadaId, CopiaDaOfertaDeCurso? oferta = null, int posicao = 1, int? sequencia = null) =>
        Convocacao.Criar(
            chamadaId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ProtocoloConvocacao.Gerar(1, sequencia ?? Random.Shared.Next(1, 99999), Relogio),
            posicao: posicao,
            ofertaCurso: oferta ?? new CopiaDaOfertaDeCursoDeTeste().Construir(),
            clock: Relogio);

    private async Task GravarAsync(Chamada chamada, Convocacao convocacao)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        contexto.Set<Chamada>().Add(chamada);
        contexto.Set<Convocacao>().Add(convocacao);
        await contexto.SaveChangesAsync();
    }

    private async Task<Convocacao> LerAsync(Guid id)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await contexto.Set<Convocacao>().AsNoTracking().SingleAsync(c => c.Id == id);
    }

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);
    }
}
