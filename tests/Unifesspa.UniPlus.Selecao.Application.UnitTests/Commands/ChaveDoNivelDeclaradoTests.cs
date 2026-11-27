namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class ChaveDoNivelDeclaradoTests
{
    [Fact]
    public void NiveisDeclarados_ChaveInformada_EntregaChaveAoDominio()
    {
        EtapaProcessoInput entrada = EtapaInput("Prova", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1)
            with
        {
            EscalaDeAtendimento = [new NivelDeAtendimentoInput("Completo", 1m, "nivel_existente")],
        };
        NivelDeAtendimentoDeclarado nivel = EscritaDasEtapasDoProcesso.NiveisDeclarados(entrada)[0];
        nivel.Chave.Should().Be("nivel_existente");
    }
}
