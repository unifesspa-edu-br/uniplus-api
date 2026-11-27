namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O modelo do EF enxerga os tipos de decisão da fase e da etapa pelo campo do domínio, e o
/// rastreamento percebe a lista alterada no lugar. Não abre conexão: só o modelo e o rastreador.
/// </summary>
public sealed class TiposDeDecisaoNoRastreamentoTests
{
    private const string Campo = "_tiposDeDecisao";

    [Theory(DisplayName = "O modelo mapeia os tipos de decisão no campo, na coluna text[] com default vazio")]
    [InlineData(typeof(FaseCronograma))]
    [InlineData(typeof(EtapaProcesso))]
    public void Modelo_MapeiaOCampoNaColuna(Type entidade)
    {
        using MonolitoDbContext contexto = NovoContextoSemConexao();

        IProperty? propriedade = contexto.Model.FindEntityType(entidade)!.FindProperty(Campo);

        propriedade.Should().NotBeNull();
        propriedade!.IsNullable.Should().BeFalse();
        propriedade.GetColumnName().Should().Be("tipos_de_decisao");
        propriedade.GetColumnType().Should().Be("text[]");
        propriedade.GetDefaultValueSql().Should().Be("'{}'::text[]");
        contexto.Model.FindEntityType(entidade)!.FindProperty(nameof(FaseCronograma.TiposDeDecisao))
            .Should().BeNull("a propriedade pública é somente leitura e não é mapeada");
    }

    [Fact(DisplayName = "Redefinir o cronograma com outros tipos reconcilia a fase rastreada e marca a coluna como modificada")]
    public void FaseRastreada_RedefinirCronograma_MarcaAColunaModificada()
    {
        using MonolitoDbContext contexto = NovoContextoSemConexao();
        Guid origemId = Guid.CreateVersion7();
        ProcessoSeletivo processo = NovoProcessoComFase(origemId, ["ANALISE_DOCUMENTAL"]);
        contexto.Attach(processo);
        FaseCronograma rastreada = processo.CronogramaFases.Single();

        processo.DefinirCronogramaFases([NovaFase(origemId, ["CLASSIFICACAO"])], [])
            .IsSuccess.Should().BeTrue();
        contexto.ChangeTracker.DetectChanges();

        processo.CronogramaFases.Single().Should().BeSameAs(rastreada);
        contexto.Entry(rastreada).Property(Campo).IsModified.Should().BeTrue();
        rastreada.TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.Classificacao);
    }

    [Fact(DisplayName = "Alterar os tipos da etapa rastreada no lugar marca a coluna como modificada")]
    public void EtapaRastreada_AlterarNoLugar_MarcaAColunaModificada()
    {
        using MonolitoDbContext contexto = NovoContextoSemConexao();
        EtapaProcesso etapa = NovaEtapa(["ANALISE_DOCUMENTAL"]);
        contexto.Attach(etapa);

        etapa.DefinirTiposDeDecisao(["CLASSIFICACAO", "HETEROIDENTIFICACAO"]).IsSuccess.Should().BeTrue();
        contexto.ChangeTracker.DetectChanges();

        contexto.Entry(etapa).Property(Campo).IsModified.Should().BeTrue();
    }

    [Fact(DisplayName = "Regravar a etapa rastreada com os mesmos tipos não marca a coluna como modificada")]
    public void EtapaRastreada_MesmosTipos_NaoMarcaAColuna()
    {
        using MonolitoDbContext contexto = NovoContextoSemConexao();
        EtapaProcesso etapa = NovaEtapa(["ANALISE_DOCUMENTAL"]);
        contexto.Attach(etapa);

        etapa.DefinirTiposDeDecisao(["ANALISE_DOCUMENTAL"]).IsSuccess.Should().BeTrue();
        contexto.ChangeTracker.DetectChanges();

        contexto.Entry(etapa).Property(Campo).IsModified.Should().BeFalse();
    }

    private static MonolitoDbContext NovoContextoSemConexao() =>
        new(new DbContextOptionsBuilder<MonolitoDbContext>()
            .UseNpgsql("Host=sem-conexao;Database=modelo")
            .UseSnakeCaseNamingConvention()
            .Options);

    private static ProcessoSeletivo NovoProcessoComFase(Guid origemId, IReadOnlyList<string?> tipos)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCronogramaFases([NovaFase(origemId, tipos)], [])
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        $"Certame de rastreamento {Guid.CreateVersion7()}",
        TiposDeProcessoDeTeste.Sisu,
        OrigemCandidatos.InscricaoPropria,
        Guid.CreateVersion7(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        IdentificadoresDeTeste.Novo());

    private static FaseCronograma NovaFase(Guid origemId, IReadOnlyList<string?> tipos) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensDoTipoDeFaseDeTeste.Da(origemId, "ANALISE_DOCUMENTAL", OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null, TiposDeDecisao: tipos),
        bancasRequeridas: []).Value!;

    private static EtapaProcesso NovaEtapa(IReadOnlyList<string?> tipos)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Etapa de teste", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirTiposDeDecisao(tipos).IsSuccess.Should().BeTrue();
        return etapa;
    }
}
