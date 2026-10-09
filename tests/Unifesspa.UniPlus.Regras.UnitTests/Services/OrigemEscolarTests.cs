namespace Unifesspa.UniPlus.Regras.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O egresso de escola pública derivado da origem escolar, para as cotas da Lei nº 12.711/2012
/// (UNI-REQ-0148): perfis de como o candidato concluiu e onde cursou o ensino médio.
/// </summary>
public sealed class OrigemEscolarTests
{
    [Theory(DisplayName = "O egresso de escola pública segue onde e como o candidato cursou o ensino médio")]
    // Rede pública, qualquer que seja a forma de conclusão.
    [InlineData(OrigemEscolar.Regular, OrigemEscolar.SomentePublica, true)]
    [InlineData(OrigemEscolar.Eja, OrigemEscolar.ComunitariaDoCampo, true)]
    [InlineData(OrigemEscolar.Enem, OrigemEscolar.PublicaEComunitaria, true)]
    [InlineData(OrigemEscolar.Encceja, OrigemEscolar.SomentePublica, true)]
    // Bolsa integral na rede privada, no todo ou em parte, não é rede pública.
    [InlineData(OrigemEscolar.Regular, OrigemEscolar.PrivadaComBolsaIntegral, false)]
    [InlineData(OrigemEscolar.Regular, OrigemEscolar.PublicaEPrivadaComBolsaIntegral, false)]
    // Parte fora da rede pública, Sistema S e exterior não contam, nem com certificação.
    [InlineData(OrigemEscolar.Eja, OrigemEscolar.PublicaEForaDaRede, false)]
    [InlineData(OrigemEscolar.Encceja, OrigemEscolar.SistemaS, false)]
    [InlineData(OrigemEscolar.Regular, OrigemEscolar.MaisDeUmForaDaRede, false)]
    // Certificação sem frequentar o ensino médio: ENCCEJA, proficiência e ENEM contam.
    [InlineData(OrigemEscolar.Encceja, OrigemEscolar.ConcluiuPorCertificacao, true)]
    [InlineData(OrigemEscolar.Proficiencia, OrigemEscolar.ConcluiuPorCertificacao, true)]
    [InlineData(OrigemEscolar.Enem, OrigemEscolar.ConcluiuPorCertificacao, true)]
    public void Derivar_PerfilDeOrigemEscolar(string formaDeConclusao, string ondeCursou, bool egresso)
    {
        ResultadoDerivacao resultado = MotorDerivacao.Derivar(
            OrigemEscolar.DerivacaoDoEgresso(),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)
            {
                [OrigemEscolar.FatoFormaDeConclusao] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(formaDeConclusao)),
                [OrigemEscolar.FatoOndeCursou] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(ondeCursou)),
            });

        resultado.Estado.Should().Be(EstadoFato.Resolvido);
        resultado.ValorBooleano.Should().Be(egresso);
    }
}
