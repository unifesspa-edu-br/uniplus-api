namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que a regra de aplicabilidade da modalidade pode citar no catálogo de fatos (ADR-0144): fato
/// citável e ativo, conhecido até a inscrição, com operador e valor do domínio dele, e sem depender
/// da própria modalidade.
/// </summary>
public sealed class ConferenciaDaRegraDeAplicabilidadeTests
{
    private const string Finalidade = "Enquadramento na reserva de vagas.";
    private const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    [Fact(DisplayName = "Regra que cita fato citável, ativo e conhecido na inscrição é aceita")]
    public void Conferir_RegraValida_Aceita() =>
        ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("PCD", true)), vigente: null, Catalogo(Declarado("PCD")))
            .Should().BeEmpty();

    [Fact(DisplayName = "Texto não entra na regra de aplicabilidade")]
    public void Conferir_CitaTexto_Recusa()
    {
        FatoCandidato nome = Declarado("NOME", DominioFato.Texto, formato: FormatoTexto.NomePessoa);

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("NOME", "MARIA")), vigente: null, Catalogo(nome)))
            .Should().Equal(ModalidadeErrorCodes.RegraCitaFatoNaoCitavel);
    }

    [Fact(DisplayName = "Fato desativado recusa citação nova e mantém a que a regra vigente já fazia")]
    public void Conferir_FatoDesativado_SoRecusaCitacaoNova()
    {
        FatoCandidato pcd = Declarado("PCD");
        pcd.Desativar();
        CatalogoDeFatos catalogo = Catalogo(pcd);

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("PCD", true)), vigente: null, catalogo))
            .Should().Equal(ModalidadeErrorCodes.RegraCitaFatoDesativado);
        ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("PCD", true)), vigente: Quando(("PCD", true)), catalogo)
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "Valor desativado recusa citação nova e mantém a que a regra vigente já fazia")]
    public void Conferir_ValorDesativado_SoRecusaCitacaoNova()
    {
        FatoCandidato corRaca = Declarado("COR_RACA", DominioFato.Categorico);
        corRaca.AdicionarValorDominio("PARDA", "Parda", 0, ativo: true);
        corRaca.DesativarValor("PARDA");
        CatalogoDeFatos catalogo = Catalogo(corRaca);

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("COR_RACA", "PARDA")), vigente: null, catalogo))
            .Should().Equal(ModalidadeErrorCodes.RegraCitaValorDesativado);
        ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("COR_RACA", "PARDA")), vigente: Quando(("COR_RACA", "PARDA")), catalogo)
            .Should().BeEmpty();
    }

    [Theory(DisplayName = "Só fato conhecido até a inscrição decide a modalidade; fase sem precedência até ela é recusada")]
    [InlineData("INSCRICAO", true)]
    [InlineData("SOLICITACAO_ISENCAO", true)]
    [InlineData("RESULTADO_FINAL", false)]
    [InlineData("HETEROIDENTIFICACAO", false)]
    public void Conferir_PontoResolucao(string ponto, bool aceita)
    {
        FatoCandidato aprovado = Declarado("APROVADO_NA_BANCA", ponto: ponto);
        PrecedenciaFase[] precedencias =
        [
            PrecedenciaFase.Criar("SOLICITACAO_ISENCAO", "INSCRICAO", permiteSobreposicao: false, []).Value!,
            PrecedenciaFase.Criar("INSCRICAO", "RESULTADO_FINAL", permiteSobreposicao: false, []).Value!,
        ];

        IReadOnlyList<FieldError> erros = ConferenciaDaRegraDeAplicabilidade.Conferir(
            Quando(("APROVADO_NA_BANCA", true)), vigente: null, new CatalogoDeFatos([aprovado], precedencias));

        Codigos(erros).Should().Equal(aceita ? [] : [ModalidadeErrorCodes.RegraCitaFatoPosteriorAInscricao]);
    }

    [Fact(DisplayName = "Operador incompatível com o domínio do fato é recusado")]
    public void Conferir_OperadorIncompativel_Recusa()
    {
        PredicadoDnf regra = Predicado(CondicaoDnf.Criar(
            "PCD", Operador.MaiorIgual, JsonSerializer.SerializeToElement(1)).Value!);

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(regra, vigente: null, Catalogo(Declarado("PCD"))))
            .Should().Equal(PredicadoDnfErrorCodes.OperadorIncompativelComDominio);
    }

    [Fact(DisplayName = "Regra que cita a própria modalidade é recusada")]
    public void Conferir_CitaAModalidade_Recusa()
    {
        FatoCandidato modalidade = Modalidade();

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(
                Quando((ModalidadeDoCandidato.Fato, "AC")), vigente: null, Catalogo(modalidade)))
            .Should().Equal(ModalidadeErrorCodes.RegraDependeDaModalidade);
    }

    [Fact(DisplayName = "Regra que cita derivado cujas regras padrão chegam à modalidade é recusada")]
    public void Conferir_CitaDerivadoDaModalidade_Recusa()
    {
        FatoCandidato modalidade = Modalidade();
        FatoCandidato concorreNaAmpla = Derivado("CONCORRE_NA_AMPLA");
        CatalogoDeFatos catalogo = Catalogo(modalidade, concorreNaAmpla);
        concorreNaAmpla.DefinirRegrasPadrao(
            [RegraDerivacao.CriarBooleana(Quando((ModalidadeDoCandidato.Fato, "AC")))], catalogo).IsSuccess.Should().BeTrue();

        Codigos(ConferenciaDaRegraDeAplicabilidade.Conferir(Quando(("CONCORRE_NA_AMPLA", true)), vigente: null, catalogo))
            .Should().Equal(ModalidadeErrorCodes.RegraDependeDaModalidade);
    }

    [Fact(DisplayName = "As recusas de condições distintas se acumulam no campo da regra")]
    public void Conferir_RecusasAcumuladas()
    {
        FatoCandidato pcd = Declarado("PCD");
        pcd.Desativar();
        FatoCandidato modalidade = Modalidade();

        IReadOnlyList<FieldError> erros = ConferenciaDaRegraDeAplicabilidade.Conferir(
            Quando(("PCD", true), (ModalidadeDoCandidato.Fato, "AC")), vigente: null, Catalogo(pcd, modalidade));

        erros.Should().AllSatisfy(static e => e.Field.Should().Be("regraDeAplicabilidade"));
        Codigos(erros).Should().BeEquivalentTo(
            [ModalidadeErrorCodes.RegraCitaFatoDesativado, ModalidadeErrorCodes.RegraDependeDaModalidade]);
    }

    private static string[] Codigos(IReadOnlyList<FieldError> erros) => [.. erros.Select(static e => e.Error.Code)];

    private static FatoCandidato Modalidade()
    {
        FatoCandidato modalidade = Derivado(ModalidadeDoCandidato.Fato, DominioFato.Categorico);
        modalidade.AdicionarValorDominio("AC", "Ampla concorrência", 0, ativo: true);
        return modalidade;
    }

    private static FatoCandidato Derivado(string codigo, DominioFato dominio = DominioFato.Booleano) =>
        FatoCandidato.CriarDerivadoDoAdministrador(
            codigo, codigo, null, dominio, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoDeProtecao.Sensivel, Finalidade, Hipotese).Value!;

    private static FatoCandidato Declarado(
        string codigo,
        DominioFato dominio = DominioFato.Booleano,
        string ponto = "INSCRICAO",
        FormatoTexto? formato = null) =>
        FatoCandidato.CriarDoAdministrador(
            codigo, codigo, null, dominio, CardinalidadeFato.Escalar,
            dominio == DominioFato.Categorico ? FonteValoresFato.Global : null, formato, ponto, EscopoFato.Candidato,
            ClassificacaoDeProtecao.Sensivel, Finalidade, Hipotese).Value!;

    private static CatalogoDeFatos Catalogo(params FatoCandidato[] fatos) => new(fatos, []);

    private static PredicadoDnf Quando(params (string Fato, object Valor)[] condicoes) =>
        Predicado([.. condicoes.Select(static c =>
            CondicaoDnf.Criar(c.Fato, Operador.Igual, JsonSerializer.SerializeToElement(c.Valor)).Value!)]);

    private static PredicadoDnf Predicado(params CondicaoDnf[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. condicoes.Select(static c => (0, c))]).Value!;
}
