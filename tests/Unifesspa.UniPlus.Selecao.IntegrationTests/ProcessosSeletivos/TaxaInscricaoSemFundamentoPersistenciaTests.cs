namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Issue #1310: a fábrica recusa <c>Cobra = true</c> sem fundamento de isenção, mas essa
/// combinação é MATERIALIZÁVEL — o EF hidrata <see cref="ConfiguracaoTaxaInscricao"/> direto da
/// coluna <c>jsonb</c>, sem passar por <c>Criar</c>. Configuração gravada antes da regra volta
/// ao agregado nesse estado, e sem o item de conformidade
/// <c>taxa_inscricao_sem_fundamento_de_isencao</c> ela publicaria.
/// </summary>
/// <remarks>
/// A prova precisa de Postgres real e de SQL cru: nenhum caminho do domínio produz o estado que
/// se está defendendo. Semear pelo agregado e adulterar a linha depois é o que reproduz a
/// hidratação de dado legado — o mesmo raciocínio dos testes de backfill.
/// </remarks>
public sealed class TaxaInscricaoSemFundamentoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private readonly ProcessoSeletivoDbFixture _fixture;

    public TaxaInscricaoSemFundamentoPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    // O estado gravado por quem configurou a taxa antes de a regra existir: a fábrica recusa produzi-lo hoje,
    // mas o EF hidrata a linha direto da coluna.
    [Fact(DisplayName = "Configuração hidratada com cobrança e lista de fundamentos vazia marca o item vermelho e recusa a publicação (issue #1310)")]
    public async Task CobraSemFundamento_ItemVermelhoEPublicacaoRecusada()
    {
        Guid processoId = await SemearProcessoQueCobraComFundamentoAsync(
            $"PS 1310 {Guid.CreateVersion7()}");
        await AdulterarFundamentosAsync(processoId, "[]");
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        processo.ConfiguracaoTaxaInscricao!.Cobra.Should().BeTrue(
            "a adulteração mexe só nos fundamentos — a cobrança continua declarada");
        AfirmarFundamentoVermelhoEPublicacaoRecusada(processo);
    }

    private static void AfirmarFundamentoVermelhoEPublicacaoRecusada(ProcessoSeletivo processo)
    {
        IReadOnlyList<ItemConformidade> checklist = ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo));

        checklist.Should().ContainSingle(i => i.Codigo == "taxa_inscricao_sem_fundamento_de_isencao")
            .Which.Ok.Should().BeFalse(
                "quem cobra reconhece ao menos um fundamento — a possibilidade de pedir isenção se " +
                "materializa nos fundamentos declarados pelo processo");
        checklist.Should().ContainSingle(i => i.Codigo == "taxa_inscricao_nao_declarada")
            .Which.Ok.Should().BeTrue("a declaração existe; o que falta é o fundamento");

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsFailure.Should().BeTrue("linha legada não pode congelar uma versão em estado que a regra proíbe");
        publicacao.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }

    [Fact(DisplayName = "Token fora do vocabulário na coluna estoura na carga do agregado — o estado inválido é alto, não silencioso (issue #1310)")]
    public async Task FundamentoForaDoVocabulario_EstouraNaCarga()
    {
        Guid processoId = await SemearProcessoQueCobraComFundamentoAsync(
            $"PS 1310 lixo {Guid.CreateVersion7()}");

        await AdulterarFundamentosAsync(processoId, """["FUNDAMENTO_INEXISTENTE"]""");

        await using UniPlusDbContext db = _fixture.CreateDbContext();

        Func<Task> carga = async () => await CarregarAsync(db, processoId);

        // É o que fecha o buraco que o item de conformidade não precisa cobrir: uma lista com
        // token desconhecido nunca chega ao checklist como "lista não vazia" que passaria a
        // contagem — o conversor da coluna recusa reserializar o sentinela e derruba a carga.
        await carga.Should().ThrowAsync<ArgumentOutOfRangeException>(
            "FundamentoIsencao.Nenhum não tem código canônico, e a persistência não inventa um");
    }

    [Fact(DisplayName = "Contraprova: configuração hidratada com cobrança e fundamento reconhecido publica normalmente (issue #1310)")]
    public async Task CobraComFundamento_PublicaNormalmente()
    {
        Guid processoId = await SemearProcessoQueCobraComFundamentoAsync(
            $"PS 1310 ok {Guid.CreateVersion7()}");

        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Should().ContainSingle(i => i.Codigo == "taxa_inscricao_sem_fundamento_de_isencao")
            .Which.Ok.Should().BeTrue();

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
    }

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        new CenarioDeCongelamento { Dados = DadosEdital.Criar("001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!, Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "integration-test-user", MotivosEscolhidos = MotivosDoProcessoDeTeste.Cobrindo(processo) });

    /// <summary>
    /// Processo estruturalmente publicável cuja taxa é declarada pelo caminho legítimo — com
    /// cobrança e um fundamento. É o único jeito de chegar à linha: a fábrica recusa gravá-la
    /// sem fundamento, então o estado legado tem de ser produzido depois, em SQL.
    /// </summary>
    private async Task<Guid> SemearProcessoQueCobraComFundamentoAsync(string nome)
    {
        await using UniPlusDbContext db = _fixture.CreateDbContext();

        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme(nome, fase: FaseConforme());

        Result<ConfiguracaoTaxaInscricao> taxa = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m,
            fundamentosCodigos: [FundamentoIsencaoCodigo.CadastroUnico]);
        taxa.IsSuccess.Should().BeTrue(taxa.Error?.Message);
        processo.DefinirTaxaInscricao(taxa.Value!).IsSuccess.Should().BeTrue();

        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();

        return processo.Id;
    }

    private async Task AdulterarFundamentosAsync(Guid processoId, string fundamentosJson)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(
AtualizacaoDosFundamentos,
            conexao);
        comando.Parameters.AddWithValue("fundamentos", fundamentosJson);
        comando.Parameters.AddWithValue("processo", processoId);

        int afetadas = await comando.ExecuteNonQueryAsync();
        afetadas.Should().Be(1, "a semeadura grava exatamente uma configuração de taxa para o processo");
    }

    private static async Task<ProcessoSeletivo> CarregarAsync(UniPlusDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.Classificacao)
            .Include(p => p.Formularios).ThenInclude(f => f.Etapas)
            .Include(p => p.Campos).ThenInclude(f => f.Precondicoes)
            .Include(p => p.AtendimentoEspecializado)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.ConfiguracaoTaxaInscricao)
            .Include(p => p.CriteriosDesempate)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos: [ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelProdutoFase.Definitivo)],
            EmiteParecerIndividual: false,
            RegraRecurso: null,
            TiposDeDecisao: [TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo(), TipoMotivoDeDecisao.HomologacaoDasInscricoes.Codigo()],
            RegraDeHomologacao: HomologacaoDasInscricoesDeTeste.Regra()),
        bancasRequeridas: []).Value!;

    private const string AtualizacaoDosFundamentos =
            """
            UPDATE selecao.configuracoes_taxa_inscricao
               SET fundamentos = @fundamentos::jsonb
             WHERE processo_seletivo_id = @processo
            """;

}
