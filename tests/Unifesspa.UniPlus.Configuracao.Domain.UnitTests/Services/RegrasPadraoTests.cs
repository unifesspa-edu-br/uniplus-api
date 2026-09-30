namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Cadastro do derivado por regra do administrador e das suas regras padrão (ADR-0136): o que a
/// regra pode citar e contribuir, e o que o catálogo recusa — ciclo, resolução antes da
/// dependência, proteção mais fraca que a dela e citação nova de fato ou valor desativado.
/// </summary>
public sealed class RegrasPadraoTests
{
    private const string Finalidade = "Enquadramento na reserva de vagas.";
    private const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    [Fact(DisplayName = "Derivado booleano é escalar sem fonte; o categórico é multivalorado de valores próprios")]
    public void CriarDerivado_FormaPeloDominio()
    {
        FatoCandidato booleano = Derivado("EGRESSO_REDE_PUBLICA");
        FatoCandidato categorico = Derivado("PERFIS", DominioFato.Categorico);

        booleano.Should().BeEquivalentTo(new
        {
            Origem = OrigemFato.Derivado,
            Cardinalidade = CardinalidadeFato.Escalar,
            FonteValores = (FonteValoresFato?)null,
            Binding = "REGRA_DERIVACAO:EGRESSO_REDE_PUBLICA",
            Sistema = false,
        });
        categorico.Should().BeEquivalentTo(new { Cardinalidade = CardinalidadeFato.Multivalorado, FonteValores = FonteValoresFato.Global });
    }

