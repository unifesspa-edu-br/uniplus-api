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
/// Prova executável de que a precondição do <c>Down</c> da migration que semeia a eliminação
/// por falta em dia de prova do ENEM também aborta diante de um RASCUNHO VIVO em
/// <c>regras_eliminacao</c>, e só diante da própria entrada (ADR-0112, Emenda 1).
/// </summary>
/// <remarks>
/// Classe própria, com fixture própria: a classe irmã
/// (<see cref="EliminacaoPorFaltaEmDiaDeProvaEnemPrecondicaoTests"/>) fabrica uma referência
/// congelada, permanente por gatilho, que faria esta precondição abortar por um motivo que não
/// é o rascunho.
/// </remarks>
public sealed class EliminacaoPorFaltaEmDiaDeProvaEnemRascunhoVivoPrecondicaoTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public EliminacaoPorFaltaEmDiaDeProvaEnemRascunhoVivoPrecondicaoTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A precondição aborta diante do rascunho vivo com a regra — a publicação não reconsulta o catálogo")]
    public async Task Precondicao_AbortaDianteDeRascunhoVivo()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        // Sem rascunho algum, reverter é legítimo.
        await ReverterAsync(context);

        Guid processoId = await FabricarRascunhoVivoAsync(
            context, RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "v1", new ArgsElimFaltaEmDiaDeProvaEnem());
        try
        {
            Func<Task> reversao = () => ReverterAsync(context);

            (await reversao.Should().ThrowAsync<DbException>(
                "a precondição aborta diante do rascunho vivo, não só da referência congelada"))
                .WithMessage("*ADR-0112*");
        }
        finally
        {
            await RemoverAsync(processoId);
        }
    }

    [Fact(DisplayName = "Rascunho vivo com OUTRA regra de eliminação não aborta")]
    public async Task Precondicao_RascunhoVivoDeOutraRegra_NaoAborta()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        Guid processoId = await FabricarRascunhoVivoAsync(
            context, RegraEliminacaoCodigo.ElimZeroEmArea, "v1", new ArgsElimZeroEmArea());
        try
        {
            await ReverterAsync(context);
        }
        finally
        {
            await RemoverAsync(processoId);
        }
    }

    [Fact(DisplayName = "Rascunho vivo com a mesma regra em outra versão não aborta")]
    public async Task Precondicao_RascunhoVivoDeOutraVersao_NaoAborta()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        Guid processoId = await FabricarRascunhoVivoAsync(
            context, RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "v2", new ArgsElimFaltaEmDiaDeProvaEnem());
        try
        {
            await ReverterAsync(context);
        }
        finally
        {
            await RemoverAsync(processoId);
        }
    }

    private static Task ReverterAsync(SelecaoDbContext context) =>
        FronteiraAppendOnlyDoRol.ExecutarAsync(context, EliminacaoPorFaltaEmDiaDeProvaEnemPrecondicaoTests.PrecondicaoDaMigration);

    private async Task RemoverAsync(Guid processoId)
    {
        await using SelecaoDbContext cleanupContext = _fixture.CreateDbContext();
        await cleanupContext.ProcessosSeletivos
            .Where(p => p.Id == processoId)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Persiste um processo com uma classificação baseada em ENEM que declara só a regra de
    /// eliminação dada: o bastante para materializar a linha em <c>regras_eliminacao</c>.
    /// </summary>
    private static async Task<Guid> FabricarRascunhoVivoAsync(
        SelecaoDbContext context, string codigoRegra, string versaoRegra, ArgsRegraEliminacao args)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Fronteira append-only — rascunho vivo {codigoRegra}/{versaoRegra}",
            TipoProcesso.SiSU,
            OrigemCandidatos.InscricaoPropria,
            Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(Regra(codigoRegra, versaoRegra), args).Value!;
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

        Result definir = processo.DefinirClassificacao(classificacao.Value!, PrecondicaoIfMatch.Ausente);
        definir.IsSuccess.Should().BeTrue(definir.Error?.Message);

        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync(CancellationToken.None);

        return processo.Id;
    }

    private static ReferenciaRegra Regra(string codigo, string versao = "v1") =>
        ReferenciaRegra.Criar(codigo, versao, new string('a', 64)).Value!;
}
