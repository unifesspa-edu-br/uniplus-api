namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Fatos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// O fato no formulário que entrega a exigência de documento: conhecido pela ordem do cronograma, com a marca "no mesmo momento da
/// anterior", e não pela vez do envio. A isenção antes da inscrição, sem a marca, cita fato que só se conhece depois dela.
/// </summary>
public sealed class ConhecimentoNaExigenciaTests
{
    private const string OrigemEscolar = "origem_escolar";

    private readonly FaseCronograma _isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);

    private static CatalogoDosFatosCitados Catalogo(Dictionary<string, string> fases, Dictionary<string, IReadOnlyCollection<string>>? derivados = null) =>
        new(fases, [], derivados ?? []);

    private static FaseCronograma Inscricao(bool marcada) => FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true, noMesmoMomentoDaAnterior: marcada);

    private ConhecimentoNaExigencia Conhecer(
        FaseCronograma inscricao, CatalogoDosFatosCitados catalogo, string fato = OrigemEscolar, params string[] fatosDaInscricao)
    {
        FormularioDeclarado formularioDaInscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", inscricao);
        FormularioDeclarado pedido = ColetaDeTeste.Facultativo("PEDIDO", _isencao);
        ConhecimentoDosFatos conhecimento = new(
            [ColetaDeTeste.Coleta(formularioDaInscricao, fatosDaInscricao.Length == 0 ? [OrigemEscolar] : fatosDaInscricao), ColetaDeTeste.Coleta(pedido, "renda")],
            catalogo,
            [_isencao, inscricao]);

        return conhecimento.NaExigencia(fato, new PontoDeUso(_isencao, pedido));
    }

    [Fact(DisplayName = "O fato da inscrição, na exigência da isenção que vem antes dela, sem a marca, é recusado como resolvido em fase posterior")]
    public void FatoDaInscricao_NaIsencaoAntesDela_SemMarca_Recusa()
    {
        ConhecimentoNaExigencia conhecimento = Conhecer(Inscricao(marcada: false), Catalogo(new() { [OrigemEscolar] = "INSCRICAO" }));

        conhecimento.Conhecido.Should().BeFalse();
        conhecimento.RecusaDaFase.Should().NotBeNull();
        conhecimento.RecusaDaFase!.Code.Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
        conhecimento.RecusaDaFase.Message.Should().Contain("'Inscrição'").And.NotContain("INSCRICAO").And.Contain("(ordem 2)").And.Contain("(ordem 1)");
    }

    [Fact(DisplayName = "Com a marca na inscrição, a isenção e a inscrição acontecem juntas e o fato é conhecido")]
    public void FatoDaInscricao_NaIsencaoNoMesmoMomento_ComMarca_EConhecido()
    {
        ConhecimentoNaExigencia conhecimento = Conhecer(Inscricao(marcada: true), Catalogo(new() { [OrigemEscolar] = "INSCRICAO" }));

        conhecimento.Conhecido.Should().BeTrue();
        conhecimento.RecusaDaFase.Should().BeNull();
    }

    [Fact(DisplayName = "A fase do catálogo que o cronograma não tem é recusada com a recusa do ponto de resolução")]
    public void FaseDoCatalogoForaDoCronograma_Recusa()
    {
        ConhecimentoNaExigencia conhecimento = Conhecer(Inscricao(marcada: false), Catalogo(new() { [OrigemEscolar] = "FASE_QUE_NAO_EXISTE" }));

        conhecimento.RecusaDaFase!.Code.Should().Be(DocumentoExigidoErrorCodes.PontoResolucaoForaDoCronograma);
        conhecimento.Conhecido.Should().BeFalse();
    }

    [Fact(DisplayName = "O derivado vale pela fase da dependência mais tardia: sem a marca é posterior, com a marca é conhecido")]
    public void Derivado_ValePelaFaseDaDependencia()
    {
        CatalogoDosFatosCitados catalogo = Catalogo(new() { ["idade"] = "ISENCAO" }, new() { ["idade"] = ["data_de_nascimento"] });

        Conhecer(Inscricao(marcada: false), catalogo, "idade", "data_de_nascimento").RecusaDaFase!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
        Conhecer(Inscricao(marcada: true), catalogo, "idade", "data_de_nascimento").Conhecido.Should().BeTrue();
    }

    [Fact(DisplayName = "O derivado com dependência que ninguém produz não é conhecido, e sem recusa de fase")]
    public void DerivadoComDependenciaDesconhecida_NaoEConhecido()
    {
        CatalogoDosFatosCitados catalogo = Catalogo([], new() { ["idade"] = ["fato_que_ninguem_produz"] });

        ConhecimentoNaExigencia conhecimento = Conhecer(Inscricao(marcada: true), catalogo, "idade");

        conhecimento.Situacao.Should().Be(ConhecimentoDoFatoNoPonto.DependenciaDesconhecida);
        conhecimento.RecusaDaFase.Should().BeNull();
        conhecimento.Conhecido.Should().BeFalse();
    }

    [Fact(DisplayName = "O fato de formulário que a exigência não alcança fica com a situação da origem, sem julgar a fase")]
    public void RespostaNaoAlcancada_SemJulgarAFase()
    {
        FaseCronograma inscricao = Inscricao(marcada: false);
        FormularioDeclarado pedidoDaInscricao = ColetaDeTeste.Facultativo("PEDIDO_INSCRICAO", inscricao);
        FormularioDeclarado pedido = ColetaDeTeste.Facultativo("PEDIDO", _isencao);
        ConhecimentoDosFatos conhecimento = new(
            [ColetaDeTeste.Coleta(pedidoDaInscricao, OrigemEscolar), ColetaDeTeste.Coleta(pedido, "renda")],
            Catalogo(new() { [OrigemEscolar] = "INSCRICAO" }),
            [_isencao, inscricao]);

        ConhecimentoNaExigencia resultado = conhecimento.NaExigencia(OrigemEscolar, new PontoDeUso(_isencao, pedido));

        resultado.Situacao.Should().Be(ConhecimentoDoFatoNoPonto.RespostaNaoAlcancada);
        resultado.RecusaDaFase.Should().BeNull();
    }

    [Fact(DisplayName = "O fato sem origem no processo fica fora do processo, sem julgar a fase")]
    public void FatoSemOrigem_ForaDoProcesso()
    {
        ConhecimentoNaExigencia resultado = Conhecer(Inscricao(marcada: false), Catalogo([]), "fato_desconhecido");

        resultado.Situacao.Should().Be(ConhecimentoDoFatoNoPonto.ForaDoProcesso);
        resultado.RecusaDaFase.Should().BeNull();
    }

    [Fact(DisplayName = "Sem o fato ou sem o ponto, a pergunta não se faz")]
    public void SemFatoOuPonto_Lanca()
    {
        ConhecimentoDosFatos conhecimento = new([], Catalogo([]), [_isencao]);

        Func<ConhecimentoNaExigencia> semFato = () => conhecimento.NaExigencia(" ", new PontoDeUso(_isencao, null));
        Func<ConhecimentoNaExigencia> semPonto = () => conhecimento.NaExigencia("fato", null!);

        semFato.Should().Throw<ArgumentException>();
        semPonto.Should().Throw<ArgumentNullException>();
    }
}