    [Fact(DisplayName = "Derivado por regra de domínio texto é recusado no domínio")]
    public void CriarDerivado_Texto_Recusa() =>
        FatoCandidato.CriarDerivadoDoAdministrador(
                "NOME_DERIVADO", "Nome", null, DominioFato.Texto, "INSCRICAO", EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese)
            .Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.DerivadoPorRegraSoBooleanoOuCategorico);

    [Fact(DisplayName = "Fato declarado não tem regras padrão")]
    public void Definir_FatoDeclarado_Recusa()
    {
        FatoCandidato declarado = Declarado("TEM_RENDA");

        declarado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", true))], Catalogo(declarado))
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.RegrasPadraoSoEmDerivadoPorRegra);
    }

    [Fact(DisplayName = "Regras válidas substituem as anteriores e a lista vazia as remove")]
    public void Definir_Valida_Substitui()
    {
        FatoCandidato renda = Declarado("TEM_RENDA");
        FatoCandidato derivado = Derivado("EGRESSO_REDE_PUBLICA");
        CatalogoDeFatos catalogo = Catalogo(renda, derivado);

        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", true))], catalogo).IsSuccess.Should().BeTrue();
        derivado.RegrasPadrao.Should().ContainSingle();

        derivado.DefinirRegrasPadrao([], catalogo).IsSuccess.Should().BeTrue();
        derivado.RegrasPadrao.Should().BeEmpty();
    }

    [Theory(DisplayName = "O categórico contribui só os seus valores; o booleano não contribui código")]
    [InlineData(DominioFato.Categorico, "EJA", true)]
    [InlineData(DominioFato.Categorico, "XYZ", false)]
    [InlineData(DominioFato.Booleano, "EJA", false)]
    public void Definir_Contribuicao(DominioFato dominio, string contribui, bool aceita)
    {
        FatoCandidato renda = Declarado("TEM_RENDA");
        FatoCandidato derivado = Derivado("PERFIS", dominio);
        if (dominio == DominioFato.Categorico)
        {
            derivado.AdicionarValorDominio("EJA", "Educação de jovens e adultos", 0, ativo: true);
        }

        Result resultado = derivado.DefinirRegrasPadrao([Regra(contribui, ("TEM_RENDA", true))], Catalogo(renda, derivado));

        if (aceita)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        }
        else
        {
            resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "regras[0]",
                Error = new { Code = RegrasDerivacaoFatoErrorCodes.ContribuiForaDoDominio },
            }, "a recusa aponta a regra que contribui fora do domínio");
        }
    }

    [Fact(DisplayName = "Texto não entra em predicado de regra")]
    public void Definir_CitaTexto_Recusa()
    {
        FatoCandidato nome = Declarado("NOME", DominioFato.Texto, formato: FormatoTexto.NomePessoa);
        FatoCandidato derivado = Derivado("TEM_NOME");

        derivado.DefinirRegrasPadrao([Booleana(("NOME", "MARIA"))], Catalogo(nome, derivado))
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.RegraCitaFatoNaoCitavel);
    }

    [Fact(DisplayName = "O derivado não protege menos que um fato que ele cita")]
    public void Definir_ClassificacaoMaisFraca_Recusa()
    {
        FatoCandidato pcd = Declarado("PCD", classificacao: ClassificacaoProtecaoDado.Sensivel);
        FatoCandidato derivado = Derivado("ATENDIMENTO_PRIORITARIO", classificacao: ClassificacaoProtecaoDado.Pessoal);

        derivado.DefinirRegrasPadrao([Booleana(("PCD", true))], Catalogo(pcd, derivado))
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia);
    }

    [Theory(DisplayName = "O derivado não resolve em fase anterior à de um fato que ele cita")]
    [InlineData("INSCRICAO", false)]
    [InlineData("HABILITACAO", true)]
    public void Definir_PontoResolucao(string pontoDoDerivado, bool aceita)
    {
        FatoCandidato certificado = Declarado("CERTIFICADO_EMITIDO", ponto: "HABILITACAO");
        FatoCandidato derivado = Derivado("PENDENTE_DE_CERTIFICADO", ponto: pontoDoDerivado);
        PrecedenciaFase[] precedencias =
        [
            PrecedenciaFase.Criar("INSCRICAO", "RESULTADO_FINAL", permiteSobreposicao: false, []).Value!,
            PrecedenciaFase.Criar("RESULTADO_FINAL", "HABILITACAO", permiteSobreposicao: false, []).Value!,
        ];

        Result resultado = derivado.DefinirRegrasPadrao(
            [Booleana(("CERTIFICADO_EMITIDO", false))], new CatalogoDeFatos([certificado, derivado], precedencias));

        if (aceita)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        }
        else
        {
            resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia);
        }
    }

    [Fact(DisplayName = "Regras que fecham ciclo entre derivados são recusadas")]
    public void Definir_Ciclo_Recusa()
    {
        FatoCandidato a = Derivado("DERIVADO_A");
        FatoCandidato b = Derivado("DERIVADO_B");
        CatalogoDeFatos catalogo = Catalogo(a, b);
        a.DefinirRegrasPadrao([Booleana(("DERIVADO_B", true))], catalogo).IsSuccess.Should().BeTrue();

        b.DefinirRegrasPadrao([Booleana(("DERIVADO_A", true))], catalogo)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.CicloEntreDerivados);
    }

    [Fact(DisplayName = "Fato desativado recusa citação nova e mantém a que a regra já fazia")]
    public void Definir_FatoDesativado_SoRecusaCitacaoNova()
    {
        FatoCandidato renda = Declarado("TEM_RENDA");
        FatoCandidato bolsa = Declarado("TEM_BOLSA");
        FatoCandidato derivado = Derivado("VULNERAVEL");
        CatalogoDeFatos catalogo = Catalogo(renda, bolsa, derivado);
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", false))], catalogo).IsSuccess.Should().BeTrue();
        renda.Desativar();
        bolsa.Desativar();

        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", false))], catalogo).IsSuccess.Should().BeTrue();
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", false)), Booleana(("TEM_BOLSA", true))], catalogo)
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "regras[1]",
                Error = new { Code = FatoCandidatoErrorCodes.RegraCitaFatoDesativado },
            }, "a recusa aponta a regra que passou a citar o fato desativado");
    }

    [Fact(DisplayName = "Valor desativado do próprio categórico não recebe contribuição nova")]
    public void Definir_ContribuiValorDesativado_Recusa()
    {
        FatoCandidato renda = Declarado("TEM_RENDA");
        FatoCandidato derivado = Derivado("PERFIS", DominioFato.Categorico);
        derivado.AdicionarValorDominio("EJA", "Educação de jovens e adultos", 0, ativo: true);
        derivado.DesativarValor("EJA");

        derivado.DefinirRegrasPadrao([Regra("EJA", ("TEM_RENDA", true))], Catalogo(renda, derivado))
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.RegraCitaValorDesativado);
    }

    [Fact(DisplayName = "As recusas de regras distintas se acumulam, cada uma no índice da sua regra")]
    public void Definir_RecusasAcumuladas()
    {
        FatoCandidato pcd = Declarado("PCD", classificacao: ClassificacaoProtecaoDado.Sensivel);
        FatoCandidato nome = Declarado("NOME", DominioFato.Texto, formato: FormatoTexto.NomePessoa);
        FatoCandidato derivado = Derivado("DERIVADO", classificacao: ClassificacaoProtecaoDado.Pessoal);

        Result resultado = derivado.DefinirRegrasPadrao(
            [Booleana(("PCD", true)), Booleana(("NOME", "MARIA"))], Catalogo(pcd, nome, derivado));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("regras[0]", FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia),
            ("regras[1]", FatoCandidatoErrorCodes.RegraCitaFatoNaoCitavel),
        ]);
    }

    private static FatoCandidato Derivado(
        string codigo,
        DominioFato dominio = DominioFato.Booleano,
        string ponto = "INSCRICAO",
        ClassificacaoProtecaoDado classificacao = ClassificacaoProtecaoDado.Sensivel) =>
        FatoCandidato.CriarDerivadoDoAdministrador(
            codigo, codigo, null, dominio, ponto, EscopoFato.Candidato, classificacao, Finalidade, Hipotese).Value!;

    private static FatoCandidato Declarado(
        string codigo,
        DominioFato dominio = DominioFato.Booleano,
        string ponto = "INSCRICAO",
        ClassificacaoProtecaoDado classificacao = ClassificacaoProtecaoDado.Pessoal,
        FormatoTexto? formato = null) =>
        FatoCandidato.CriarDoAdministrador(
            codigo, codigo, null, dominio, CardinalidadeFato.Escalar, null, formato, ponto, EscopoFato.Candidato,
            classificacao, Finalidade, Hipotese).Value!;

    private static CatalogoDeFatos Catalogo(params FatoCandidato[] fatos) => new(fatos, []);

    private static RegraDerivacao Booleana((string Fato, object Valor) condicao) =>
        RegraDerivacao.CriarBooleana(Quando(condicao));

    private static RegraDerivacao Regra(string contribui, (string Fato, object Valor) condicao) =>
        RegraDerivacao.Criar(Quando(condicao), contribui).Value!;

    private static PredicadoDnf Quando((string Fato, object Valor) condicao) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(condicao.Fato, Operador.Igual, JsonSerializer.SerializeToElement(condicao.Valor)).Value!)]).Value!;
}
