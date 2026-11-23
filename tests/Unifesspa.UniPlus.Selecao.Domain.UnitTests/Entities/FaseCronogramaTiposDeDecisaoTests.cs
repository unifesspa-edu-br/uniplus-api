namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A fase declara por conta própria os tipos de decisão que produz: confere na criação, guarda a cópia
/// canônica, repõe a declaração ao atualizar e nunca expõe a lista que guarda.
/// </summary>
public sealed class FaseCronogramaTiposDeDecisaoTests
{
    private const string Classificacao = "CLASSIFICACAO";
    private const string Isencao = "ISENCAO_DA_TAXA";
    private const string Heteroidentificacao = "HETEROIDENTIFICACAO";

    private static OrigemCanonicaDaFase Origem(OrigemDataFase origemData = OrigemDataFase.NaExecucao) => new(
        Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", origemData,
        AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false);

    private static DecisoesDeCronogramaDaFase Cronograma() => new(1, null, null, UnidadesDeTeste.Ceps);

    private static Result<FaseCronograma> Criar(IReadOnlyList<string?>? tipos, OrigemDataFase origemData = OrigemDataFase.NaExecucao) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!, Origem(origemData), Cronograma(), new PublicacoesDaFase([], false, null, tipos), []);

    private static FaseCronograma FaseCom(params string?[] tipos) => Criar(tipos).Value!;

    private static FaseCronograma Congelada() => FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        Guid.CreateVersion7(), Origem(), Cronograma(), new PublicacoesDaFase([], false, null), []);

    private static (string? Campo, string Codigo)[] Recusas(Result<FaseCronograma> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "A fase que não declara tipo, ou declara a lista vazia, guarda a declaração vazia")]
    public void Criar_SemDeclarar_FicaSemTipos()
    {
        Result<FaseCronograma> semLista = Criar(null);
        Result<FaseCronograma> listaVazia = Criar([]);

        semLista.IsSuccess.Should().BeTrue(semLista.Error?.Message);
        listaVazia.IsSuccess.Should().BeTrue(listaVazia.Error?.Message);
        semLista.Value!.TiposDeDecisao.Should().BeEmpty();
        listaVazia.Value!.TiposDeDecisao.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase guarda os tipos declarados em ordem canônica, qualquer que seja a ordem de entrada")]
    public void Criar_ComTipos_GuardaEmOrdemCanonica()
    {
        FaseCronograma fase = FaseCom(Isencao, Classificacao, Heteroidentificacao);

        fase.TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.Heteroidentificacao, TipoMotivoDeDecisao.IsencaoDaTaxa);
    }

    [Fact(DisplayName = "Tipo fora do vocabulário recusa a criação no campo e na posição do tipo")]
    public void Criar_TipoInvalido_RecusaNaPosicao()
    {
        Recusas(Criar([Classificacao, "RECURSO_DA_BANCA"])).Should().Equal(
            ("tiposDeDecisao[1]", MotivoDeDecisaoErrorCodes.TipoInvalido));
    }

    [Fact(DisplayName = "Tipo repetido na mesma fase recusa a criação na posição do repetido")]
    public void Criar_TipoRepetido_RecusaNaPosicaoDoRepetido()
    {
        Recusas(Criar([Classificacao, Isencao, Classificacao])).Should().Equal(
            ("tiposDeDecisao[2]", MotivoDeDecisaoErrorCodes.TipoDeDecisaoDuplicado));
    }

    [Fact(DisplayName = "A recusa dos tipos acumula com as demais violações da declaração da fase")]
    public void Criar_TiposInvalidosEJanelaAusente_AcumulaAsDuasRecusas()
    {
        Recusas(Criar([null], OrigemDataFase.NoEdital)).Should().BeEquivalentTo(
            [
                ((string?)null, "FaseCronograma.JanelaObrigatoriaEmDataNoEdital"),
                ("tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ]);
    }

    [Fact(DisplayName = "A fase guarda cópia: alterar a lista de entrada depois não muda a declaração")]
    public void Criar_ListaDeEntradaAlteradaDepois_NaoMudaADeclaracao()
    {
        List<string?> entrada = [Classificacao];
        FaseCronograma fase = Criar(entrada).Value!;

        entrada.Add(Isencao);
        entrada[0] = Heteroidentificacao;

        fase.TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.Classificacao);
    }

    [Fact(DisplayName = "A lista devolvida pela fase não é alterável nem por conversão de tipo")]
    public void TiposDeDecisao_Devolvida_NaoEAlteravelPorConversao()
    {
        FaseCronograma fase = FaseCom(Classificacao);

        fase.TiposDeDecisao.Should().NotBeAssignableTo<List<TipoMotivoDeDecisao>>();
        IList<TipoMotivoDeDecisao> comoLista = fase.TiposDeDecisao.Should().BeAssignableTo<IList<TipoMotivoDeDecisao>>().Subject;
        comoLista.IsReadOnly.Should().BeTrue();
        Action alterar = () => comoLista.Add(TipoMotivoDeDecisao.IsencaoDaTaxa);
        alterar.Should().Throw<NotSupportedException>();
        fase.TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.Classificacao);
    }

    [Fact(DisplayName = "Reidratar a fase do envelope deixa a declaração vazia")]
    public void Reidratar_ComoOEnvelopeDevolve_FicaSemTipos()
    {
        Congelada().TiposDeDecisao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Atualizar a fase copia os tipos da fase de origem, trocando a declaração anterior")]
    public void AtualizarDe_FaseComOutrosTipos_SubstituiADeclaracao()
    {
        FaseCronograma viva = FaseCom(Classificacao);

        viva.AtualizarDe(FaseCom(Isencao, Heteroidentificacao), []);

        viva.TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.Heteroidentificacao, TipoMotivoDeDecisao.IsencaoDaTaxa);
    }

    [Fact(DisplayName = "Atualizar a fase com origem sem tipos limpa a declaração")]
    public void AtualizarDe_FaseSemTipos_LimpaADeclaracao()
    {
        FaseCronograma viva = FaseCom(Classificacao);

        viva.AtualizarDe(FaseCom(), []);

        viva.TiposDeDecisao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Atualizar a fase copia: mudar a origem depois não muda a fase atualizada")]
    public void AtualizarDe_OrigemAlteradaDepois_NaoMudaAFaseAtualizada()
    {
        FaseCronograma viva = FaseCom(Classificacao);
        FaseCronograma origem = FaseCom(Isencao);
        viva.AtualizarDe(origem, []);

        origem.AtualizarDe(FaseCom(Heteroidentificacao), []);

        viva.TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.IsencaoDaTaxa);
    }

    [Fact(DisplayName = "Reposição a partir de fase congelada sem tipos ainda não preserva os tipos de decisão declarados")]
    public void Reposicao_AindaNaoPreservaOsTiposDeDecisaoDeclarados()
    {
        FaseCronograma viva = FaseCom(Classificacao, Isencao);

        viva.AtualizarDe(Congelada(), []);

        viva.TiposDeDecisao.Should().BeEmpty();
    }
}
