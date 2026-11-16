namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// No processo com inscrição pelo sistema, a regra de aplicabilidade de cada modalidade ofertada cita só
/// fatos conhecidos ao fim da fase que coleta inscrição (UNI-REQ-0076, UNI-REQ-0075). O fato que só fica
/// conhecido depois, como o grupo da convocação, é recusado com a modalidade e o fato identificados.
/// </summary>
public sealed class RegrasDasModalidadesNaInscricaoTests
{
    private static readonly ProcessoDosFormularios PeloSistema = new(InscricaoPeloSistema: true, CobraTaxa: false, PrimeiraFaseDepoisDaImportacaoId: null);

    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _isencao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);
    private readonly FaseCronograma _convocacao = FaseDeTeste.Criar(6, "CONVOCACAO");

    private ConhecimentoDosFatos Conhecimento(params ColetaDoFormulario[] coletas) =>
        new(
            coletas,
            new CatalogoDosFatosCitados(
                new Dictionary<string, string> { ["grupo_da_convocacao"] = "CONVOCACAO" },
                ["renda_per_capita"],
                new Dictionary<string, IReadOnlyCollection<string>> { ["ensino_medio_publico"] = ["origem_escolar"] }),
            [_inscricao, _isencao, _convocacao]);

    private static readonly Dictionary<string, IReadOnlyCollection<string>> DerivadosComRamosCompartilhados = new()
    {
        ["a"] = ["b", "c"],
        ["b"] = ["d"],
        ["c"] = ["d"],
        ["d"] = ["origem_escolar"],
    };

    private static IReadOnlyList<RegraDaModalidadeOfertada> Modalidade(params string[] fatos) => [new("AC_PPI", fatos)];

    [Fact(DisplayName = "A regra que cita fato coletado pelo formulário da inscrição é aceita")]
    public void FatoDoFormularioDaInscricao_Aceita()
    {
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar", "cor_ou_raca");

        RegrasDasModalidadesNaInscricao.Conferir(PeloSistema, Conhecimento(inscricao), Modalidade("origem_escolar", "cor_ou_raca")).Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra que cita fato derivado do formulário da inscrição é aceita")]
    public void FatoDerivadoDaInscricao_Aceita()
    {
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar");

        RegrasDasModalidadesNaInscricao.Conferir(PeloSistema, Conhecimento(inscricao), Modalidade("ensino_medio_publico")).Should().BeEmpty();
    }

    [Fact(DisplayName = "O fato que só fica conhecido depois da inscrição é recusado com a modalidade e o fato")]
    public void FatoDeFasePosterior_RecusaComModalidadeEFato()
    {
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar");

        FieldError recusa = RegrasDasModalidadesNaInscricao.Conferir(PeloSistema, Conhecimento(inscricao), Modalidade("origem_escolar", "grupo_da_convocacao")).Single();

        recusa.Field.Should().Be("modalidades[AC_PPI].fatos.grupo_da_convocacao");
        recusa.Error.Code.Should().Be(ModalidadesNaInscricaoErrorCodes.RegraCitaFatoNaoColetado);
        recusa.Error.Message.Should().NotContain("grupo_da_convocacao").And.NotContain("AC_PPI");
    }

    [Fact(DisplayName = "O fato só de formulário facultativo é recusado")]
    public void FatoDeFacultativo_Recusa()
    {
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar");
        ColetaDoFormulario pedido = ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("PEDIDO", _isencao), "fundamento_da_isencao");

        RegrasDasModalidadesNaInscricao.Conferir(PeloSistema, Conhecimento(inscricao, pedido), Modalidade("fundamento_da_isencao"))
            .Should().ContainSingle().Which.Error.Code.Should().Be(ModalidadesNaInscricaoErrorCodes.RegraCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "O fato de formulário obrigatório de fase anterior à inscrição, que não a coleta, é recusado")]
    public void FatoDeFormularioForaDaInscricao_Recusa()
    {
        FaseCronograma anterior = FaseDeTeste.Criar(1, "PREPARACAO");
        FaseCronograma inscricaoPosterior = FaseDeTeste.Criar(3, "INSCRICAO_TARDIA", coletaInscricao: true);
        ColetaDoFormulario previo = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("PREVIO", anterior), "fato_previo");
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", inscricaoPosterior), "origem_escolar");
        ConhecimentoDosFatos conhecimento = new(
            [previo, inscricao],
            new CatalogoDosFatosCitados(new Dictionary<string, string>(), [], new Dictionary<string, IReadOnlyCollection<string>>()),
            [anterior, inscricaoPosterior]);

        RegrasDasModalidadesNaInscricao.Conferir(PeloSistema, conhecimento, Modalidade("fato_previo"))
            .Should().ContainSingle().Which.Error.Code.Should().Be(ModalidadesNaInscricaoErrorCodes.RegraCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "O derivado cujos ramos compartilham outro derivado da inscrição é aceito")]
    public void DerivadoComDependenciaCompartilhada_Aceita()
    {
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar");
        ConhecimentoDosFatos conhecimento = new([inscricao], new CatalogoDosFatosCitados(new Dictionary<string, string>(), [], DerivadosComRamosCompartilhados), [_inscricao]);

        RegrasDasModalidadesNaInscricao.Conferir(PeloSistema, conhecimento, Modalidade("a")).Should().BeEmpty();
    }

    [Fact(DisplayName = "O fato resolvido pelo sistema, sem formulário da inscrição, é recusado")]
    public void FatoResolvidoPeloSistema_Recusa()
    {
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar");
        ConhecimentoDosFatos conhecimento = new(
            [inscricao],
            new CatalogoDosFatosCitados(new Dictionary<string, string> { ["atributo_do_sistema"] = "INSCRICAO" }, [], new Dictionary<string, IReadOnlyCollection<string>>()),
            [_inscricao]);

        RegrasDasModalidadesNaInscricao.Conferir(PeloSistema, conhecimento, Modalidade("atributo_do_sistema"))
            .Should().ContainSingle().Which.Error.Code.Should().Be(ModalidadesNaInscricaoErrorCodes.RegraCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "O fato declarado que nenhum formulário coleta é recusado")]
    public void FatoDeclaradoSemFormulario_Recusa()
    {
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar");

        RegrasDasModalidadesNaInscricao.Conferir(PeloSistema, Conhecimento(inscricao), Modalidade("renda_per_capita"))
            .Should().ContainSingle().Which.Error.Code.Should().Be(ModalidadesNaInscricaoErrorCodes.RegraCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "O processo de resultado importado não é conferido")]
    public void ResultadoImportado_NaoEConferido()
    {
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar");

        RegrasDasModalidadesNaInscricao.Conferir(PeloSistema with { InscricaoPeloSistema = false }, Conhecimento(inscricao), Modalidade("grupo_da_convocacao"))
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "O processo sem vagas por modalidade não é conferido")]
    public void SemModalidades_NaoEConferido()
    {
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar");

        RegrasDasModalidadesNaInscricao.Conferir(PeloSistema, Conhecimento(inscricao), []).Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada modalidade e cada fato recusado saem juntos")]
    public void Recusas_SeAcumulam()
    {
        ColetaDoFormulario inscricao = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar");

        RegrasDasModalidadesNaInscricao.Conferir(
            PeloSistema,
            Conhecimento(inscricao),
            [new RegraDaModalidadeOfertada("AC_PPI", ["grupo_da_convocacao"]), new RegraDaModalidadeOfertada("AC_PCD", ["grupo_da_convocacao", "renda_per_capita"])])
            .Select(static recusa => recusa.Field).Should().Equal(
                "modalidades[AC_PPI].fatos.grupo_da_convocacao",
                "modalidades[AC_PCD].fatos.grupo_da_convocacao",
                "modalidades[AC_PCD].fatos.renda_per_capita");
    }
}
