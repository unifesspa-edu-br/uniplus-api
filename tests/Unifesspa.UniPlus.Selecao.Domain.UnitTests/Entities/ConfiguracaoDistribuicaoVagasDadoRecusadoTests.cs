namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConfiguracaoDeVagas;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas.DistribuicaoDeVagasDeTeste;

/// <summary>
/// A quantidade negativa é recusada em um lugar só, na forma da declaração, e a declaração com dado recusado
/// na resolução do cadastro nunca vira configuração.
/// </summary>
public sealed class ConfiguracaoDistribuicaoVagasDadoRecusadoTests
{
    private static readonly FatosDaOfertaNoCadastro FatosDeTeste = new() { OfertaCurso = OfertaCursoDeTeste.Medicina };

    [Fact(DisplayName = "Criar recusa a quantidade negativa declarada em uma modalidade, no campo da linha do quadro")]
    public void Criar_QuantidadeNegativaDeclarada_Recusa()
    {
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(Fixada("IND", 60), Fixada("QUI", -3));

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(), declarada, FatosDeTeste);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            erro => erro.Field == "quadro[1].quantidade"
                && erro.Error.Code == DistribuicaoDeVagasErrorCodes.QuantidadeVagaNegativa
                && erro.Error.Message == "A quantidade de vagas declarada não pode ser negativa.");
    }

    [Fact(DisplayName = "A distribuição federal recusa a retirada negativa antes de calcular o quadro")]
    public void Criar_RetiradaNegativaNaLeiDeCotas_Recusa()
    {
        ModalidadeSelecionada retirada = ApoioDaConfiguracaoDeVagas.Modalidade(
            "V", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, -1, "AC");
        DistribuicaoDeVagasDeclarada declarada = DaLeiDeCotas() with
        {
            Modalidades = [.. AsOitoFederaisMaisAmplaConcorrencia(), retirada],
        };

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(), declarada, FatosDeTeste);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            erro => erro.Field == "quadro[9].quantidade" && erro.Error.Code == DistribuicaoDeVagasErrorCodes.QuantidadeVagaNegativa);
        resultado.Value.Should().BeNull();
    }

    [Fact(DisplayName = "Criar lança quando a declaração traz dado recusado")]
    public void Criar_ComDadoRecusado_Lanca()
    {
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(Fixada("IND", 50)) with
        {
            Recusados = Recusando(DadoDaDistribuicaoDeVagas.Modalidades),
        };

        Action criar = () => ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(), declarada, FatosDeTeste);

        criar.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Montar o quadro lança quando a declaração traz dado recusado")]
    public void Montar_ComDadoRecusado_Lanca()
    {
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(Fixada("IND", 50)) with
        {
            Recusados = Recusando(DadoDaDistribuicaoDeVagas.ReferenciaDemografica),
        };

        Action montar = () => MontagemDoQuadroDeVagas.Montar(declarada);

        montar.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "A conferência da declaração com modalidade recusada não confere a citação fora das aceitas")]
    public void ConferirDeclaracao_ModalidadeRecusada_NaoConfereCitacaoForaDasAceitas()
    {
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(ModalidadeQueCitaUmDestino("IND", destino: "AUSENTE"));

        ConfiguracaoDistribuicaoVagas.ConferirDeclaracao(declarada, FatosDeTeste)
            .Should().ContainSingle(recusa => recusa.Error.Code == DistribuicaoDeVagasErrorCodes.RemanejamentoDestinoNaoSelecionado);
        ConfiguracaoDistribuicaoVagas.ConferirDeclaracao(
            declarada with { Recusados = Recusando(DadoDaDistribuicaoDeVagas.Modalidades) }, FatosDeTeste)
            .Should().BeEmpty();
    }

    private static ModalidadeSelecionada ModalidadeQueCitaUmDestino(string codigo, string destino) =>
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(codigo, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal) with { RegraRemanejamento = RegraRemanejamentoModalidade.DestinoUnico, RemanejamentoDestino = destino, BaseLegal = "base legal" }, 50).Value!;
}
