namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A linha de <c>etapas_processo</c> como o banco a guarda, lida sem passar pelo agregado.</summary>
internal sealed record EtapaGravada(Guid Id, string Nome, int? Ordem, decimal? Peso, Guid ProcessoSeletivoId);

/// <summary>O processo semeado com a Prova Objetiva e a Redação, e os tipos de etapa que cada uma declara.</summary>
internal sealed record ProcessoComDuasEtapas(
    Guid ProcessoId, Guid ProvaObjetivaId, Guid TipoProvaObjetivaId, Guid RedacaoId, Guid TipoRedacaoId);

/// <summary>
/// Semeia, grava e lê as etapas de um processo direto no Postgres do fixture, para os testes que provam o que a
/// definição das etapas deixa (ou não) no banco.
/// </summary>
internal static class EtapasNoBancoDeTeste
{
    private const string FaseDasEtapas = "RESULTADO_FINAL";

    /// <summary>O processo conforme ainda em rascunho, com a Prova Objetiva e a Redação (esta com uma banca).</summary>
    public static async Task<ProcessoComDuasEtapas> SemearRascunhoAsync(ProcessoSeletivoDbFixture fixture, string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme(nome);
        (EtapaProcesso prova, EtapaProcesso redacao) = (processo.Etapas.Single(), EtapaDeRedacao());
        processo.DefinirEtapas([prova, redacao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using SelecaoDbContext contexto = fixture.CreateDbContext();
        contexto.ProcessosSeletivos.Add(processo);
        await contexto.SaveChangesAsync();
        return new ProcessoComDuasEtapas(processo.Id, prova.Id, prova.TipoEtapaOrigemId, redacao.Id, redacao.TipoEtapaOrigemId);
    }

    /// <summary>
    /// O processo publicado, com a sessão de retificação aberta e a Redação acrescentada nela: a Prova Objetiva é a
    /// etapa da versão publicada vigente, e só ela começou (houve importação de notas nela).
    /// </summary>
    public static async Task<ProcessoComDuasEtapas> SemearRetificacaoComProvaIniciadaAsync(ProcessoSeletivoDbFixture fixture, string nome)
    {
        ProcessoSeletivoPublicacaoSeeder.Resultado publicado = await ProcessoSeletivoPublicacaoSeeder.PublicarAsync(
            fixture, nome, new DateTimeOffset(2040, 3, 10, 0, 0, 0, TimeSpan.Zero));
        await using SelecaoDbContext contexto = fixture.CreateDbContext();
        ProcessoSeletivo processo = (await new ProcessoSeletivoRepository(contexto, TimeProvider.System).ObterParaMutacaoAsync(publicado.ProcessoId))!;
        EtapaProcesso prova = processo.Etapas.Single();
        EtapaProcesso redacao = EtapaDeRedacao();

        AbrirRetificacaoComARedacao(processo, await contexto.VersoesConfiguracao.SingleAsync(v => v.Id == publicado.VersaoId), prova, redacao);
        contexto.LotesDeNotasDoEnem.Add(ImportacaoDeNotasNaEtapa(processo.Id, prova.Id, publicado.VersaoId));
        await contexto.SaveChangesAsync();
        return new ProcessoComDuasEtapas(processo.Id, prova.Id, prova.TipoEtapaOrigemId, redacao.Id, redacao.TipoEtapaOrigemId);
    }

    public static async Task<IReadOnlyList<EtapaGravada>> EtapasGravadasAsync(ProcessoSeletivoDbFixture fixture, Guid processoId)
    {
        await using SelecaoDbContext conferencia = fixture.CreateDbContext();
        return await conferencia.Set<EtapaProcesso>()
            .AsNoTracking()
            .Where(e => e.ProcessoSeletivoId == processoId)
            .OrderBy(e => e.Nome)
            .Select(e => new EtapaGravada(e.Id, e.Nome, e.Ordem, e.Peso, e.ProcessoSeletivoId))
            .ToListAsync();
    }

    public static async Task<int> BancasGravadasAsync(ProcessoSeletivoDbFixture fixture, Guid etapaId)
    {
        await using SelecaoDbContext conferencia = fixture.CreateDbContext();
        return await conferencia.Set<BancaDaEtapa>().CountAsync(b => b.EtapaProcessoId == etapaId);
    }

    public static async Task<string?> ETagDaSessaoGravadaAsync(ProcessoSeletivoDbFixture fixture, Guid processoId)
    {
        await using SelecaoDbContext conferencia = fixture.CreateDbContext();
        ProcessoSeletivo processo = await conferencia.ProcessosSeletivos.Include(p => p.Rascunho).SingleAsync(p => p.Id == processoId);
        return processo.ETagDaSessaoEditorial;
    }

    private static void AbrirRetificacaoComARedacao(
        ProcessoSeletivo processo, VersaoConfiguracao vigente, EtapaProcesso prova, EtapaProcesso redacao)
    {
        processo.AbrirRetificacao(
            "Inclusão da redação", vigente, identificadorDaVersaoBase: null,
            VersoesPublicadasDeTeste.SoAVigente(processo), "operador-de-teste", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([prova, redacao], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
    }

    private static LoteDeNotasDoEnem ImportacaoDeNotasNaEtapa(Guid processoId, Guid etapaId, Guid versaoId) =>
        LoteDeNotasDoEnem.Registrar(
            processoId, etapaId, "2026", "INEP_ENEM_RETORNO", "v1", new string('b', 64), new string('c', 64), "operador-de-teste",
            DateTimeOffset.UtcNow, 0, versaoId, new ResultadoDoCruzamento([], [], [], []), []);

    private static EtapaProcesso EtapaDeRedacao()
    {
        TipoEtapaSnapshot tipo = TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        EtapaProcesso redacao = EtapaProcesso.Criar(
            "Redação", CaraterEtapa.Classificatoria, tipo, peso: 1m, ordem: 2, notaMaxima: 10m,
            formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: FaseDasEtapas).Value!;
        redacao.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_REDACAO", "Banca de redação")]).IsSuccess.Should().BeTrue();
        return redacao;
    }

    /// <summary>A etapa como o cliente a reenvia: o <c>Id</c> ecoado e o que mudou.</summary>
    public static EtapaProcessoInput Reenvio(Guid? id, Guid tipoId, string nome, int ordem, decimal peso = 1m) => new(
        nome, CaraterEtapa.Classificatoria, tipoId, peso, null, ordem, id,
        FaseCodigo: FaseDasEtapas, Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 10m, FormaDeAvaliacao: "OBJETIVA");

    /// <summary>A etapa que o cliente acrescenta: sem <c>Id</c>, com o tipo que o cadastro conhece.</summary>
    public static EtapaProcessoInput Nova(Guid tipoId, string nome, int ordem) =>
        Reenvio(id: null, tipoId, nome, ordem);

    /// <summary>A entrada do cadastro de tipos de etapa que admite pontuação e eliminação.</summary>
    public static TipoEtapaView CadastroDoTipo(Guid tipoId, string codigo, string nome) =>
        new(tipoId, codigo, nome, null, true, true, false);
}
