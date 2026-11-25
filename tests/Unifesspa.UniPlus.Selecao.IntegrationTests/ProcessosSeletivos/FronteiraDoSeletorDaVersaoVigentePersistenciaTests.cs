namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CenarioDeRetificacaoPersistida;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CenarioDeVersaoVigentePersistida;

/// <summary>
/// A fronteira do seletor da versão vigente (Postgres real): ele lê só a tabela de versões, não
/// depende de o ato já existir em Publicações e não resolve configuração de processo excluído ou
/// inexistente (ADR-0061, ADR-0075, ADR-0104, ADR-0108).
/// </summary>
[Collection(VersaoVigentePersistenciaColecao.Nome)]
public sealed class FronteiraDoSeletorDaVersaoVigentePersistenciaTests
{
    private readonly CenarioDeVersaoVigentePersistida _cenario;

    public FronteiraDoSeletorDaVersaoVigentePersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeVersaoVigentePersistida(fixture);
    }

    [Fact(DisplayName = "O SQL do seletor toca APENAS a tabela de versões — nenhum atributo do ato entra na escolha")]
    public async Task ObterVersaoVigente_SqlSoTocaEmVersoes()
    {
        // A prova mecânica de que a vigência ordena VERSÕES, não documentos (ADR-0104). A tabela do
        // edital nem existe mais; o que resta a garantir é que nenhuma OUTRA fonte de atributo
        // documental (o documento, um join com Publicações) se infiltre no seletor pela porta dos fundos.
        RelogioManualDaVigencia clock = new(T0);
        (Guid processoId, _, _) = await _cenario.PublicarERetificarAsync(
            nameof(ObterVersaoVigente_SqlSoTocaEmVersoes), clock, TimeSpan.FromDays(1));

        CapturadorDeSql capturador = new();
        await using MonolitoDbContext context = CriarContextoQueCapturaSql(capturador);
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(context);

        (await repository.ObterVersaoVigenteAsync(processoId, T0.AddDays(2), CancellationToken.None)).Should().NotBeNull();

        ConferirSqlDaVersaoVigente(capturador);
    }

    private static void ConferirSqlDaVersaoVigente(CapturadorDeSql capturador)
    {
        capturador.Comandos.Should().ContainSingle("a resolução do vigente é UMA leitura — não há hidratação do documento a fazer");
        string sql = capturador.Comandos[0];
        sql.Should().Contain("versoes_configuracao");
        sql.Should().NotContain("editais", "a tabela do documento não existe mais, e o seletor jamais dependeu dela");
        sql.Should().NotContain("documentos_a_publicar", "o documento não decide qual configuração vale");
        sql.Should().NotContain("atos_normativos", "e o ato tampouco — ele entra por VALOR, não por join (ADR-0061)");
    }

    /// <summary>
    /// O ato é registrado em Publicações por mensagem durável, DEPOIS do commit da publicação (ADR-0108). Entre o 204 e a
    /// drenagem do outbox, a versão existe e o ato ainda não — e é exatamente aí que o certame precisa continuar
    /// consultável. A referência é por valor, sem FK (ADR-0061): o seletor não sabe nem pergunta se o ato já existe do
    /// outro lado.
    /// </summary>
    [Fact(DisplayName = "Versão cujo ato ainda não foi registrado resolve normalmente — a seleção não espera a fila drenar")]
    public async Task ObterVersaoVigente_AtoAindaNaoRegistrado_AindaResolve()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nameof(ObterVersaoVigente_AtoAindaNaoRegistrado_AindaResolve));
        DocumentoAPublicar documento = DocumentoConfirmado(processo.Id);
        SnapshotCanonico canonico = Canonicalizar(processo, NovosDados(documento.Id), documento);
        Guid atoAindaNaoRegistrado = Guid.CreateVersion7();
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processo.Id, new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, documento.HashSha256!),
            atoAindaNaoRegistrado, "integration-test-user", T0);
        await GravarAsync(processo, documento, versao);

        VersaoConfiguracao? vigente = await VigenteAsync(processo.Id, T0.AddDays(1));

        vigente.Should().NotBeNull("a seleção não depende da existência do ato do outro lado da fila");
        vigente!.Id.Should().Be(versao.Id);
        vigente.AtoCriadorId.Should().Be(atoAindaNaoRegistrado, "a referência por valor ao ato está na própria versão");
    }

    private async Task GravarAsync(ProcessoSeletivo processo, DocumentoAPublicar documento, VersaoConfiguracao versao)
    {
        await using UniPlusDbContext writeContext = _cenario.CriarDbContext();
        await writeContext.ProcessosSeletivos.AddAsync(processo, CancellationToken.None);
        await writeContext.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
        await writeContext.VersoesConfiguracao.AddAsync(versao, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<VersaoConfiguracao?> VigenteAsync(Guid processoId, DateTimeOffset instante)
    {
        await using UniPlusDbContext context = _cenario.CriarDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(context);

        return await repository.ObterVersaoVigenteAsync(processoId, instante, CancellationToken.None);
    }

    [Fact(DisplayName = "Seletor não resolve configuração de processo soft-deleted nem de processo inexistente (base do 404)")]
    public async Task ObterVersaoVigente_ProcessoInexistenteOuExcluido_RetornaNull()
    {
        RelogioManualDaVigencia clock = new(T0);
        (Guid processoId, _, _) = await _cenario.PublicarERetificarAsync(
            nameof(ObterVersaoVigente_ProcessoInexistenteOuExcluido_RetornaNull), clock, TimeSpan.FromDays(1));

        await ExcluirProcessoAsync(processoId);

        await using UniPlusDbContext context = _cenario.CriarDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(context);

        await ConferirAusenciaDeVersaoAsync(repository, processoId);
    }

    private static async Task ConferirAusenciaDeVersaoAsync(ProcessoSeletivoRepository repository, Guid processoId)
    {
        // VersaoConfiguracao é forense (sem exclusão lógica própria), mas o EXISTS
        // através de ProcessosSeletivos herda o filtro global: um processo excluído
        // não vaza a configuração congelada.
        (await repository.ObterVersaoVigenteAsync(processoId, T0.AddDays(5), CancellationToken.None))
            .Should().BeNull("um processo excluído logicamente cai no mesmo 404 do resto da API");
        (await repository.ExisteAsync(processoId, CancellationToken.None)).Should().BeFalse();

        (await repository.ObterVersaoVigenteAsync(Guid.CreateVersion7(), T0.AddDays(5), CancellationToken.None))
            .Should().BeNull();
    }

    private async Task ExcluirProcessoAsync(Guid processoId)
    {
        await using (UniPlusDbContext deleteContext = _cenario.CriarDbContext())
        {
            ProcessoSeletivo processo = await deleteContext.ProcessosSeletivos
                .SingleAsync(p => p.Id == processoId, CancellationToken.None);
            processo.MarkAsDeleted("integration-test-user", T0.AddDays(3));
            await deleteContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private MonolitoDbContext CriarContextoQueCapturaSql(CapturadorDeSql capturador)
    {
        DbContextOptions<MonolitoDbContext> options = new DbContextOptionsBuilder<MonolitoDbContext>()
            .UseNpgsql(_cenario.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new SoftDeleteInterceptor(TimeProvider.System, userContext: null),
                new AuditableInterceptor(TimeProvider.System, userContext: null),
                capturador)
            .Options;

        return new MonolitoDbContext(options);
    }

    /// <summary>Captura o SQL emitido — prova mecânica de que o seletor não toca em <c>editais</c>.</summary>
    private sealed class CapturadorDeSql : DbCommandInterceptor
    {
        public List<string> Comandos { get; } = [];

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            Comandos.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
