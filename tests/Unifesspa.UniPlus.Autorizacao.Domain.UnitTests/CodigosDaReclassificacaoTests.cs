namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Os códigos textuais gravados e expostos pela reclassificação e os erros que ela devolve.</summary>
public sealed class CodigosDaReclassificacaoTests
{
    [Fact(DisplayName = "Cada origem de base, resultado, tipo de divergência e origem de aviso volta do código que gravou")]
    public void Codigos_FazemIdaEVolta()
    {
        foreach (OrigemDaBase v in Enum.GetValues<OrigemDaBase>())
        {
            OrigemDaBaseCodigo.FromCodigo(v.ToCodigo()).Should().Be(v);
        }

        foreach (ResultadoDaLiberacao v in Enum.GetValues<ResultadoDaLiberacao>())
        {
            ResultadoDaLiberacaoCodigo.FromCodigo(v.ToCodigo()).Should().Be(v);
            ResultadoDaLiberacaoCodigo.TentarLer(v.ToCodigo()).Should().Be(v);
        }

        foreach (TipoDeDivergencia v in Enum.GetValues<TipoDeDivergencia>())
        {
            TipoDeDivergenciaCodigo.FromCodigo(v.ToCodigo()).Should().Be(v);
        }

        foreach (OrigemDoAviso v in Enum.GetValues<OrigemDoAviso>())
        {
            OrigemDoAvisoCodigo.FromCodigo(v.ToCodigo()).Should().Be(v);
        }
    }

    [Fact(DisplayName = "O código desconhecido é recusado ao ler e vira nulo ao tentar ler")]
    public void Codigos_Desconhecidos()
    {
        Action origemDaBase = () => OrigemDaBaseCodigo.FromCodigo("x");
        Action tipo = () => TipoDeDivergenciaCodigo.FromCodigo("x");
        Action resultado = () => ResultadoDaLiberacaoCodigo.FromCodigo("x");
        Action aviso = () => OrigemDoAvisoCodigo.FromCodigo("x");

        origemDaBase.Should().Throw<ArgumentOutOfRangeException>();
        tipo.Should().Throw<ArgumentOutOfRangeException>();
        resultado.Should().Throw<ArgumentOutOfRangeException>();
        aviso.Should().Throw<ArgumentOutOfRangeException>();
        ResultadoDaLiberacaoCodigo.TentarLer("x").Should().BeNull();
        ResultadoDaLiberacaoCodigo.TentarLer(null).Should().BeNull();
    }

    [Fact(DisplayName = "Cada erro da reclassificação leva o código e a mensagem em português")]
    public void Erros_TemCodigoEMensagem()
    {
        DomainError[] erros =
        [
            ReclassificacaoErros.NaoEncontrada(), ReclassificacaoErros.Superada(), ReclassificacaoErros.EtapaJaDecidida(),
            ReclassificacaoErros.ConcessaoForaDaReclassificacao(), ReclassificacaoErros.EtapaNaoExigida(),
            ReclassificacaoErros.DecisorEBeneficiario(), ReclassificacaoErros.MesmoDecisorNasDuasEtapas(),
            ReclassificacaoErros.DocumentoNaoLido(), ReclassificacaoErros.EscopoNaoComporta(), ReclassificacaoErros.MotivoInvalido(500),
            ReclassificacaoErros.SemPermissaoNaEtapa(), ReclassificacaoErros.EtapaInvalida(), ReclassificacaoErros.ResultadoInvalido(),
            ReclassificacaoErros.AlvoInvalido(),
        ];

        erros.Select(static e => e.Code).Should().OnlyHaveUniqueItems().And.OnlyContain(static c => c.StartsWith("Reclassificacao.", StringComparison.Ordinal));
        erros.Select(static e => e.Message).Should().OnlyContain(static m => !string.IsNullOrWhiteSpace(m));
    }

    [Fact(DisplayName = "A divergência de mudança guarda o valor de antes e de depois, e a de composição, nenhum")]
    public void Divergencia_GuardaOsValores()
    {
        DivergenciaDaGovernanca mudanca = DivergenciaDaGovernanca.DaMudanca(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "a:b:c", TipoDeDivergencia.Classificacao, new ValorDivergente("interno", "sensivel"), PerfisDeTeste.Instante, 0);
        DivergenciaDaGovernanca composicao = DivergenciaDaGovernanca.DaComposicao(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "a:b:c", TipoDeDivergencia.ComposicaoFaltante, PerfisDeTeste.Instante, 0);

        (mudanca.De, mudanca.Para).Should().Be(("interno", "sensivel"));
        (composicao.De, composicao.Para).Should().Be((null, null));
    }
}
