namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso.ApoioDaPersistenciaDeOfertaCurso;

/// <summary>
/// O texto que a oferta grava nas colunas dos regimes e dos turnos é o código de contrato, na ordem declarada, e a
/// leitura de um código que o enum não declara falha, sem devolver valor padrão.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class OfertaCursoRegimesETurnosNoBancoTests
{
    private const string TipoDaColunaDeTurnos = """
        SELECT format_type(a.atttypid, a.atttypmod) AS "Value"
        FROM pg_attribute a
        JOIN pg_class c ON c.oid = a.attrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'configuracao' AND c.relname = 'ofertas_de_curso' AND a.attname = 'turnos'
        """;

    private const string TurnosGravados = "SELECT array_to_string(turnos, ',') AS \"Value\" FROM configuracao.ofertas_de_curso WHERE id = {0}";
    private const string FuncionamentoGravado = "SELECT regime_de_funcionamento AS \"Value\" FROM configuracao.ofertas_de_curso WHERE id = {0}";
    private const string RegimeDeTurnoGravado = "SELECT regime_de_turno AS \"Value\" FROM configuracao.ofertas_de_curso WHERE id = {0}";

    private const string RemoveDominioDoFuncionamento = "ALTER TABLE configuracao.ofertas_de_curso DROP CONSTRAINT ck_ofertas_de_curso_regime_de_funcionamento";
    private const string RemoveDominioDoRegimeDeTurno = "ALTER TABLE configuracao.ofertas_de_curso DROP CONSTRAINT ck_ofertas_de_curso_regime_de_turno, DROP CONSTRAINT ck_ofertas_de_curso_turnos_regime";
    private const string RemoveDominioDosTurnos = "ALTER TABLE configuracao.ofertas_de_curso DROP CONSTRAINT ck_ofertas_de_curso_turnos_dominio";

    private readonly ConfiguracaoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDeOfertaCurso _apoio;

    public OfertaCursoRegimesETurnosNoBancoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDeOfertaCurso(fixture);
    }

    [Fact(DisplayName = "A coluna dos turnos é uma lista de texto de até 30 caracteres")]
    public async Task Turnos_TipoDaColuna_EListaDeTextoDe30Caracteres()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        string tipo = await ctx.Database.SqlQueryRaw<string>(TipoDaColunaDeTurnos).SingleAsync();

        tipo.Should().Be("character varying(30)[]");
    }

    [Theory(DisplayName = "Os turnos gravados são os códigos de contrato, na mesma ordem, e a leitura devolve os mesmos turnos")]
    [InlineData(new[] { "MATUTINO" }, "MATUTINO", "REGULAR", new[] { TurnoOferta.Matutino })]
    [InlineData(new[] { "VESPERTINO" }, "VESPERTINO", "REGULAR", new[] { TurnoOferta.Vespertino })]
    [InlineData(new[] { "NOTURNO" }, "NOTURNO", "REGULAR", new[] { TurnoOferta.Noturno })]
    [InlineData(new[] { "MATUTINO", "VESPERTINO" }, "MATUTINO,VESPERTINO", "INTEGRAL", new[] { TurnoOferta.Matutino, TurnoOferta.Vespertino })]
    [InlineData(new[] { "MATUTINO", "NOTURNO" }, "MATUTINO,NOTURNO", "INTEGRAL", new[] { TurnoOferta.Matutino, TurnoOferta.Noturno })]
    [InlineData(new[] { "VESPERTINO", "NOTURNO" }, "VESPERTINO,NOTURNO", "INTEGRAL", new[] { TurnoOferta.Vespertino, TurnoOferta.Noturno })]
    public async Task Turnos_Gravados_VoltamNaMesmaOrdem(string[] declarados, string textoEsperado, string regime, TurnoOferta[] esperados)
    {
        Guid ofertaId = await GravarOfertaAsync("EXTENSIVO", regime, declarados);

        (await TextoAsync(TurnosGravados, ofertaId)).Should().Be(textoEsperado);
        (await LerAsync(ofertaId)).Turnos.Should().Equal(esperados);
    }

    [Theory(DisplayName = "Os regimes gravados em SQL cru são os códigos de contrato e a leitura devolve os mesmos regimes")]
    [InlineData("INTENSIVO", "INTEGRAL", RegimeDeFuncionamento.Intensivo, RegimeDeTurno.Integral)]
    [InlineData("EXTENSIVO", "REGULAR", RegimeDeFuncionamento.Extensivo, RegimeDeTurno.Regular)]
    [InlineData("EXTENSIVO", "INTEGRAL", RegimeDeFuncionamento.Extensivo, RegimeDeTurno.Integral)]
    [InlineData("ALTERNANCIA_PEDAGOGICA", "REGULAR", RegimeDeFuncionamento.AlternanciaPedagogica, RegimeDeTurno.Regular)]
    [InlineData("ALTERNANCIA_PEDAGOGICA", "INTEGRAL", RegimeDeFuncionamento.AlternanciaPedagogica, RegimeDeTurno.Integral)]
    public async Task Regimes_GravadosComoCodigo_VoltamAosMesmosValores(
        string funcionamento, string regime, RegimeDeFuncionamento funcionamentoEsperado, RegimeDeTurno regimeEsperado)
    {
        Guid ofertaId = await GravarOfertaAsync(funcionamento, regime, TurnosDoRegime(regime));

        (await TextoAsync(FuncionamentoGravado, ofertaId)).Should().Be(funcionamento);
        (await TextoAsync(RegimeDeTurnoGravado, ofertaId)).Should().Be(regime);
        OfertaCurso lida = await LerAsync(ofertaId);
        lida.RegimeDeFuncionamento.Should().Be(funcionamentoEsperado);
        lida.RegimeDeTurno.Should().Be(regimeEsperado);
    }

    [Theory(DisplayName = "CHECK de banco recusa item de turno com espaço, em outra caixa ou vazio")]
    [InlineData(" MATUTINO")]
    [InlineData("MATUTINO ")]
    [InlineData("matutino")]
    [InlineData("")]
    public async Task Check_TurnoForaDosCodigos_Recusa(string item)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(cursoId, localId, LinhaCom("EXTENSIVO", "REGULAR", [item]));

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Theory(DisplayName = "CHECK de banco recusa regime de turno com espaço, em outra caixa ou vazio")]
    [InlineData(" REGULAR")]
    [InlineData("regular")]
    [InlineData("")]
    public async Task Check_RegimeDeTurnoForaDosCodigos_Recusa(string regime)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(cursoId, localId, LinhaCom("EXTENSIVO", regime, ["MATUTINO"]));

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Theory(DisplayName = "Regime de funcionamento gravado com código que o enum não declara faz a leitura falhar, sem valor padrão")]
    [InlineData("SEMI_INTENSIVO")]
    [InlineData("intensivo")]
    [InlineData(" INTENSIVO")]
    [InlineData("0")]
    public async Task Leitura_FuncionamentoComCodigoInexistente_Falha(string codigoInexistente) =>
        await ConferirFalhaDaLeituraAsync(RemoveDominioDoFuncionamento, LinhaCom(codigoInexistente, "REGULAR", ["MATUTINO"]));

    [Theory(DisplayName = "Regime de turno gravado com código que o enum não declara faz a leitura falhar, sem valor padrão")]
    [InlineData("PARCIAL")]
    [InlineData("regular")]
    [InlineData("")]
    public async Task Leitura_RegimeDeTurnoComCodigoInexistente_Falha(string codigoInexistente) =>
        await ConferirFalhaDaLeituraAsync(RemoveDominioDoRegimeDeTurno, LinhaCom("EXTENSIVO", codigoInexistente, ["MATUTINO"]));

    [Theory(DisplayName = "Turno gravado com código que o enum não declara faz a leitura falhar, sem valor padrão")]
    [InlineData("MADRUGADA")]
    [InlineData("matutino")]
    [InlineData("")]
    public async Task Leitura_TurnoComCodigoInexistente_Falha(string codigoInexistente) =>
        await ConferirFalhaDaLeituraAsync(RemoveDominioDosTurnos, LinhaCom("EXTENSIVO", "REGULAR", [codigoInexistente]));

    private static string[] TurnosDoRegime(string regime) =>
        regime == "INTEGRAL" ? ["MATUTINO", "VESPERTINO"] : ["MATUTINO"];

    private static LinhaCruDeOferta LinhaCom(string funcionamento, string regime, string?[] turnos) =>
        new("REGULAR", "PRESENCIAL", regime, turnos, null, null, funcionamento);

    // A restrição cai só dentro da transação, que nunca é confirmada: o banco volta a recusar o código para os demais testes.
    private async Task ConferirFalhaDaLeituraAsync(string comandoQueRemoveARestricao, LinhaCruDeOferta linha)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await ctx.Database.ExecuteSqlRawAsync(comandoQueRemoveARestricao);
        await InserirCruNoContextoAsync(ctx, cursoId, localId, linha);

        Func<Task> act = () => ctx.OfertasCurso.AsNoTracking().Where(o => o.CursoId == cursoId).ToListAsync();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private async Task<Guid> GravarOfertaAsync(string funcionamento, string regime, string[] turnos)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso oferta = OfertaCurso.Criar(
            cursoId,
            localId,
            NovaUnidade(),
            new DeclaracaoDaOfertaDeCurso(CodigoCru(), "REGULAR", "PRESENCIAL", funcionamento, regime, turnos)).ValorEsperado();
        await _apoio.GravarAsync(oferta);

        return oferta.Id;
    }

    private async Task<string> TextoAsync(string consulta, Guid ofertaId)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        return await ctx.Database.SqlQueryRaw<string>(consulta, ofertaId).SingleAsync();
    }

    private async Task<OfertaCurso> LerAsync(Guid ofertaId)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        return await ctx.OfertasCurso.AsNoTracking().SingleAsync(o => o.Id == ofertaId);
    }
}
