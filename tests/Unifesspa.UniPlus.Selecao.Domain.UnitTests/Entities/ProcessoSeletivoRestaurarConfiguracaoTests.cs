namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Reposição da configuração congelada: uma restauração recusada não pode alterar nada do
/// agregado (tudo ou nada), e a identidade das etapas reconciliadas segue a regra de
/// preservação de <c>Id</c> por referência de negócio (Story #859 critério de aceite sobre
/// restauração tudo-ou-nada; ADR-0110 decisão sobre identidade na reidratação).
/// </summary>
/// <remarks>
/// A propriedade central aqui é <b>tudo ou nada</b>: uma restauração que falha não pode
/// deixar o agregado meio-reposto. Se a validação fosse feita dimensão a dimensão,
/// enquanto se aplica, um grafo que falhasse na <b>última</b> checagem já teria trocado
/// etapas e distribuição — e o certame ficaria numa configuração que <b>nunca existiu</b>:
/// nem a viva, nem a congelada.
/// </remarks>
public sealed partial class ProcessoSeletivoRestaurarConfiguracaoTests
{
    private static readonly Guid EtapaOriginal = new("aaaa0000-0000-4000-8000-000000000001");
    private static readonly Guid EtapaCongelada = new("aaaa0000-0000-4000-8000-000000000002");

