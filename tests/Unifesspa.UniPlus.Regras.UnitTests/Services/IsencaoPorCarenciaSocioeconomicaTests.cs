namespace Unifesspa.UniPlus.Regras.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O direito à isenção da taxa pela Lei nº 12.799/2013 (UNI-REQ-0149): perfis de renda, de como o
/// candidato concluiu e de onde cursou o ensino médio.
/// </summary>
public sealed class IsencaoPorCarenciaSocioeconomicaTests
{
    [Theory(DisplayName = "A isenção por carência socioeconômica segue a renda e a origem escolar que a lei admite")]
    // Têm direito: curso regular ou EJA, na rede pública ou com bolsa integral na rede privada.
    [InlineData(true, OrigemEscolar.Regular, OrigemEscolar.SomentePublica, true)]
    [InlineData(true, OrigemEscolar.Eja, OrigemEscolar.PrivadaComBolsaIntegral, true)]
    [InlineData(true, OrigemEscolar.Regular, OrigemEscolar.PublicaEPrivadaComBolsaIntegral, true)]
    // Renda acima de um salário mínimo e meio, mesmo com a origem escolar que a lei admite.
    [InlineData(false, OrigemEscolar.Regular, OrigemEscolar.SomentePublica, false)]
    // Certificação não é ensino médio completo cursado, com ou sem anos em escola pública.
    [InlineData(true, OrigemEscolar.Encceja, OrigemEscolar.SomentePublica, false)]
    [InlineData(true, OrigemEscolar.Enem, OrigemEscolar.ConcluiuPorCertificacao, false)]
    [InlineData(true, OrigemEscolar.Proficiencia, OrigemEscolar.ConcluiuPorCertificacao, false)]
    // A lei não cita a escola comunitária do campo conveniada, no todo ou em parte.
    [InlineData(true, OrigemEscolar.Regular, OrigemEscolar.ComunitariaDoCampo, false)]
    [InlineData(true, OrigemEscolar.Regular, OrigemEscolar.PublicaEComunitaria, false)]
    // Bolsa parcial, sem bolsa, parte fora da rede pública sem bolsa integral, Sistema S e exterior.
    [InlineData(true, OrigemEscolar.Regular, OrigemEscolar.PrivadaComBolsaParcial, false)]
    [InlineData(true, OrigemEscolar.Regular, OrigemEscolar.Privada, false)]
    [InlineData(true, OrigemEscolar.Eja, OrigemEscolar.PublicaEForaDaRede, false)]
    [InlineData(true, OrigemEscolar.Regular, OrigemEscolar.SistemaS, false)]
    [InlineData(true, OrigemEscolar.Regular, OrigemEscolar.Exterior, false)]
    [InlineData(true, OrigemEscolar.Regular, OrigemEscolar.MaisDeUmForaDaRede, false)]
    public void Avaliar_PerfilDoPedido(bool rendaAteUmSalarioEMeio, string formaDeConclusao, string ondeCursou, bool temDireito)
    {
        Ternario resultado = IsencaoPorCarenciaSocioeconomica.Condicao.Avaliar(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)
        {
            [IsencaoPorCarenciaSocioeconomica.FatoRenda] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(rendaAteUmSalarioEMeio)),
            [OrigemEscolar.FatoFormaDeConclusao] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(formaDeConclusao)),
            [OrigemEscolar.FatoOndeCursou] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(ondeCursou)),
        });

        resultado.Should().Be(temDireito ? Ternario.Verdadeiro : Ternario.Falso);
    }
}
