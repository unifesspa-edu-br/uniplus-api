namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CodigosDeContratoDasDependenciasDaConfiguracaoTests
{
    [Fact]
    public void TipoDeParte_CodigosERotulos_SaoEstaveis() =>
        Enum.GetValues<TipoDeParteDaConfiguracao>().Select(static v => (v, v.Codigo(), v.Rotulo())).Should().Equal(
            (TipoDeParteDaConfiguracao.Campo, "CAMPO", "Campo do formulário"),
            (TipoDeParteDaConfiguracao.Dado, "DADO", "Dado do candidato"),
            (TipoDeParteDaConfiguracao.Exigencia, "EXIGENCIA", "Exigência de documento"),
            (TipoDeParteDaConfiguracao.Secao, "SECAO", "Seção do formulário"),
            (TipoDeParteDaConfiguracao.Termo, "TERMO", "Termo de consentimento"));

    [Fact]
    public void Motivo_CodigosERotulos_SaoEstaveis() =>
        Enum.GetValues<MotivoDaDependencia>().Select(static v => (v, v.Codigo(), v.Rotulo())).Should().Equal(
            (MotivoDaDependencia.InformaDado, "RESPOSTA_DO_CAMPO", "Informa o dado"),
            (MotivoDaDependencia.CondicionaExibicao, "EXIBICAO", "Condiciona a exibição"),
            (MotivoDaDependencia.CalculaDado, "CALCULO", "Calcula o dado"),
            (MotivoDaDependencia.CondicionaExigencia, "CONDICAO_DA_EXIGENCIA", "Condiciona a exigência"));

    [Fact]
    public void Motivo_OrdemNumerica_DefineOrdemNoCongelado() =>
        Enum.GetValues<MotivoDaDependencia>().Select(static v => ((int)v, v.Codigo())).Should().Equal(
            (0, "RESPOSTA_DO_CAMPO"), (1, "EXIBICAO"), (2, "CALCULO"), (3, "CONDICAO_DA_EXIGENCIA"));

    [Fact]
    public void TipoDeParte_OrdemNumerica_DefineDesempate() =>
        Enum.GetValues<TipoDeParteDaConfiguracao>().Select(static v => (int)v).Should().Equal(0, 1, 2, 3, 4);

    [Fact]
    public void Valores_Descricoes_Preenchidas()
    {
        Enum.GetValues<TipoDeParteDaConfiguracao>().Should().OnlyContain(static v => !string.IsNullOrWhiteSpace(v.Descricao()));
        Enum.GetValues<MotivoDaDependencia>().Should().OnlyContain(static v => !string.IsNullOrWhiteSpace(v.Descricao()));
    }
}
