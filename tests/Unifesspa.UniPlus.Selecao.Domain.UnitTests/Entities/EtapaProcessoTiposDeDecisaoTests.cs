namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A etapa declara por conta própria os tipos de decisão que produz, pela operação pública do domínio:
/// confere tudo antes de guardar a cópia canônica, não muda quando recusa e nunca expõe a lista que
/// guarda.
/// </summary>
public sealed class EtapaProcessoTiposDeDecisaoTests
{
    private const string Classificacao = TipoMotivoDeDecisaoCodigo.Classificacao;
    private const string Isencao = TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa;
    private const string Heteroidentificacao = TipoMotivoDeDecisaoCodigo.Heteroidentificacao;

    private static IdentificacaoDaEtapa Identificacao() => new(
        Nome: "Prova Objetiva",
        Carater: CaraterEtapa.Ambas,
        TipoEtapa: TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva",
            admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
        Ordem: null,
        FaseCodigo: null);

    private static DeclaracaoDaAvaliacaoDaEtapa Avaliacao() => new(
        Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null,
        FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null);

    private static EtapaProcesso Etapa() => EtapaProcesso.Criar(Identificacao(), Avaliacao()).Value!;

    private static EtapaProcesso EtapaCom(params string?[] tipos)
    {
        EtapaProcesso etapa = Etapa();
        etapa.DefinirTiposDeDecisao(tipos).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static EtapaProcesso Congelada() =>
        EtapaProcesso.Reidratar(Guid.CreateVersion7(), Identificacao(), Avaliacao(), itens: null);

    private static (string? Campo, string Codigo)[] Recusas(Result resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "A etapa criada ou reidratada nasce sem tipo declarado")]
    public void CriarEReidratar_NascemSemTipos()
    {
        Etapa().TiposDeDecisao.Should().BeEmpty();
        Congelada().TiposDeDecisao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Declaração ausente ou vazia limpa os tipos que a etapa tinha")]
    public void DefinirTiposDeDecisao_AusenteOuVazia_LimpaADeclaracao()
    {
        EtapaProcesso etapa = EtapaCom(Classificacao);

        etapa.DefinirTiposDeDecisao(null).IsSuccess.Should().BeTrue();
        etapa.TiposDeDecisao.Should().BeEmpty();
        etapa.DefinirTiposDeDecisao([Isencao]).IsSuccess.Should().BeTrue();
        etapa.DefinirTiposDeDecisao([]).IsSuccess.Should().BeTrue();
        etapa.TiposDeDecisao.Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa guarda os tipos declarados em ordem canônica, qualquer que seja a ordem de entrada")]
    public void DefinirTiposDeDecisao_VariosTipos_GuardaEmOrdemCanonica()
    {
        EtapaProcesso etapa = EtapaCom(Isencao, Classificacao, Heteroidentificacao);

        etapa.TiposDeDecisao.Should().Equal(Classificacao, Heteroidentificacao, Isencao);
    }

    [Fact(DisplayName = "Tipos inválidos e repetidos acumulam na recusa, por posição de entrada")]
    public void DefinirTiposDeDecisao_InvalidoERepetido_AcumulaNaRecusa()
    {
        Result resultado = Etapa().DefinirTiposDeDecisao([null, Classificacao, Classificacao, "RECURSO_DA_BANCA"]);

        Recusas(resultado).Should().Equal(
            ("tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("tiposDeDecisao[2]", MotivoDeDecisaoErrorCodes.TipoDeDecisaoDuplicado),
            ("tiposDeDecisao[3]", MotivoDeDecisaoErrorCodes.TipoInvalido));
    }

    [Fact(DisplayName = "Declaração recusada não muda os tipos que a etapa já tinha")]
    public void DefinirTiposDeDecisao_Recusada_MantemADeclaracaoAnterior()
    {
        EtapaProcesso etapa = EtapaCom(Classificacao);

        etapa.DefinirTiposDeDecisao([Isencao, "RECURSO_DA_BANCA"]).IsFailure.Should().BeTrue();

        etapa.TiposDeDecisao.Should().Equal(Classificacao);
    }

    [Fact(DisplayName = "A etapa guarda cópia: alterar a lista de entrada depois não muda a declaração")]
    public void DefinirTiposDeDecisao_ListaDeEntradaAlteradaDepois_NaoMudaADeclaracao()
    {
        EtapaProcesso etapa = Etapa();
        List<string?> entrada = [Classificacao];
        etapa.DefinirTiposDeDecisao(entrada);

        entrada.Add(Isencao);
        entrada[0] = Heteroidentificacao;

        etapa.TiposDeDecisao.Should().Equal(Classificacao);
    }

    [Fact(DisplayName = "A lista devolvida pela etapa não é alterável nem por conversão de tipo")]
    public void TiposDeDecisao_Devolvida_NaoEAlteravelPorConversao()
    {
        EtapaProcesso etapa = EtapaCom(Classificacao);

        etapa.TiposDeDecisao.Should().NotBeAssignableTo<List<string>>();
        IList<string> comoLista = etapa.TiposDeDecisao.Should().BeAssignableTo<IList<string>>().Subject;
        comoLista.IsReadOnly.Should().BeTrue();
        Action alterar = () => comoLista.Add(Isencao);
        alterar.Should().Throw<NotSupportedException>();
        etapa.TiposDeDecisao.Should().Equal(Classificacao);
    }

    [Fact(DisplayName = "Repor a etapa copia os tipos da etapa de origem, trocando a declaração anterior")]
    public void ReporDe_EtapaComTipos_SubstituiADeclaracao()
    {
        EtapaProcesso viva = EtapaCom(Classificacao);

        viva.ReporDe(EtapaCom(Isencao, Heteroidentificacao));

        viva.TiposDeDecisao.Should().Equal(Heteroidentificacao, Isencao);
    }

    [Fact(DisplayName = "Repor a etapa copia: mudar a origem depois não muda a etapa reposta")]
    public void ReporDe_OrigemAlteradaDepois_NaoMudaAEtapaReposta()
    {
        EtapaProcesso viva = Etapa();
        EtapaProcesso origem = EtapaCom(Isencao);
        viva.ReporDe(origem);

        origem.DefinirTiposDeDecisao([Heteroidentificacao]);

        viva.TiposDeDecisao.Should().Equal(Isencao);
    }

    [Fact(DisplayName = "Reposição a partir de etapa congelada sem tipos ainda não preserva os tipos de decisão declarados")]
    public void Reposicao_AindaNaoPreservaOsTiposDeDecisaoDeclarados()
    {
        EtapaProcesso viva = EtapaCom(Classificacao, Isencao);

        viva.ReporDe(Congelada());

        viva.TiposDeDecisao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Fase e etapa podem declarar o mesmo tipo, cada uma com o que é seu")]
    public void MesmoTipoNaFaseENaEtapa_EAceito()
    {
        FaseCronograma fase = FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", OrigemDataFase.NaExecucao,
                AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(1, null, null, null, UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], false, null, [Classificacao]),
            []).Value!;
        EtapaProcesso etapa = EtapaCom(Classificacao);

        fase.TiposDeDecisao.Should().Equal(Classificacao);
        etapa.TiposDeDecisao.Should().Equal(Classificacao);
    }
}
