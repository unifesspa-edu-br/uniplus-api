namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Prova executável das guardas da migration que troca <c>ELIM-CORTE-REDACAO</c> por
/// <c>ELIM-CORTE-EM-AREA</c> (ADR-0112, Emenda 1), rodando o SQL recortado do próprio arquivo da
/// migration, e não uma cópia dele.
/// </summary>
public sealed class SubstituiCorteDeRedacaoPeloCorteEmAreaTests : IClassFixture<RegraCatalogoDbFixture>
{
    internal const string ArquivoDaMigration = "20260924181553_SubstituiCorteDeRedacaoPeloCorteEmArea.cs";

    private readonly RegraCatalogoDbFixture _fixture;

    public SubstituiCorteDeRedacaoPeloCorteEmAreaTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O Up descarta o rascunho vivo que declara o corte de Redação, e o processo volta a ser lido")]
    public async Task Up_DescartaRascunhoVivoComCorteDeRedacao()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        Guid processoId = await FabricarRascunhoVivoAsync(context, new ArgsElimZeroEmArea(), RegraEliminacaoCodigo.ElimZeroEmArea);
        try
        {
            await FronteiraAppendOnlyDoRol.ExecutarAsync(context, $$"""
                UPDATE selecao.regras_eliminacao r
                SET regra_codigo = 'ELIM-CORTE-REDACAO', args = '{"$tipo":"corteRedacao","minimo":400}'
                FROM selecao.configuracoes_classificacao c
                WHERE r.configuracao_classificacao_id = c.id AND c.processo_seletivo_id = '{{processoId}}'
                """);

            await FronteiraAppendOnlyDoRol.ExecutarAsync(context, SqlDaMigration(FronteiraAppendOnlyDoRol.BlocoUp));

            await using SelecaoDbContext leitura = _fixture.CreateDbContext();
            ProcessoSeletivo processo = await leitura.ProcessosSeletivos
                .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
                .SingleAsync(p => p.Id == processoId);
            processo.Classificacao!.RegrasEliminacao.Should().BeEmpty();
        }
        finally
        {
            await RemoverAsync(processoId);
        }
    }

    [Fact(DisplayName = "O Down aborta diante do rascunho vivo com o corte em área, e não diante de outra regra")]
    public async Task Down_AbortaDianteDeRascunhoVivoComCorteEmArea()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        string guarda = SqlDaMigration(FronteiraAppendOnlyDoRol.BlocoDown);

        Guid outraRegra = await FabricarRascunhoVivoAsync(context, new ArgsElimZeroEmArea(), RegraEliminacaoCodigo.ElimZeroEmArea);
        try
        {
            await FronteiraAppendOnlyDoRol.ExecutarAsync(context, guarda);
        }
        finally
        {
            await RemoverAsync(outraRegra);
        }

        Guid comCorte = await FabricarRascunhoVivoAsync(context, new ArgsElimCorteEmArea("REDACAO", 400m), RegraEliminacaoCodigo.ElimCorteEmArea);
        try
        {
            Func<Task> reversao = () => FronteiraAppendOnlyDoRol.ExecutarAsync(context, guarda);

            (await reversao.Should().ThrowAsync<DbException>()).WithMessage("*ADR-0112*");
        }
        finally
        {
            await RemoverAsync(comCorte);
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

    private async Task RemoverAsync(Guid processoId)
    {
        await using SelecaoDbContext cleanupContext = _fixture.CreateDbContext();
        await cleanupContext.ProcessosSeletivos
            .Where(p => p.Id == processoId)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    private static async Task<Guid> FabricarRascunhoVivoAsync(SelecaoDbContext context, ArgsRegraEliminacao args, string codigoRegra)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Substituição do corte — rascunho {codigoRegra}",
            TipoProcesso.SiSU,
            OrigemCandidatos.InscricaoPropria,
            Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(Regra(codigoRegra), args).Value!;
        Result<ConfiguracaoClassificacao> classificacao = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar),
            casasArredondamento: 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria),
            nOpcoesAlocacao: 1,
            [eliminacao],
            baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnemDeTeste.Completo());
        classificacao.IsSuccess.Should().BeTrue(classificacao.Error?.Message);
        processo.DefinirClassificacao(classificacao.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync(CancellationToken.None);
        return processo.Id;
    }

    private static ReferenciaRegra Regra(string codigo) =>
        ReferenciaRegra.Criar(codigo, "v1", new string('a', 64)).Value!;
}

/// <summary>
/// A referência congelada ao corte de Redação barra a substituição. Fixture própria: a versão de
/// configuração fabricada é permanente por gatilho e barraria as provas da classe irmã.
/// </summary>
public sealed class SubstituiCorteDeRedacaoPeloCorteEmAreaCongeladaTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public SubstituiCorteDeRedacaoPeloCorteEmAreaCongeladaTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O Up aborta diante de versão congelada que cita o corte de Redação")]
    public async Task Up_AbortaDianteDeReferenciaCongelada()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        string guarda = SubstituiCorteDeRedacaoPeloCorteEmAreaTests.SqlDaMigration(FronteiraAppendOnlyDoRol.BlocoUp);

        await FronteiraAppendOnlyDoRol.ExecutarAsync(context, guarda);

        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context, "corte de Redação congelado", FronteiraAppendOnlyDoRol.TriplaDeReferencia("ELIM-CORTE-REDACAO", "v1"));
        Func<Task> substituicao = () => FronteiraAppendOnlyDoRol.ExecutarAsync(context, guarda);

        (await substituicao.Should().ThrowAsync<DbException>()).WithMessage("*ADR-0112*");
    }
}
