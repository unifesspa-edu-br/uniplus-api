namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

public sealed class CondicaoDeHomologacaoComEnquadramentoRecebidoTests
{
    [Fact]
    public void DeclararComOEnquadramentoRecebido_CausasIndependentes_AcumulaNaOrdem()
    {
        FieldError[] causas = [new("[0][0].valor", new DomainError("teste.primeiro", "Primeira causa.")), new("[0][1].valor", new DomainError("teste.segundo", "Segunda causa."))];
        Result<PredicadoDnf?> enquadramento = Result<PredicadoDnf?>.ValidationFailure(causas);
        Result<CondicaoDeHomologacao> esperado = CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, 1997, "motivo inválido", null);

        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.DeclararComOEnquadramentoRecebido(CodigoDoRetornoDoInep, 1997, "motivo inválido", enquadramento);

        resultado.Errors.Should().Equal([.. esperado.Errors, .. causas.Select(causa => causa.NoCaminhoDoItem("enquadramento"))]);
        resultado.Errors.Select(erro => erro.Field).Should().Equal("edicaoDoExame", "motivoDesfavoravelCodigo", "enquadramento[0][0].valor", "enquadramento[0][1].valor");
    }

    [Fact]
    public void DeclararComOEnquadramentoRecebido_SemCampoDoEnquadramento_ApontaEnquadramento()
    {
        Result<PredicadoDnf?> enquadramento = Result<PredicadoDnf?>.Failure(new DomainError("teste.forma", "Forma inválida."));
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.DeclararComOEnquadramentoRecebido(CodigoDoRetornoDoInep, 2025, null, enquadramento);
        resultado.Errors.Select(erro => (erro.Field, erro.Error.Code, erro.Error.Message)).Should().Equal(("enquadramento", "teste.forma", "Forma inválida."));
    }

    [Fact]
    public void DeclararComOEnquadramentoRecebido_SemRecusas_MantemEnquadramento()
    {
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("DADO_FICTICIO", Operador.Igual, JsonSerializer.SerializeToElement("opção")).Value!)]).Value!;
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.DeclararComOEnquadramentoRecebido(CodigoDoRetornoDoInep, 2025, null, Result<PredicadoDnf?>.Success(predicado));
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Enquadramento.Should().BeSameAs(predicado);
    }

    [Fact]
    public void DeclararComOEnquadramentoRecebido_CondicaoInvalidaEEnquadramentoValido_RecusaSomenteCondicao()
    {
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("DADO_FICTICIO", Operador.Igual, JsonSerializer.SerializeToElement("opção")).Value!)]).Value!;
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.DeclararComOEnquadramentoRecebido(CodigoDoRetornoDoInep, 1997, "motivo inválido", Result<PredicadoDnf?>.Success(predicado));
        resultado.Errors.Should().Equal(CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, 1997, "motivo inválido", predicado).Errors);
    }

    [Fact]
    public void DeclararComOEnquadramentoRecebido_EnquadramentoAusente_AcumulaSomenteCausasDaCondicao()
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.DeclararComOEnquadramentoRecebido(CodigoDoRetornoDoInep, 1997, "motivo inválido", Result<PredicadoDnf?>.Success(null));
        resultado.Errors.Should().Equal(CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, 1997, "motivo inválido", null).Errors);
    }
}
