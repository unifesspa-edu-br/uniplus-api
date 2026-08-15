namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Prova <b>contra o banco</b> a fronteira que autorizou substituir a definição de
/// <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c> no lugar, ao inverter a política de unidade do prazo
/// de interposição.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SubstituicaoDaRegraDeRecursoGuardaTests"/> lê o arquivo da migration e prova
/// que o SQL das guardas está lá. Isso pega a guarda apagada por descuido, e não pega a
/// guarda que não funciona — é o que esta classe cobre, executando os mesmos blocos contra
/// o Postgres do fixture.
/// </para>
/// <para>
/// O SQL aqui é cópia deliberada do que a migration carrega, e a classe irmã é quem amarra
/// as duas pontas: se a migration mudar sem que esta cópia acompanhe, é lá que quebra.
/// </para>
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo escrito no próprio teste, sem valor externo interpolado.")]
public sealed class SubstituicaoDaRegraDeRecursoFronteiraTests : IClassFixture<RegraCatalogoDbFixture>, IAsyncLifetime
{
    private const string CodigoDaRegra = "RECURSO-PRAZO-ANCORADO-EM-ATO";

    private const string HashDaDefinicaoVigente =
        "92e78394a057b6eadbdcb69c7b08793ff8801790856874d99355074483b2709c";

    // Os três SQLs vêm da própria migration, não de cópia: é ela que decide qual unidade
    // cada sentido recusa, e um teste que reescrevesse o predicado passaria a provar a si
    // mesmo. `Sentido` carrega junto o texto de orientação e o hash de destino.
    private static (int Unidade, string ComoRedeclarar, string HashDestino) Avanco =>
        SubstituiInvariantesPrazoInterposicaoEmDiaUtil.ArgumentosDoAvanco;

    private static (int Unidade, string ComoRedeclarar, string HashDestino) Reversao =>
        SubstituiInvariantesPrazoInterposicaoEmDiaUtil.ArgumentosDaReversao;

    private static string GuardaDe((int Unidade, string ComoRedeclarar, string HashDestino) sentido) =>
        SubstituiInvariantesPrazoInterposicaoEmDiaUtil.SqlDaGuardaDeRegraViva(sentido.Unidade, sentido.ComoRedeclarar);

    private readonly RegraCatalogoDbFixture _fixture;

    /// <summary>
    /// Processos fabricados por este fato, para que ele os remova ao terminar.
    /// </summary>
    private readonly List<Guid> _processosFabricados = [];

    public SubstituicaoDaRegraDeRecursoFronteiraTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Remove o que o fato inseriu — sem isso, a asserção de que a guarda <b>não</b> aborta
    /// dependeria da ordem em que o xUnit executa os fatos da classe.
    /// </summary>
    /// <remarks>
    /// A guarda varre <c>regras_recurso_fase</c> inteira, como a migration faz de verdade:
    /// ela não tem como saber quais linhas pertencem a qual cenário. Então a linha em dia
    /// corrido deixada por um fato faria o fato seguinte abortar por dado que não é dele.
    /// Estreitar o predicado para o teste passar seria provar outra guarda, não esta —
    /// a limpeza é que precisa acontecer. Cascade no banco leva fase e regra junto.
    /// </remarks>
    public async Task DisposeAsync()
    {
        if (_processosFabricados.Count == 0)
        {
            return;
        }

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        string ids = string.Join(", ", _processosFabricados.Select(id => $"'{id}'"));
        await FronteiraAppendOnlyDoRol.ExecutarAsync(
            context, $"DELETE FROM selecao.processos_seletivos WHERE id IN ({ids});");
    }

