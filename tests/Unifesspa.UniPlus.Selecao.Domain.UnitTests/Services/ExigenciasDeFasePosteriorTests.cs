namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As exigências que a fase da inscrição ainda não decide: a exigência cujo gatilho cita um fato perguntado por um
/// formulário de fase posterior à da inscrição fica bloqueada, e a de fato perguntado até a fase da inscrição, não.
/// Cada formulário pergunta um fato próprio e tem uma exigência citando esse fato.
/// </summary>
public sealed class ExigenciasDeFasePosteriorTests
{
    private const string Secao = "DADOS";

    private static readonly PapelDoFormularioDeTeste[] Papeis =
        [PapelDoFormularioDeTeste.Inscricao, PapelDoFormularioDeTeste.IsencaoTaxa, PapelDoFormularioDeTeste.Habilitacao];

    [Fact(DisplayName = "O formulário da fase de isenção anterior à inscrição é conhecido, e o da habilitação posterior fica bloqueado")]
    public void FormularioDeFaseAnterior_Conhecido_FormularioDeFasePosterior_Bloqueado()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

        PapeisBloqueadosNa(
            [isencao, inscricao, habilitacao],
            (PapelDoFormularioDeTeste.Inscricao, inscricao.Id),
            (PapelDoFormularioDeTeste.IsencaoTaxa, isencao.Id),
            (PapelDoFormularioDeTeste.Habilitacao, habilitacao.Id))
            .Should().Equal(PapelDoFormularioDeTeste.Habilitacao);
    }

    [Fact(DisplayName = "O formulário posto numa fase que só abre a isenção não é da inscrição: a fase da inscrição é a que coleta inscrição")]
    public void FormularioEmFaseQueSoAbreAIsencao_NaoDefineAFaseDaInscricao()
    {
        FaseCronograma abreAIsencao = FaseDeTeste.Criar(1, "ABERTURA_DA_ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma primeiraInscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);

        PapeisBloqueadosNa(
            [abreAIsencao, primeiraInscricao],
            (PapelDoFormularioDeTeste.Inscricao, abreAIsencao.Id),
            (PapelDoFormularioDeTeste.Habilitacao, primeiraInscricao.Id))
            .Should().Equal(PapelDoFormularioDeTeste.IsencaoTaxa);
    }

    [Fact(DisplayName = "Havendo várias fases de inscrição, vale a fase do formulário de inscrição")]
    public void VariasFasesDeInscricao_ValeAFaseDoFormulario()
    {
        FaseCronograma primeiraInscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma isencao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma segundaInscricao = FaseDeTeste.Criar(3, "INSCRICAO_COMPLEMENTAR", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(4, "HABILITACAO");

        PapeisBloqueadosNa(
            [primeiraInscricao, isencao, segundaInscricao, habilitacao],
            (PapelDoFormularioDeTeste.Inscricao, segundaInscricao.Id),
            (PapelDoFormularioDeTeste.IsencaoTaxa, isencao.Id),
            (PapelDoFormularioDeTeste.Habilitacao, habilitacao.Id))
            .Should().Equal(PapelDoFormularioDeTeste.Habilitacao);
    }

    [Fact(DisplayName = "Com formulários obrigatórios em duas fases que coletam inscrição, vale a mais tardia das duas")]
    public void FormulariosObrigatoriosEmDuasFasesDeInscricao_ValeAMaisTardia()
    {
        FaseCronograma primeiraInscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma isencao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma segundaInscricao = FaseDeTeste.Criar(3, "INSCRICAO_COMPLEMENTAR", coletaInscricao: true);

        PapeisBloqueadosNa(
            [primeiraInscricao, isencao, segundaInscricao],
            (PapelDoFormularioDeTeste.Inscricao, primeiraInscricao.Id),
            (PapelDoFormularioDeTeste.IsencaoTaxa, isencao.Id),
            (PapelDoFormularioDeTeste.Habilitacao, segundaInscricao.Id))
            .Should().BeEmpty("o formulário da isenção, na fase 2, vem antes da fase 3 da segunda inscrição");
    }

    [Fact(DisplayName = "O formulário na própria fase da inscrição, ou em outra fase na mesma posição, é conhecido")]
    public void FormularioNaMesmaPosicaoDaInscricao_Conhecido()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
        FaseCronograma naMesmaPosicao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

        PapeisBloqueadosNa(
            [inscricao, naMesmaPosicao, habilitacao],
            (PapelDoFormularioDeTeste.Inscricao, inscricao.Id),
            (PapelDoFormularioDeTeste.IsencaoTaxa, naMesmaPosicao.Id),
            (PapelDoFormularioDeTeste.Habilitacao, habilitacao.Id))
            .Should().Equal(PapelDoFormularioDeTeste.Habilitacao);
    }

    [Fact(DisplayName = "O formulário cuja fase não está no cronograma fica fora do que se conhece")]
    public void FormularioDeFaseForaDoCronograma_FicaFora()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);

        PapeisBloqueadosNa(
            [inscricao],
            (PapelDoFormularioDeTeste.Inscricao, inscricao.Id),
            (PapelDoFormularioDeTeste.IsencaoTaxa, Guid.CreateVersion7()))
            .Should().Equal(PapelDoFormularioDeTeste.IsencaoTaxa, PapelDoFormularioDeTeste.Habilitacao);
    }

    [Fact(DisplayName = "Sem formulário de inscrição, nenhuma exigência é bloqueada")]
    public void SemFormularioDeInscricao_NadaBloqueado()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);

        IReadOnlySet<Guid> bloqueadas = ExigenciasDeFasePosterior.Na(
            Configuracao([isencao], (PapelDoFormularioDeTeste.IsencaoTaxa, isencao.Id)).Configuracao, SemFatosConhecidos);

        bloqueadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com a fase do formulário de inscrição fora do cronograma, nenhuma exigência é bloqueada")]
    public void FaseDaInscricaoForaDoCronograma_NadaBloqueado()
    {
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

        IReadOnlySet<Guid> bloqueadas = ExigenciasDeFasePosterior.Na(
            Configuracao(
                [habilitacao],
                (PapelDoFormularioDeTeste.Inscricao, Guid.CreateVersion7()),
                (PapelDoFormularioDeTeste.Habilitacao, habilitacao.Id)).Configuracao,
            SemFatosConhecidos);

        bloqueadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Fases com a mesma identidade no cronograma são recusadas com exceção")]
    public void FasesComIdentidadeRepetida_Lanca()
    {
        Guid identidade = Guid.CreateVersion7();
        FaseCronograma[] fases = [FaseReidratada(identidade, 1), FaseReidratada(identidade, 2)];
        ConfiguracaoAvaliavel configuracao = Configuracao(fases, (PapelDoFormularioDeTeste.Inscricao, identidade)).Configuracao;

        Func<IReadOnlySet<Guid>> consultar = () => ExigenciasDeFasePosterior.Na(configuracao, SemFatosConhecidos);

        consultar.Should().Throw<ArgumentException>();
    }

    private static Dictionary<string, FatoResolvido> SemFatosConhecidos { get; } = new(StringComparer.Ordinal);

    private static List<PapelDoFormularioDeTeste> PapeisBloqueadosNa(
        FaseCronograma[] fases, params (PapelDoFormularioDeTeste Papel, Guid FaseId)[] formularios)
    {
        (ConfiguracaoAvaliavel configuracao, Dictionary<PapelDoFormularioDeTeste, Guid> exigencias) = Configuracao(fases, formularios);
        IReadOnlySet<Guid> bloqueadas = ExigenciasDeFasePosterior.Na(configuracao, SemFatosConhecidos);

        return [.. Papeis.Where(f => bloqueadas.Contains(exigencias[f]))];
    }

    private static (ConfiguracaoAvaliavel Configuracao, Dictionary<PapelDoFormularioDeTeste, Guid> ExigenciaPorPapel) Configuracao(
        FaseCronograma[] fases, params (PapelDoFormularioDeTeste Papel, Guid FaseId)[] formularios)
    {
        Dictionary<PapelDoFormularioDeTeste, DocumentoExigido> documentos = Papeis.ToDictionary(static f => f, f => ExigenciaDoFato(fases[0], FatoDe(f)));
        ConfiguracaoAvaliavel configuracao = new(
            DefinicaoComUmaPaginaPorFormulario(),
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            RegrasDeComprovacaoDaVersao.DaConfiguracaoPublicada([.. Papeis.Select((f, indice) => RegraDeComprovacao.CriarRegraDeDocumento(documentos[f], indice).Value!)]),
            [],
            [],
            [],
            fases,
            [.. formularios.Select(static f => Formulario(f.Papel, f.FaseId))]);

        return (configuracao, documentos.ToDictionary(static d => d.Key, static d => d.Value.Id));
    }

    private static DefinicaoFormulario DefinicaoComUmaPaginaPorFormulario() =>
        new(
            [.. Papeis.Select(static f => new DefinicaoPagina(
                DefinicaoDoProcesso.CodigoDaPagina(FormularioDeTeste.CodigoDoPapel(f), Secao), null, [new DefinicaoItem(FatoDe(f), null, Obrigatoriedade.Sempre, [])]))],
            [],
            []);

    private static string FatoDe(PapelDoFormularioDeTeste papel) => $"FATO_DA_{papel}".ToUpperInvariant();

    private static DocumentoExigido ExigenciaDoFato(FaseCronograma fase, string fato) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "COMPROVANTE", "Comprovante", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(fase.Id, null, FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Inscricao)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    private static FormularioProcesso Formulario(PapelDoFormularioDeTeste papel, Guid faseId) =>
        FormularioProcesso.Criar(
            FormularioDeTeste.DeclaradoNoPapel(papel, faseId), new CronogramaDoProcesso([]), null,
            [PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 0, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!]).Value!;

    private static FaseCronograma FaseReidratada(Guid id, int ordem) =>
        FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            id,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), $"FASE_{ordem}", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: ordem == 1, ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null),
            []);
}
