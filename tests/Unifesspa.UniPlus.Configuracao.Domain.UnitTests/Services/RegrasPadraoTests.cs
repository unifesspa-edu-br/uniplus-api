namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
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
        FatoCandidato categorico = Derivado("PERFIS", TipoDoDado.Opcao);

        booleano.Should().BeEquivalentTo(new
        {
            Origem = OrigemFato.Calculado,
            Cardinalidade = QuantidadeDeValores.ValorUnico,
            FonteValores = (FonteValoresFato?)null,
            Binding = "REGRA_DERIVACAO:EGRESSO_REDE_PUBLICA",
            SemeadoPeloSistema = false,
        });
        categorico.Should().BeEquivalentTo(new { Cardinalidade = QuantidadeDeValores.VariosValores, FonteValores = FonteValoresFato.Catalogo });
    }

    [Fact(DisplayName = "Derivado por regra de domínio texto é recusado no domínio")]
    public void CriarDerivado_Texto_Recusa() =>
        CriacaoDoFato.CriarDerivadoDoAdministrador(
            new DescritivoDoFato("NOME_DERIVADO", "Nome", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.Texto, null, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese))
            .Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.DerivadoPorRegraSoBooleanoOuCategorico);

    [Theory(DisplayName = "Domínio do derivado no cadastro: ausente é obrigatório, fora do vocabulário é inválido")]
    [InlineData(null, FatoCandidatoErrorCodes.DominioObrigatorio)]
    [InlineData("INEXISTENTE", FatoCandidatoErrorCodes.DominioInvalido)]
    public void CriarDerivado_DominioAusenteOuInvalido_RecusaComOCodigoDoDominio(string? codigoDoDominio, string codigo) =>
        CriacaoDoFato.CriarDerivadoDoAdministrador(
            new DescritivoDoFato("DERIVADO", "Derivado", "Descrição do fato."),
            new DefinicaoDoFato(CodigoInformado<TipoDoDado>.Ler(codigoDoDominio), null, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("dominio", codigo));

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

    [Fact(DisplayName = "O categórico contribui um dos seus valores")]
    public void Definir_ContribuiValorDoCategorico_Aceita()
    {
        Result resultado = DefinirContribuicao(TipoDoDado.Opcao, "EJA");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "O categórico não contribui valor que não é seu; o booleano não contribui código")]
    [InlineData(TipoDoDado.Opcao, "XYZ")]
    [InlineData(TipoDoDado.SimOuNao, "EJA")]
    public void Definir_ContribuiForaDoDominio_Recusa(TipoDoDado dominio, string contribui) =>
        DefinirContribuicao(dominio, contribui).Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "regras[0]",
            Error = new { Code = DerivacaoDoFatoErrorCodes.ContribuiForaDoDominio },
        }, "a recusa aponta a regra que contribui fora do domínio");

    [Fact(DisplayName = "Texto não entra em predicado de regra")]
    public void Definir_CitaTexto_Recusa()
    {
        FatoCandidato nome = Declarado("NOME", TipoDoDado.Texto, formato: FormatoTexto.NomePessoa);
        FatoCandidato derivado = Derivado("TEM_NOME");

        derivado.DefinirRegrasPadrao([Booleana(("NOME", "MARIA"))], Catalogo(nome, derivado))
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.RegraCitaFatoNaoCitavel);
    }

    [Fact(DisplayName = "Fato de membro de grupo não entra em predicado de regra padrão: fora da ocorrência, não tem valor único")]
    public void Definir_CitaFatoDeMembro_Recusa()
    {
        FatoCandidato sobGuarda = Declarado("SOB_GUARDA", escopo: EscopoFato.MembroGrupo);
        FatoCandidato derivado = Derivado("TEM_MENOR_SOB_GUARDA");

        derivado.DefinirRegrasPadrao([Booleana(("SOB_GUARDA", true))], Catalogo(sobGuarda, derivado))
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.RegraCitaFatoNaoCitavel);
    }

    [Fact(DisplayName = "O derivado não protege menos que um fato que ele cita")]
    public void Definir_ClassificacaoMaisFraca_Recusa()
    {
        FatoCandidato pcd = Declarado("PCD", classificacao: ClassificacaoDeProtecao.Sensivel);
        FatoCandidato derivado = Derivado("ATENDIMENTO_PRIORITARIO", classificacao: ClassificacaoDeProtecao.Pessoal);

        derivado.DefinirRegrasPadrao([Booleana(("PCD", true))], Catalogo(pcd, derivado))
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia);
    }

    [Fact(DisplayName = "O derivado não resolve em fase anterior à de um fato que ele cita")]
    public void Definir_PontoResolucaoAnteriorAoFatoCitado_Recusa() =>
        DefinirRegraQueCitaOCertificado("INSCRICAO").Error!.Code.Should().Be(FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia);

    [Fact(DisplayName = "O derivado resolve na mesma fase do fato que ele cita")]
    public void Definir_PontoResolucaoDoFatoCitado_Aceita()
    {
        Result resultado = DefinirRegraQueCitaOCertificado("HABILITACAO");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
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
        (FatoCandidato derivado, CatalogoDeFatos catalogo) = DerivadoQueCitaRendaComRendaEBolsaDesativadas();

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
        FatoCandidato derivado = Derivado("PERFIS", TipoDoDado.Opcao);
        derivado.AdicionarValorDominio("EJA", "Educação de jovens e adultos", 0, ativo: true);
        derivado.DesativarValor("EJA");

        derivado.DefinirRegrasPadrao([Regra("EJA", ("TEM_RENDA", true))], Catalogo(renda, derivado))
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.RegraCitaValorDesativado);
    }

    [Fact(DisplayName = "As recusas de regras distintas se acumulam, cada uma no índice da sua regra")]
    public void Definir_RecusasAcumuladas()
    {
        FatoCandidato pcd = Declarado("PCD", classificacao: ClassificacaoDeProtecao.Sensivel);
        FatoCandidato nome = Declarado("NOME", TipoDoDado.Texto, formato: FormatoTexto.NomePessoa);
        FatoCandidato derivado = Derivado("DERIVADO", classificacao: ClassificacaoDeProtecao.Pessoal);

        Result resultado = derivado.DefinirRegrasPadrao(
            [Booleana(("PCD", true)), Booleana(("NOME", "MARIA"))], Catalogo(pcd, nome, derivado));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("regras[0]", FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia),
            ("regras[1]", FatoCandidatoErrorCodes.RegraCitaFatoNaoCitavel),
        ]);
    }

    private static Result DefinirContribuicao(TipoDoDado dominio, string contribui)
    {
        FatoCandidato renda = Declarado("TEM_RENDA");
        FatoCandidato derivado = Derivado("PERFIS", dominio);
        if (dominio == TipoDoDado.Opcao)
        {
            derivado.AdicionarValorDominio("EJA", "Educação de jovens e adultos", 0, ativo: true);
        }

        return derivado.DefinirRegrasPadrao([Regra(contribui, ("TEM_RENDA", true))], Catalogo(renda, derivado));
    }

    private static Result DefinirRegraQueCitaOCertificado(string pontoDoDerivado)
    {
        FatoCandidato certificado = Declarado("CERTIFICADO_EMITIDO", ponto: "HABILITACAO");
        FatoCandidato derivado = Derivado("PENDENTE_DE_CERTIFICADO", ponto: pontoDoDerivado);
        PrecedenciaFase[] precedencias =
        [
            PrecedenciaFase.Criar("INSCRICAO", "RESULTADO_FINAL", permiteSobreposicao: false, []).Value!,
            PrecedenciaFase.Criar("RESULTADO_FINAL", "HABILITACAO", permiteSobreposicao: false, []).Value!,
        ];

        return derivado.DefinirRegrasPadrao(
            [Booleana(("CERTIFICADO_EMITIDO", false))], new CatalogoDeFatos([certificado, derivado], precedencias));
    }

    private static (FatoCandidato Derivado, CatalogoDeFatos Catalogo) DerivadoQueCitaRendaComRendaEBolsaDesativadas()
    {
        FatoCandidato renda = Declarado("TEM_RENDA");
        FatoCandidato bolsa = Declarado("TEM_BOLSA");
        FatoCandidato derivado = Derivado("VULNERAVEL");
        CatalogoDeFatos catalogo = Catalogo(renda, bolsa, derivado);
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", false))], catalogo).IsSuccess.Should().BeTrue();
        renda.Desativar();
        bolsa.Desativar();
        return (derivado, catalogo);
    }

    private static FatoCandidato Derivado(
        string codigo,
        TipoDoDado dominio = TipoDoDado.SimOuNao,
        string ponto = "INSCRICAO",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Sensivel) =>
        CriacaoDoFato.CriarDerivadoDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, null, null, null, ponto, EscopoFato.Candidato, classificacao, Finalidade, Hipotese)).Value!;

    private static FatoCandidato Declarado(
        string codigo,
        TipoDoDado dominio = TipoDoDado.SimOuNao,
        string ponto = "INSCRICAO",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        FormatoTexto? formato = null,
        EscopoFato escopo = EscopoFato.Candidato) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, QuantidadeDeValores.ValorUnico, null, formato, ponto, escopo, classificacao, Finalidade, Hipotese)).Value!;

    private static CatalogoDeFatos Catalogo(params FatoCandidato[] fatos) => new(fatos, []);

    private static RegraDerivacao Booleana((string Fato, object Valor) condicao) =>
        RegraDerivacao.CriarBooleana(Quando(condicao));

    private static RegraDerivacao Regra(string contribui, (string Fato, object Valor) condicao) =>
        RegraDerivacao.Criar(Quando(condicao), contribui).Value!;

    private static PredicadoDnf Quando((string Fato, object Valor) condicao) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(condicao.Fato, Operador.Igual, JsonSerializer.SerializeToElement(condicao.Valor)).Value!)]).Value!;
}
