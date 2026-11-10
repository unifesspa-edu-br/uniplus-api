namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CodigosDaReclassificacaoTests
{
    [Theory(DisplayName = "O código textual de cada origem de base volta ao mesmo valor")]
    [InlineData(OrigemDaBase.Migracao)]
    [InlineData(OrigemDaBase.Saida)]
    [InlineData(OrigemDaBase.Liberacao)]
    [InlineData(OrigemDaBase.Conciliacao)]
    public void OrigemDaBase_IdaEVolta(OrigemDaBase valor) =>
        OrigemDaBaseCodigo.FromCodigo(valor.ToCodigo()).Should().Be(valor);

    [Theory(DisplayName = "O código textual de cada tipo de divergência volta ao mesmo valor")]
    [InlineData(TipoDeDivergencia.ComposicaoFaltante)]
    [InlineData(TipoDeDivergencia.ComposicaoExcedente)]
    [InlineData(TipoDeDivergencia.Classificacao)]
    [InlineData(TipoDeDivergencia.Escopo)]
    public void TipoDeDivergencia_IdaEVolta(TipoDeDivergencia valor) =>
        TipoDeDivergenciaCodigo.FromCodigo(valor.ToCodigo()).Should().Be(valor);

    [Theory(DisplayName = "O código textual de cada origem de aviso volta ao mesmo valor")]
    [InlineData(OrigemDoAviso.Reclassificacao)]
    [InlineData(OrigemDoAviso.Divergencia)]
    public void OrigemDoAviso_IdaEVolta(OrigemDoAviso valor) =>
        OrigemDoAvisoCodigo.FromCodigo(valor.ToCodigo()).Should().Be(valor);

    [Theory(DisplayName = "O código textual de cada resultado da liberação volta ao mesmo valor")]
    [InlineData(ResultadoDaLiberacao.Liberada)]
    [InlineData(ResultadoDaLiberacao.Mantida)]
    public void ResultadoDaLiberacao_IdaEVolta(ResultadoDaLiberacao valor)
    {
        ResultadoDaLiberacaoCodigo.FromCodigo(valor.ToCodigo()).Should().Be(valor);
        ResultadoDaLiberacaoCodigo.TentarLer(valor.ToCodigo()).Should().Be(valor);
    }

    [Fact(DisplayName = "Um código desconhecido é recusado em cada conversão e o resultado desconhecido não é lido")]
    public void CodigoDesconhecido_Recusado()
    {
        FluentActions.Invoking(() => OrigemDaBaseCodigo.FromCodigo("x")).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => TipoDeDivergenciaCodigo.FromCodigo("x")).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => OrigemDoAvisoCodigo.FromCodigo("x")).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ResultadoDaLiberacaoCodigo.FromCodigo("x")).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ((OrigemDaBase)99).ToCodigo()).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ((TipoDeDivergencia)99).ToCodigo()).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ((OrigemDoAviso)99).ToCodigo()).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ((ResultadoDaLiberacao)99).ToCodigo()).Should().Throw<ArgumentOutOfRangeException>();
        ResultadoDaLiberacaoCodigo.TentarLer("x").Should().BeNull();
    }

    [Fact(DisplayName = "Cada erro da reclassificação traz um código próprio e uma mensagem em português")]
    public void Erros_TemCodigoProprioEMensagem()
    {
        DomainError[] erros =
        [
            ReclassificacaoErros.Superada(),
            ReclassificacaoErros.EtapaJaDecidida(),
            ReclassificacaoErros.ConcessaoForaDaReclassificacao(),
            ReclassificacaoErros.EtapaNaoExigida(),
            ReclassificacaoErros.DecisorEBeneficiario(),
            ReclassificacaoErros.MesmoDecisorNasDuasEtapas(),
            ReclassificacaoErros.DocumentoNaoLido(),
            ReclassificacaoErros.EscopoNaoComporta(),
            ReclassificacaoErros.MotivoInvalido(500),
            ReclassificacaoErros.SemPermissaoNaEtapa(),
        ];

        erros.Select(e => e.Code).Should().OnlyHaveUniqueItems();
        erros.Should().OnlyContain(e => e.Message.Length > 0);
    }
}
