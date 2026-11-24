namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A regra de homologação das inscrições que a fase declara, contra o Postgres real: a coluna
/// <c>regra_de_homologacao</c> (<c>jsonb</c>) grava e lê a regra com e sem enquadramento, assume nulo quando a
/// fase não homologa e aceita a troca da regra na fase rastreada.
/// </summary>
public sealed class RegraDeHomologacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string TipoDoRetornoDoInep = "RESULTADO_NO_RETORNO_DO_INEP";
    private const string ResultadoPreliminar = "RESULTADO_PRELIMINAR_HOMOLOGACAO";

    [Fact(DisplayName = "Regra com e sem enquadramento grava e lê de volta, na ordem declarada")]
    public async Task RegraComESemEnquadramento_GravaELeDeVolta()
    {
        RegraDeHomologacaoDasInscricoes regra = Regra(
            ResultadoPreliminar,
            Alternativa(Condicao(2025, "SEM_RESULTADO_INEP", enquadramento: null), Condicao(2024, "INSCRICAO_DE_OUTRA_EDICAO", EnquadramentoDaModalidade("AMPLA"))),
            Alternativa(Condicao(2023, "SEM_RESULTADO_INEP", enquadramento: null)));
        (_, Guid faseId) = await GravarProcessoAsync(Guid.CreateVersion7(), regra);

        RegraDeHomologacaoDasInscricoes? lida = (await RelerFaseAsync(faseId)).RegraDeHomologacao;

        lida.Should().NotBeNull();
        lida!.MesmoConteudo(regra).Should().BeTrue();
        lida.Alternativas[0].Condicoes[1].Enquadramento.Should().NotBeNull();
        lida.Alternativas[0].Condicoes[0].Enquadramento.Should().BeNull();
    }

    [Fact(DisplayName = "Fase sem regra grava a coluna nula e lê sem regra")]
    public async Task FaseSemRegra_GravaNuloELeSemRegra()
    {
        (_, Guid faseId) = await GravarProcessoAsync(Guid.CreateVersion7(), regra: null);

        (await RelerFaseAsync(faseId)).RegraDeHomologacao.Should().BeNull();
    }

    [Fact(DisplayName = "Regra sem alternativa grava e lê como a fase que homologa toda inscrição enviada")]
    public async Task RegraSemAlternativa_GravaELeSemAlternativa()
    {
        (_, Guid faseId) = await GravarProcessoAsync(Guid.CreateVersion7(), Regra(ResultadoPreliminar));

        RegraDeHomologacaoDasInscricoes? lida = (await RelerFaseAsync(faseId)).RegraDeHomologacao;

        lida.Should().NotBeNull();
        lida!.HomologaTodaInscricaoEnviada.Should().BeTrue();
        lida.ResultadoAtoCodigo.Should().Be(ResultadoPreliminar);
    }

    [Fact(DisplayName = "Redefinir o cronograma com outra regra altera a fase rastreada e a mudança persiste")]
    public async Task RedefinirCronograma_TrocaARegraDaFaseRastreada()
    {
        Guid origemId = Guid.CreateVersion7();
        (Guid processoId, Guid faseId) = await GravarProcessoAsync(origemId, Regra(ResultadoPreliminar, Alternativa(Condicao(2025, "SEM_RESULTADO_INEP", null))));
        RegraDeHomologacaoDasInscricoes nova = Regra(ResultadoPreliminar, Alternativa(Condicao(2025, "SEM_RESULTADO_INEP", EnquadramentoDaModalidade("AMPLA"))));

        await RedefinirCronogramaAsync(processoId, origemId, nova);

        (await RelerFaseAsync(faseId)).RegraDeHomologacao!.MesmoConteudo(nova).Should().BeTrue();
    }

    [Fact(DisplayName = "Redefinir o cronograma sem regra remove a regra da fase rastreada e a mudança persiste")]
    public async Task RedefinirCronograma_SemRegra_RemoveARegra()
    {
        Guid origemId = Guid.CreateVersion7();
        (Guid processoId, Guid faseId) = await GravarProcessoAsync(origemId, Regra(ResultadoPreliminar, Alternativa(Condicao(2025, "SEM_RESULTADO_INEP", null))));

        await RedefinirCronogramaAsync(processoId, origemId, regra: null);

        (await RelerFaseAsync(faseId)).RegraDeHomologacao.Should().BeNull();
    }

    [Fact(DisplayName = "A coluna da regra é jsonb, aceita nulo e documenta o propósito")]
    public async Task ColunaDaRegra_EJsonbNulavelEComentada()
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();

        (string tipo, string anulavel, string? comentario) = await ColunaAsync(db);

        tipo.Should().Be("jsonb");
        anulavel.Should().Be("YES");
        comentario.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<(string Tipo, string Anulavel, string? Comentario)> ColunaAsync(UniPlusDbContext db)
    {
        await using System.Data.Common.DbCommand comando = db.Database.GetDbConnection().CreateCommand();
        comando.CommandText = """
            select c.data_type, c.is_nullable, col_description(format('%I.%I', c.table_schema, c.table_name)::regclass, c.ordinal_position)
            from information_schema.columns c
            where c.table_name = 'fases_cronograma' and c.column_name = 'regra_de_homologacao'
            """;
        await db.Database.OpenConnectionAsync();
        await using System.Data.Common.DbDataReader leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue("a tabela da fase tem a coluna da regra");
        return (leitor.GetString(0), leitor.GetString(1), leitor.IsDBNull(2) ? null : leitor.GetString(2));
    }

    private static RegraDeHomologacaoDasInscricoes Regra(string? resultadoAtoCodigo, params AlternativaDeCondicoes[] alternativas) =>
        RegraDeHomologacaoDasInscricoes.Declarar(resultadoAtoCodigo, alternativas);

    private static AlternativaDeCondicoes Alternativa(params CondicaoDeHomologacao[] condicoes) =>
        AlternativaDeCondicoes.Declarar(condicoes).Value!;

    private static CondicaoDeHomologacao Condicao(int edicao, string? motivo, PredicadoDnf? enquadramento) =>
        CondicaoDeHomologacao.Declarar(TipoDoRetornoDoInep, edicao, motivo, enquadramento).Value!;

    private static PredicadoDnf EnquadramentoDaModalidade(string modalidade) => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar("MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement(modalidade)).Value!)]).Value!;

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        $"Certame de regra de homologação {Guid.CreateVersion7()}",
        TiposDeProcessoDeTeste.SiSU,
        OrigemCandidatos.InscricaoPropria,
        Guid.CreateVersion7(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        IdentificadoresDeTeste.Novo());

    private static FaseCronograma NovaFase(Guid origemId, RegraDeHomologacaoDasInscricoes? regra) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(origemId, "HOMOLOGACAO_INSCRICOES", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: ProdutosDoResultado(regra), EmiteParecerIndividual: false, RegraRecurso: null, TiposDeDecisao: [], RegraDeHomologacao: regra),
        bancasRequeridas: []).Value!;

    private static ProdutoDaFase[] ProdutosDoResultado(RegraDeHomologacaoDasInscricoes? regra) =>
        regra?.ResultadoAtoCodigo is { } resultado
            ? [ProdutoDaFase.Criar(resultado, PapelProdutoFase.Preliminar), ProdutoDaFase.Criar(resultado, PapelProdutoFase.Definitivo)]
            : [];

    private async Task<(Guid ProcessoId, Guid FaseId)> GravarProcessoAsync(Guid origemId, RegraDeHomologacaoDasInscricoes? regra)
    {
        await using UniPlusDbContext escrita = fixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCronogramaFases([NovaFase(origemId, regra)], []).IsSuccess.Should().BeTrue();
        escrita.ProcessosSeletivos.Add(processo);
        await escrita.SaveChangesAsync();
        return (processo.Id, processo.CronogramaFases.Single().Id);
    }

    private async Task RedefinirCronogramaAsync(Guid processoId, Guid origemId, RegraDeHomologacaoDasInscricoes? regra)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = await db.ProcessosSeletivos
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);
        processo.DefinirCronogramaFases([NovaFase(origemId, regra)], []).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private async Task<FaseCronograma> RelerFaseAsync(Guid faseId)
    {
        await using UniPlusDbContext conferencia = fixture.CreateDbContext();
        return await conferencia.Set<FaseCronograma>().FirstAsync(f => f.Id == faseId);
    }
}