    [Fact(DisplayName = "Uma restauração que falha numa validação TARDIA não altera NADA")]
    public void RestauracaoQueFalha_NaoAlteraEstado()
    {
        // A falha é TARDIA de propósito: a checagem de classificação (INV-B4, EtapaRef de
        // eliminação) só roda depois de etapas/distribuição/desempate já terem sido
        // percorridos em ValidarGrafo. Um caso trivial (etapas vazias) passaria mesmo numa
        // implementação que aplicasse etapas e distribuição antes de chegar à classificação
        // — e não testaria nada. Desde #850, a invariante ENEM×eliminação não serve mais
        // para esta prova: ela é validada dentro de ConfiguracaoClassificacao.Criar, ANTES
        // de o grafo sequer existir — usar um EtapaRef órfão é o que sobrevive na raiz
        // (depende de _etapas, fora do alcance da configuração isolada).
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        VersaoConfiguracao versao = VersaoDo(processo);

        Estado antes = Estado.De(processo);

        GrafoConfiguracao invalido = Grafo(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva)],
            eliminacoes: [
                RegraEliminacao.Criar(
                    Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, 'e'),
                    new ArgsElimNotaMinimaEtapa(Guid.NewGuid(), 4m)).Value!,
            ]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefEliminacaoInexistente");

        Estado.De(processo).Should().BeEquivalentTo(antes,
            "a validação acontece INTEIRA antes de qualquer escrita. Se a reposição aplicasse dimensão a dimensão, " +
            "este grafo já teria trocado etapas e distribuição antes de falhar na classificação — e o certame " +
            "ficaria numa configuração que nunca existiu.");
    }

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM sob classificação que não é ENEM é recusado, como na gravação")]
    public void Restaurar_EtapaNotaEnemSemClassificacaoEnem_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), Grafo(etapas: [EtapaNotaEnemCongelada(EtapaCongelada, 1)]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "Restaurar eliminação por ausência que cita etapa fora do grafo é recusado, como na gravação")]
    public void Restaurar_AusenciaNaEtapaInexistente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(eliminacoes: [RegraEliminacao.Criar(
                Regra(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, 'e'),
                new ArgsElimAusenciaNaEtapa(Guid.NewGuid())).Value!]));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefEliminacaoInexistente");
    }

    [Fact(DisplayName = "Restaurar eliminação por ausência que cita a etapa do ENEM é recusado, como na gravação")]
    public void Restaurar_AusenciaNaEtapaDoEnem_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(
                etapas: [EtapaNotaEnemCongelada(EtapaCongelada, 1)],
                classificacao: ClassificacaoEnemMediaPonderada([RegraEliminacao.Criar(
                    Regra(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, 'e'),
                    new ArgsElimAusenciaNaEtapa(EtapaCongelada)).Value!])));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AusenciaNaEtapaDoEnem");
    }

    [Fact(DisplayName = "Restaurar duas etapas de nota do ENEM é recusado, como na gravação")]
    public void Restaurar_DuasEtapasNotaEnem_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(
                etapas: [EtapaNotaEnemCongelada(EtapaCongelada, 1), EtapaNotaEnemCongelada(EtapaOriginal, 2)],
                classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemDuplicada");
    }

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM com banca é recusado, como na gravação")]
    public void Restaurar_EtapaNotaEnemComBanca_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        EtapaProcesso etapa = EtapaNotaEnemCongelada(EtapaCongelada, 1);
        etapa.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(etapas: [etapa], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComBanca");
    }

    [Theory(DisplayName = "Restaurar etapa de nota do ENEM fora da média é recusado, antes da regra geral, como na gravação")]
    [InlineData(CaraterEtapa.Eliminatoria)]
    [InlineData(CaraterEtapa.Classificatoria)]
    [InlineData(CaraterEtapa.Ambas)]
    public void Restaurar_EtapaNotaEnemForaDaMedia_Recusa(CaraterEtapa carater)
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        EtapaProcesso etapa = EtapaProcesso.Reidratar(EtapaCongelada, "Nota do ENEM", carater, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, null, null, 1);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(etapas: [etapa], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");
    }

    [Fact(DisplayName = "Restaurar grafo com mais opções de curso que ofertas é recusado, como na gravação, e não altera o agregado")]
    public void Restaurar_MaisOpcoesQueOfertas_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        Estado antes = Estado.De(processo);

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), Grafo(numeroDeOpcoesDeCurso: 2));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.OpcoesDeCursoAcimaDasOfertas);
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Theory(DisplayName = "Restaurar grafo sem número de opções, ou fora de 1 e 2, é recusado, como na declaração")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(3)]
    public void Restaurar_NumeroDeOpcoesForaDaFaixa_Recusa(int? numero)
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        Estado antes = Estado.De(processo);

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), Grafo(numeroDeOpcoesDeCurso: numero));

        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.OpcoesDeCursoInvalido);
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Restaurar repõe o número de opções congelado, sobrepondo o que a sessão de retificação declarou")]
    public void Restaurar_ReponhaONumeroCongelado()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        // Estado que a sessão deixou: o número vivo difere do congelado.
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.NumeroDeOpcoesDeCurso))!.SetValue(processo, 2);

        processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), Grafo(numeroDeOpcoesDeCurso: 1)).IsSuccess.Should().BeTrue();

        processo.NumeroDeOpcoesDeCurso.Should().Be(1);
    }

    [Fact(DisplayName = "Restaurar deriva a declaração do bônus da versão publicada: com bônus, aplica; sem, não aplica")]
    public void Restaurar_DerivaADeclaracaoDoBonus()
    {
        ProcessoSeletivo semBonus = ProcessoPublicado(TipoProcesso.PSIQ);
        semBonus.RestaurarConfiguracaoCongelada(VersaoDo(semBonus), Grafo()).IsSuccess.Should().BeTrue();
        semBonus.AplicaBonusRegional.Should().BeFalse();

        ProcessoSeletivo comBonus = ProcessoPublicado(TipoProcesso.PSIQ);
        ConfiguracaoBonusRegional bonus = ConfiguracaoBonusRegional.Criar(
            Regra(RegraBonusCodigo.Multiplicativo, 'a'), 1.20m, null, null, Guid.NewGuid(), "PORTARIA",
            "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1504208", "Marabá", "PA")]).Value!;
        comBonus.RestaurarConfiguracaoCongelada(VersaoDo(comBonus), GrafoComBonus(bonus)).IsSuccess.Should().BeTrue();
        comBonus.AplicaBonusRegional.Should().BeTrue();
        comBonus.BonusRegional.Should().NotBeNull();
    }

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM com parecer e recurso por ciência é aceito, como na gravação")]
    public void Restaurar_EtapaNotaEnemComParecerERecursoPorCiencia_Aceita()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        EtapaProcesso etapa = EtapaNotaEnemCongelada(EtapaCongelada, 1);
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        etapa.DefinirRecursos([RecursoDaEtapaDeTeste(AncoraDoRecurso.CienciaIndividual, Guid.Empty)]).IsSuccess.Should().BeTrue();

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(etapas: [etapa], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM com produto ou recurso em ato é recusado, como na gravação")]
    public void Restaurar_EtapaNotaEnemComProdutoOuRecursoEmAto_Recusa()
    {
        EtapaProcesso comProduto = EtapaNotaEnemCongelada(EtapaCongelada, 1);
        comProduto.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();
        EtapaProcesso comRecursoEmAto = EtapaNotaEnemCongelada(EtapaCongelada, 1);
        comRecursoEmAto.DefinirRecursos([RecursoDaEtapaDeTeste(AncoraDoRecurso.AtoPublicado, Guid.CreateVersion7())]).IsSuccess.Should().BeTrue();

        foreach (EtapaProcesso etapa in new[] { comProduto, comRecursoEmAto })
        {
            ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
            Result resultado = processo.RestaurarConfiguracaoCongelada(
                VersaoDo(processo),
                Grafo(etapas: [etapa], classificacao: ClassificacaoEnemMediaPonderada()));

            resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto");
        }
    }

    private static RecursoDaEtapa RecursoDaEtapaDeTeste(AncoraDoRecurso ancora, Guid produtoAncoraId) =>
        RecursoDaEtapa.Criar(
            ancora,
            ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!,
            new ArgsRegraPrazoRecurso(48m, UnidadePrazo.Horas, null, null, false, null, null),
            produtoAncoraId).Value!;

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM sob classificação ENEM pela média ponderada é aceito")]
    public void Restaurar_EtapaNotaEnemSobClassificacaoEnemMediaPonderada_Aceita()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(etapas: [EtapaNotaEnemCongelada(EtapaCongelada, 1)], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().ContainSingle().Which.DeclaraNotaDoEnem.Should().BeTrue();
    }

    [Fact(DisplayName = "Restaurar desempate por área do ENEM sob classificação sem quadro de pesos é recusado, como na gravação")]
    public void Restaurar_DesempatePorAreaSemQuadro_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        Estado antes = Estado.De(processo);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(criterios: [DesempatePorArea("REDACAO")]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Restaurar desempate por área que cita área fora do quadro restaurado é recusado, como na gravação")]
    public void Restaurar_DesempatePorAreaForaDoQuadro_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(criterios: [DesempatePorArea("REDACAO", "FISICA")], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemForaDoQuadro");
    }

    [Fact(DisplayName = "Restaurar desempate por área com áreas do quadro restaurado é aceito")]
    public void Restaurar_DesempatePorAreaDoQuadro_Aceita()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(criterios: [DesempatePorArea("REDACAO", "MATEMATICA")], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CriteriosDesempate.Should().ContainSingle()
            .Which.Args.Should().BeOfType<ArgsDesempateMaiorNotaAreaEnem>()
            .Which.Areas.Should().Equal("REDACAO", "MATEMATICA");
    }

    [Fact(DisplayName = "Restaurar dois critérios de desempate que citam a mesma área é recusado, como na gravação")]
    public void Restaurar_AreaCitadaPorDoisCriterios_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        Estado antes = Estado.De(processo);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(
                criterios: [DesempatePorArea("REDACAO"), DesempatePorArea(2, "MATEMATICA", "REDACAO")],
                classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Restaurar formulário com mais itens que o teto é recusado, como na gravação")]
    public void Restaurar_ItensAcimaDoTeto_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        Estado antes = Estado.De(processo);
        FatoColetado[] itens = [.. Enumerable.Range(0, FormaDoItem.MaximoDeItens + 1).Select(static i => FatoColetado.Criar(
            $"FATO_{i}", i, "Campo", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, finalidade: FinalidadeFormulario.Inscricao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!)];

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), Grafo(fatosColetados: itens));

        resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }
}
