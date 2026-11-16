namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services.CenarioDeConformidadeLegal;

/// <summary>
/// Forma do resultado de <see cref="AvaliadorConformidadeLegal"/>: uma avaliação por regra de entrada, lista vazia
/// aprova, e o predicado customizado nunca reprova nem lança, só emite aviso.
/// </summary>
public sealed class AvaliadorConformidadeLegalResultadoTests
{
    [Fact(DisplayName = "CA-01: sem nenhuma regra vigente, a avaliação aprova (lista vazia)")]
    public void SemRegras_Aprova()
    {
        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            NovoProcesso(), TipoProcessoAvaliado, [], IdentidadesDeCadastro.Vazio, FatosDeModalidadeDeTeste.DoCatalogo);

        resultado.Regras.Should().BeEmpty();
        resultado.Avisos.Should().BeEmpty();
    }

    [Fact(DisplayName = "CA-01: devolve exatamente uma RegraAvaliada por ObrigatoriedadeLegal de entrada")]
    public void UmaRegraAvaliadaPorRegraDeEntrada()
    {
        ObrigatoriedadeLegal regra1 = NovaRegra("R1", new ConcorrenciaDuplaObrigatoria());
        ObrigatoriedadeLegal regra2 = NovaRegra("R2", new ConcorrenciaDuplaObrigatoria());

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            NovoProcesso(), TipoProcessoAvaliado, [regra1, regra2], IdentidadesDeCadastro.Vazio, FatosDeModalidadeDeTeste.DoCatalogo);

        resultado.Regras.Select(r => r.RegraCodigo).Should().BeEquivalentTo(["R1", "R2"]);
        resultado.Regras.Should().OnlyContain(r => r.TipoProcessoCodigoAvaliado == TipoProcessoAvaliado);
    }

    [Fact(DisplayName = "CA-08 (Customizado): sempre aprova, sempre emite aviso, nunca lança — inclusive com Parametros malformado")]
    public void Customizado_SempreAprovaEEmiteAviso_MesmoComParametrosMalformado()
    {
        using JsonDocument documento = JsonDocument.Parse("""{"qualquer": "coisa", "ate": [1,2,3]}""");
        ObrigatoriedadeLegal regra = NovaRegra("CUSTOM", new Customizado(documento.RootElement.Clone()));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            NovoProcesso(), TipoProcessoAvaliado, [regra], IdentidadesDeCadastro.Vazio, FatosDeModalidadeDeTeste.DoCatalogo);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
        resultado.Avisos.Should().ContainSingle();
    }
}