    [Fact(DisplayName = "Nenhuma configuração congelada referencia a entrada substituída — e o detector que afirma isso funciona")]
    public async Task NenhumaConfiguracaoCongeladaReferencia()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        // A substituição no lugar só foi legítima porque a entrada ainda era vocabulário,
        // não fato (ADR-0112). O helper prova antes que o detector enxerga a referência real
        // e não confunde com ela prefixo, homônimo, outra versão nem referência sem hash —
        // sem isso, "nenhuma referência" poderia ser só um detector cego.
        await FronteiraAppendOnlyDoRol.NenhumaReferenciaCongeladaAsync(
            context, CodigoDaRegra, RegraCatalogoSeed.VersaoV1);
    }

    [Fact(DisplayName = "Rascunho em dia corrido aborta o avanço — a unidade deixou de ser declarável")]
    public async Task RascunhoEmDiaCorrido_AbortaOAvanco()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        Guid faseId = await FabricarRascunhoComRegraDeRecursoAsync(context, "dia-corrido");

        // A linha legada não é construível pelo domínio: RegraRecursoFase.Criar recusa dia
        // corrido desde a inversão. Ela é escrita direto na coluna porque é exatamente assim
        // que existiria num banco alimentado sob a política anterior — e porque o EF a
        // reidrataria sem reavaliar invariante nenhuma.
        await ForcarUnidadeAsync(context, faseId, (UnidadePrazo)Avanco.Unidade);

        Func<Task> avancar = () => FronteiraAppendOnlyDoRol.ExecutarAsync(context, GuardaDe(Avanco));

        (await avancar.Should().ThrowAsync<DbException>(
            "publicar esse rascunho produziria versão com unidade que a regra vigente recusa"))
            .WithMessage($"*{Avanco.ComoRedeclarar}*");
    }

    [Fact(DisplayName = "Rascunho em dias úteis aborta a reversão — o problema espelhado, sob a definição antiga")]
    public async Task RascunhoEmDiasUteis_AbortaAReversao()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        // Aqui o domínio constrói: dias úteis é a unidade principal da política vigente.
        await FabricarRascunhoComRegraDeRecursoAsync(context, "dias-uteis", UnidadePrazo.DiasUteis, valor: 2m);

        Func<Task> reverter = () => FronteiraAppendOnlyDoRol.ExecutarAsync(context, GuardaDe(Reversao));

        (await reverter.Should().ThrowAsync<DbException>(
            "a definição que volta a valer proíbe dia útil na interposição, e nada revalida a unidade ao carregar"))
            .WithMessage($"*{Reversao.ComoRedeclarar}*");
    }

    [Fact(DisplayName = "Rascunho em unidade aceita não bloqueia o deploy, e tem o hash reapontado para a definição vigente")]
    public async Task RascunhoEmUnidadeAceita_NaoBloqueia_ETemOHashReapontado()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        Guid faseId = await FabricarRascunhoComRegraDeRecursoAsync(context, "horas");
        await ForcarHashAsync(context, faseId, Reversao.HashDestino);

        Func<Task> avancar = () => FronteiraAppendOnlyDoRol.ExecutarAsync(context, GuardaDe(Avanco));
        await avancar.Should().NotThrowAsync(
            "prazo em horas continua declarável, e bloquear o deploy por causa dele seria guarda ampliada além do que a mudança exige");

        await FronteiraAppendOnlyDoRol.ExecutarAsync(context, SubstituiInvariantesPrazoInterposicaoEmDiaUtil.SqlDoReaponteDeHash(Avanco.HashDestino));

        string hashDepois = await LerHashAsync(context, faseId);
        hashDepois.Should().Be(Avanco.HashDestino,
            "sem versão sucessora, manter o hash anterior deixaria a referência apontando para definição que não existe mais");
    }

    /// <summary>
    /// Cria e persiste um processo em rascunho cuja fase declara regra de recurso, e devolve
    /// o Id da fase — a chave para alcançar a linha filha nas asserções.
    /// </summary>
    private async Task<Guid> FabricarRascunhoComRegraDeRecursoAsync(
        SelecaoDbContext context,
        string cenario,
        UnidadePrazo unidade = UnidadePrazo.Horas,
        decimal valor = 48m)
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme(
            $"Fronteira da substituição — {cenario}");

        RegraRecursoFase regraRecurso = RegraRecursoFase.Criar(
            ReferenciaRegra.Criar(CodigoDaRegra, "v1", HashDaDefinicaoVigente).Value!,
            new ArgsRegraPrazoRecurso(
                PrazoValor: valor,
                PrazoUnidade: unidade,
                AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
                SuspensividadePrimeiraInstanciaValor: null,
                SuspensividadePrimeiraInstanciaUnidade: null,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null)).Value!;

        FaseCronograma faseComRecurso = FaseCronograma.Criar(
            ordem: 2,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_PRELIMINAR",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: false,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: false,
            coletaInscricao: false,
            inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_PRELIMINAR",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: regraRecurso).Value!;

        processo.DefinirCronogramaFases(
            [.. processo.CronogramaFases, faseComRecurso], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync(CancellationToken.None);
        _processosFabricados.Add(processo.Id);

        return processo.CronogramaFases.Single(f => f.RegraRecurso is not null).RegraRecurso!.Id;
    }

    private static Task ForcarUnidadeAsync(SelecaoDbContext context, Guid regraId, UnidadePrazo unidade) =>
        FronteiraAppendOnlyDoRol.ExecutarAsync(context, $"""
            UPDATE selecao.regras_recurso_fase
            SET prazo_unidade = {(int)unidade}
            WHERE id = '{regraId}';
            """);

    private static Task ForcarHashAsync(SelecaoDbContext context, Guid regraId, string hash) =>
        FronteiraAppendOnlyDoRol.ExecutarAsync(context, $"""
            UPDATE selecao.regras_recurso_fase
            SET regra_hash = '{hash}'
            WHERE id = '{regraId}';
            """);

    private static async Task<string> LerHashAsync(SelecaoDbContext context, Guid regraId)
    {
        DbConnection conexao = context.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await conexao.OpenAsync(CancellationToken.None);
        }

        await using DbCommand comando = conexao.CreateCommand();
        comando.CommandText = $"SELECT regra_hash FROM selecao.regras_recurso_fase WHERE id = '{regraId}';";
        return (string)(await comando.ExecuteScalarAsync(CancellationToken.None))!;
    }
}
