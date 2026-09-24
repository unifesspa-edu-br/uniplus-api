namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Prova executável das guardas da migration que troca <c>ALOCACAO-OPCOES-RN04</c> por
/// <c>ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA</c> (ADR-0112, Emenda 1), rodando o SQL recortado do
/// próprio arquivo da migration.
/// </summary>
public sealed class RenomeiaAlocacaoParaPrimeiraOpcaoPrioritariaTests : IClassFixture<RegraCatalogoDbFixture>
{
    internal const string ArquivoDaMigration = "20260924224022_RenomeiaAlocacaoParaPrimeiraOpcaoPrioritaria.cs";
    private const string CodigoAntigo = "ALOCACAO-OPCOES-RN04";

    private readonly RegraCatalogoDbFixture _fixture;

    public RenomeiaAlocacaoParaPrimeiraOpcaoPrioritariaTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O Up descarta a classificação em rascunho que cita o código antigo, e só ela")]
    public async Task Up_DescartaSoARascunhoComCodigoAntigo()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        Guid comAntigo = await FabricarRascunhoAsync(context, CodigoAntigo);
        Guid comNovo = await FabricarRascunhoAsync(context, RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria);
        try
        {
            await FronteiraAppendOnlyDoRol.ExecutarAsync(context, SqlDaMigration(FronteiraAppendOnlyDoRol.BlocoUp));

            (await ClassificacaoExisteAsync(comAntigo)).Should().BeFalse();
            (await ClassificacaoExisteAsync(comNovo)).Should().BeTrue();
        }
        finally
        {
            await RemoverAsync(comAntigo);
            await RemoverAsync(comNovo);
        }
    }

    [Fact(DisplayName = "O Down descarta a classificação em rascunho que cita o código novo, e só ela")]
    public async Task Down_DescartaSoARascunhoComCodigoNovo()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        Guid comAntigo = await FabricarRascunhoAsync(context, CodigoAntigo);
        Guid comNovo = await FabricarRascunhoAsync(context, RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria);
        try
        {
            await FronteiraAppendOnlyDoRol.ExecutarAsync(context, SqlDaMigration(FronteiraAppendOnlyDoRol.BlocoDown));

            (await ClassificacaoExisteAsync(comNovo)).Should().BeFalse();
            (await ClassificacaoExisteAsync(comAntigo)).Should().BeTrue();
        }
        finally
        {
            await RemoverAsync(comAntigo);
            await RemoverAsync(comNovo);
        }
    }

    /// <summary>O SQL que a migration passa a <c>migrationBuilder.Sql</c> no bloco pedido.</summary>
    internal static string SqlDaMigration(Func<string, string> bloco)
    {
        string trecho = bloco(FronteiraAppendOnlyDoRol.LerMigration(ArquivoDaMigration));
        const string Abertura = "migrationBuilder.Sql(\"\"\"";
        int inicio = trecho.IndexOf(Abertura, StringComparison.Ordinal);
        inicio.Should().BeGreaterThanOrEqualTo(0, "o bloco da migration executa SQL próprio");
        inicio += Abertura.Length;
        int fim = trecho.IndexOf("\"\"\");", inicio, StringComparison.Ordinal);
        return trecho[inicio..fim];
    }

    private async Task<bool> ClassificacaoExisteAsync(Guid processoId)
    {
        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await leitura.ProcessosSeletivos
            .Include(p => p.Classificacao)
            .SingleAsync(p => p.Id == processoId);
        return processo.Classificacao is not null;
    }

    private async Task RemoverAsync(Guid processoId)
    {
        await using SelecaoDbContext cleanupContext = _fixture.CreateDbContext();
        await cleanupContext.ProcessosSeletivos
            .Where(p => p.Id == processoId)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    private static async Task<Guid> FabricarRascunhoAsync(SelecaoDbContext context, string codigoAlocacao)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Renomeação da alocação — rascunho {codigoAlocacao}",
            TipoProcesso.SiSU,
            OrigemCandidatos.InscricaoPropria,
            Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada),
            regraArredondamento: null,
            casasArredondamento: null,
            Regra(codigoAlocacao),
            nOpcoesAlocacao: 1,
            [],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync(CancellationToken.None);
        return processo.Id;
    }

    private static ReferenciaRegra Regra(string codigo) =>
        ReferenciaRegra.Criar(codigo, "v1", new string('a', 64)).Value!;
}

/// <summary>
/// A referência congelada barra a renomeação nos dois sentidos. Fixtures próprias: a versão de
/// configuração fabricada é permanente por gatilho e barraria as provas das classes irmãs.
/// </summary>
public sealed class RenomeiaAlocacaoCodigoAntigoCongeladoTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public RenomeiaAlocacaoCodigoAntigoCongeladoTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O Up aborta diante de versão congelada que cita o código antigo")]
    public async Task Up_AbortaDianteDeReferenciaCongelada()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        string guarda = RenomeiaAlocacaoParaPrimeiraOpcaoPrioritariaTests.SqlDaMigration(FronteiraAppendOnlyDoRol.BlocoUp);

        await FronteiraAppendOnlyDoRol.ExecutarAsync(context, guarda);

        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context, "alocação antiga congelada", FronteiraAppendOnlyDoRol.TriplaDeReferencia("ALOCACAO-OPCOES-RN04", "v1"));
        Func<Task> substituicao = () => FronteiraAppendOnlyDoRol.ExecutarAsync(context, guarda);

        (await substituicao.Should().ThrowAsync<DbException>()).WithMessage("*ADR-0112*");
    }
}

/// <inheritdoc cref="RenomeiaAlocacaoCodigoAntigoCongeladoTests"/>
public sealed class RenomeiaAlocacaoCodigoNovoCongeladoTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public RenomeiaAlocacaoCodigoNovoCongeladoTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O Down aborta diante de versão congelada que cita o código novo")]
    public async Task Down_AbortaDianteDeReferenciaCongelada()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        string guarda = RenomeiaAlocacaoParaPrimeiraOpcaoPrioritariaTests.SqlDaMigration(FronteiraAppendOnlyDoRol.BlocoDown);

        await FronteiraAppendOnlyDoRol.ExecutarAsync(context, guarda);

        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context, "alocação nova congelada",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1"));
        Func<Task> reversao = () => FronteiraAppendOnlyDoRol.ExecutarAsync(context, guarda);

        (await reversao.Should().ThrowAsync<DbException>()).WithMessage("*ADR-0112*");
    }
}
