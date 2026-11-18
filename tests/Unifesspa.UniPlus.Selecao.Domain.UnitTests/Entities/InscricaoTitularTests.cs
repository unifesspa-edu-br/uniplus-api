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

public sealed class InscricaoTitularTests
{
    [Fact(DisplayName = "Iniciar fixa a versão do formulário e nasce em Rascunho, revisão 1")]
    public void Iniciar_FixaVersaoENasceRascunho()
    {
        Result<Inscricao> resultado = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer", VersaoFormulario);

        resultado.IsSuccess.Should().BeTrue();
        Inscricao inscricao = resultado.Value!;
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        inscricao.Formulario.VersaoFormularioId.Should().Be(VersaoFormulario);
        inscricao.Estado.NumeroInscricao.Should().BeNull("o número só nasce na primeira submissão");
        inscricao.Revisao.Should().Be(1);
    }

    [Fact(DisplayName = "Iniciar grava a resposta do CPF com o CPF da conta, cifrada como identificador")]
    public void Iniciar_GravaARespostaDoCpfDaConta()
    {
        Inscricao inscricao = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer", VersaoFormulario).Value!;

        inscricao.Formulario.Preenchimento.Respostas["CPF"].GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Valor);
        inscricao.Formulario.DefinicoesDasRespostas["CPF"].Should().Match<DefinicaoDaResposta>(d => d.Identificador && d.Cifrada);
    }

    [Fact(DisplayName = "Iniciar sem conta é recusado — o token precisa do claim")]
    public void Iniciar_SemContaId_RetornaFalha()
    {
        Result<Inscricao> resultado = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "", "https://issuer", VersaoFormulario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ContaIdObrigatorio);
    }

    [Fact(DisplayName = "Iniciar sem emissor é recusado")]
    public void Iniciar_SemEmissor_RetornaFalha()
    {
        Result<Inscricao> resultado = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "conta-1", "", VersaoFormulario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.EmissorObrigatorio);
    }

    [Theory(DisplayName = "ConferirDono sem conta no token recusa com ContaIdObrigatorio, sem exceção")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConferirDono_SemContaId_RecusaComContaIdObrigatorio(string? contaId)
    {
        Inscricao inscricao = NovaInscricao();

        inscricao.Titular.ConferirDono(contaId)!.Code.Should().Be(InscricaoErrorCodes.ContaIdObrigatorio);
    }

    [Fact(DisplayName = "ConferirDono recusa conta diferente do gravado")]
    public void ConferirDono_ContaIdDiferente_RetornaFalha()
    {
        Inscricao inscricao = NovaInscricao();

        inscricao.Titular.ConferirDono("outra-conta")!.Code.Should().Be(InscricaoErrorCodes.DonoDivergente);
        inscricao.Titular.ConferirDono("conta-1").Should().BeNull("a mesma conta que iniciou é o dono");
    }
}
