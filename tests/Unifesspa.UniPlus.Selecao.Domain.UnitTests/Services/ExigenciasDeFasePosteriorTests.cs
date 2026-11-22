namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As exigências que a fase da inscrição ainda não decide: a exigência cujo gatilho cita um fato perguntado por um
/// formulário de fase posterior à da inscrição fica bloqueada, e a de fato perguntado até a fase da inscrição, não.
/// Cada finalidade pergunta um fato próprio e tem uma exigência citando esse fato.
/// </summary>
public sealed class ExigenciasDeFasePosteriorTests
{
    private const string Secao = "DADOS";

    private static readonly FinalidadeFormulario[] Finalidades =
        [FinalidadeFormulario.Inscricao, FinalidadeFormulario.IsencaoTaxa, FinalidadeFormulario.Habilitacao];

    [Fact(DisplayName = "O formulário da fase de isenção anterior à inscrição é conhecido, e o da habilitação posterior fica bloqueado")]
    public void FormularioDeFaseAnterior_Conhecido_FormularioDeFasePosterior_Bloqueado()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

        FinalidadesBloqueadasNa(
            [isencao, inscricao, habilitacao],
            (FinalidadeFormulario.Inscricao, inscricao.Id),
            (FinalidadeFormulario.IsencaoTaxa, isencao.Id),
            (FinalidadeFormulario.Habilitacao, habilitacao.Id))
            .Should().Equal(FinalidadeFormulario.Habilitacao);
    }

    [Fact(DisplayName = "O formulário de inscrição na fase que abre a isenção antes da primeira fase de inscrição vale pela ordem dessa fase")]
    public void InscricaoEmFaseQueAbreAIsencao_ValePelaOrdemDaFase()
    {
        FaseCronograma abreAIsencao = FaseDeTeste.Criar(1, "ABERTURA_DA_ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma primeiraInscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);

        FinalidadesBloqueadasNa(
            [abreAIsencao, primeiraInscricao],
            (FinalidadeFormulario.Inscricao, abreAIsencao.Id),
            (FinalidadeFormulario.Habilitacao, primeiraInscricao.Id))
            .Should().Equal(FinalidadeFormulario.IsencaoTaxa, FinalidadeFormulario.Habilitacao);
    }

    [Fact(DisplayName = "Havendo várias fases de inscrição, vale a fase do formulário de inscrição")]
    public void VariasFasesDeInscricao_ValeAFaseDoFormulario()
    {
        FaseCronograma primeiraInscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma isencao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma segundaInscricao = FaseDeTeste.Criar(3, "INSCRICAO_COMPLEMENTAR", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(4, "HABILITACAO");

        FinalidadesBloqueadasNa(
            [primeiraInscricao, isencao, segundaInscricao, habilitacao],
            (FinalidadeFormulario.Inscricao, segundaInscricao.Id),
            (FinalidadeFormulario.IsencaoTaxa, isencao.Id),
            (FinalidadeFormulario.Habilitacao, habilitacao.Id))
            .Should().Equal(FinalidadeFormulario.Habilitacao);
    }

    [Fact(DisplayName = "O formulário na própria fase da inscrição, ou em outra fase na mesma posição, é conhecido")]
    public void FormularioNaMesmaPosicaoDaInscricao_Conhecido()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
        FaseCronograma naMesmaPosicao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

        FinalidadesBloqueadasNa(
            [inscricao, naMesmaPosicao, habilitacao],
            (FinalidadeFormulario.Inscricao, inscricao.Id),
            (FinalidadeFormulario.IsencaoTaxa, naMesmaPosicao.Id),
            (FinalidadeFormulario.Habilitacao, habilitacao.Id))
            .Should().Equal(FinalidadeFormulario.Habilitacao);
    }

    [Fact(DisplayName = "O formulário cuja fase não está no cronograma fica fora do que se conhece")]
    public void FormularioDeFaseForaDoCronograma_FicaFora()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);

        FinalidadesBloqueadasNa(
            [inscricao],
            (FinalidadeFormulario.Inscricao, inscricao.Id),
            (FinalidadeFormulario.IsencaoTaxa, Guid.CreateVersion7()))
            .Should().Equal(FinalidadeFormulario.IsencaoTaxa, FinalidadeFormulario.Habilitacao);
    }

    [Fact(DisplayName = "Sem formulário de inscrição, nenhuma exigência é bloqueada")]
    public void SemFormularioDeInscricao_NadaBloqueado()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);

        IReadOnlySet<Guid> bloqueadas = ExigenciasDeFasePosterior.Na(
            Configuracao([isencao], (FinalidadeFormulario.IsencaoTaxa, isencao.Id)).Configuracao, SemFatosConhecidos);

        bloqueadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com a fase do formulário de inscrição fora do cronograma, nenhuma exigência é bloqueada")]
    public void FaseDaInscricaoForaDoCronograma_NadaBloqueado()
    {
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

        IReadOnlySet<Guid> bloqueadas = ExigenciasDeFasePosterior.Na(
            Configuracao(
                [habilitacao],
                (FinalidadeFormulario.Inscricao, Guid.CreateVersion7()),
                (FinalidadeFormulario.Habilitacao, habilitacao.Id)).Configuracao,
            SemFatosConhecidos);

        bloqueadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Fases com a mesma identidade no cronograma são recusadas com exceção")]
    public void FasesComIdentidadeRepetida_Lanca()
    {
        Guid identidade = Guid.CreateVersion7();
        FaseCronograma[] fases = [FaseReidratada(identidade, 1), FaseReidratada(identidade, 2)];
        ConfiguracaoAvaliavel configuracao = Configuracao(fases, (FinalidadeFormulario.Inscricao, identidade)).Configuracao;

        Func<IReadOnlySet<Guid>> consultar = () => ExigenciasDeFasePosterior.Na(configuracao, SemFatosConhecidos);

        consultar.Should().Throw<ArgumentException>();
    }

    private static Dictionary<string, FatoResolvido> SemFatosConhecidos { get; } = new(StringComparer.Ordinal);

    private static List<FinalidadeFormulario> FinalidadesBloqueadasNa(
        FaseCronograma[] fases, params (FinalidadeFormulario Finalidade, Guid FaseId)[] formularios)
    {
        (ConfiguracaoAvaliavel configuracao, Dictionary<FinalidadeFormulario, Guid> exigencias) = Configuracao(fases, formularios);
        IReadOnlySet<Guid> bloqueadas = ExigenciasDeFasePosterior.Na(configuracao, SemFatosConhecidos);

        return [.. Finalidades.Where(f => bloqueadas.Contains(exigencias[f]))];
    }

    private static (ConfiguracaoAvaliavel Configuracao, Dictionary<FinalidadeFormulario, Guid> ExigenciaPorFinalidade) Configuracao(
        FaseCronograma[] fases, params (FinalidadeFormulario Finalidade, Guid FaseId)[] formularios)
    {
        Dictionary<FinalidadeFormulario, DocumentoExigido> documentos = Finalidades.ToDictionary(static f => f, f => ExigenciaDoFato(fases[0], FatoDe(f)));
        ConfiguracaoAvaliavel configuracao = new(
            DefinicaoComUmaEtapaPorFinalidade(),
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            RegrasDeComprovacaoDaVersao.DaConfiguracaoPublicada([.. Finalidades.Select((f, indice) => RegraDeComprovacao.CriarRegraDeDocumento(documentos[f], indice).Value!)]),
            [],
            [],
            [],
            fases,
            [.. formularios.Select(static f => Formulario(f.Finalidade, f.FaseId))]);

        return (configuracao, documentos.ToDictionary(static d => d.Key, static d => d.Value.Id));
    }

    private static DefinicaoFormulario DefinicaoComUmaEtapaPorFinalidade() =>
        new(
            [.. Finalidades.Select(static f => new DefinicaoEtapa(
                DefinicaoDoProcesso.CodigoDaEtapa(f, Secao), null, [new DefinicaoItem(FatoDe(f), null, Obrigatoriedade.Sempre, [])]))],
            [],
            []);

    private static string FatoDe(FinalidadeFormulario finalidade) => $"FATO_DA_{finalidade}".ToUpperInvariant();

    private static DocumentoExigido ExigenciaDoFato(FaseCronograma fase, string fato) =>
        DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "COMPROVANTE", "Comprovante", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            [], null, FormatosPermitidos.Criar(true, null).Value!, null).Value!;

    private static FormularioProcesso Formulario(FinalidadeFormulario finalidade, Guid faseId) =>
        FormularioProcesso.Criar(
            finalidade, faseId, null,
            [EtapaFormulario.Criar("REVISAO", 0, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!]).Value!;

    private static FaseCronograma FaseReidratada(Guid id, int ordem) =>
        FaseCronograma.Reidratar(
            id,
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), $"FASE_{ordem}", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: ordem == 1, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null),
            []);
}
