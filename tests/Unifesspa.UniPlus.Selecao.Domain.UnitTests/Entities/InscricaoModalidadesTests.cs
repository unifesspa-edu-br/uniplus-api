namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static InscricaoDeTeste;

public sealed class InscricaoModalidadesTests
{
    [Fact(DisplayName = "Com a modalidade derivada e nenhuma aplicável, o envio é recusado em cada campo e grupo de que ela depende, sem mudar a inscrição")]
    public void Submeter_NenhumaModalidade_RecusaNosCamposQueADecidem()
    {
        Inscricao inscricao = NovaInscricao();
        int revisao = inscricao.Revisao;

        Result<ComprovanteInscricao> resultado = SubmeterCom(inscricao, ComModalidades(SituacaoDasModalidades.NaoConcorre));

        resultado.Errors.Select(static e => e.Field).Should().Equal("CONCORRER_PCD", "COMPOSICAO_FAMILIAR");
        resultado.Errors.Should().AllSatisfy(static e => e.Error.Code.Should().Be(InscricaoErrorCodes.NenhumaModalidade));
        resultado.Errors[0].Error.Message.Should().EndWith("consulte o edital.");
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        inscricao.Revisao.Should().Be(revisao);
        resultado.Value.Should().BeNull("a recusa não emite comprovante");
    }

    [Fact(DisplayName = "Com a modalidade derivada ainda indecidida, o envio é recusado em vez de sair sem modalidade")]
    public void Submeter_ModalidadeNaoDecidida_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = SubmeterCom(inscricao, ComModalidades(SituacaoDasModalidades.FaltaResponder));

        resultado.Errors.Should().ContainSingle().Which.Should().Be(new FieldError(
            "MODALIDADE", resultado.Errors[0].Error));
        resultado.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.ModalidadeNaoDecidida);
        resultado.Value.Should().BeNull("a recusa não emite comprovante");
    }

    [Fact(DisplayName = "O envio grava os códigos da modalidade como derivado, na ordem das ofertadas, e o comprovante registra a seção com o nome")]
    public void Submeter_ComModalidade_GravaODerivadoEOComprovante()
    {
        Inscricao inscricao = NovaInscricao();

        ComprovanteInscricao comprovante = SubmeterCom(inscricao, ComModalidades(
            SituacaoDasModalidades.Concorre,
            new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0),
            new ModalidadeDeConcorrencia("LI_EP", "Escola pública", 3))).Value!;

        inscricao.Formulario.Derivados["MODALIDADE"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC", "LI_EP");
        inscricao.Formulario.DefinicoesDasRespostas["MODALIDADE"].Should().Be(
            new DefinicaoDaResposta(TipoDoValorDeResposta.Codigo, Multivalorado: true, Identificador: false));
        comprovante.Modalidades.Should().Equal(
            new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0), new ModalidadeDeConcorrencia("LI_EP", "Escola pública", 3));
        comprovante.Derivados.Should().NotContainKey("MODALIDADE", "a seção das modalidades já a registra, com o nome");
        comprovante.IntegridadeConfere().Should().BeTrue();
    }

    [Fact(DisplayName = "Sem a modalidade derivada, o envio não grava modalidade e o comprovante não tem a seção")]
    public void Submeter_SemDerivacaoDaModalidade_ComprovanteSemASecao()
    {
        Inscricao inscricao = NovaInscricao();

        ComprovanteInscricao comprovante = SubmeterCom(inscricao, FormularioDeTeste.AvaliacaoSemOcultos).Value!;

        inscricao.Formulario.Derivados.Should().NotContainKey("MODALIDADE");
        comprovante.Modalidades.Should().BeNull();
    }

    [Fact(DisplayName = "A correção sem modalidade aplicável é recusada e não muda a inscrição já enviada nem o comprovante dela")]
    public void Submeter_CorrecaoSemModalidade_NaoMudaOEnvioAnterior()
    {
        Inscricao inscricao = NovaInscricao();
        ComprovanteInscricao primeiro = SubmeterCom(
            inscricao, ComModalidades(SituacaoDasModalidades.Concorre, new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0))).Value!;
        int revisao = inscricao.Revisao;

        Result<ComprovanteInscricao> correcao = SubmeterCom(inscricao, ComModalidades(SituacaoDasModalidades.NaoConcorre));

        correcao.IsFailure.Should().BeTrue();
        inscricao.Revisao.Should().Be(revisao);
        correcao.Value.Should().BeNull("a recusa não emite comprovante");
        primeiro.IntegridadeConfere().Should().BeTrue("o comprovante já emitido não muda");
        inscricao.Formulario.Derivados["MODALIDADE"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC");
    }
}
