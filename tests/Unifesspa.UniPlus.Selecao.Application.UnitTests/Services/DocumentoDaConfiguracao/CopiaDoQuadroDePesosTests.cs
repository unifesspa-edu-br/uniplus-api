namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A cópia do quadro de pesos leva os grupos da resolução e os rótulos dos que faltam, e devolve a recusa do domínio a uma linha que ele não aceita.</summary>
public sealed class CopiaDoQuadroDePesosTests
{
    [Fact(DisplayName = "A resolução completa vira o quadro completo, com um grupo congelado por linha e nenhum grupo ausente")]
    public void ResolucaoCompleta_ViraQuadroCompleto()
    {
        Result<QuadroDePesosCitado> copia = CopiaDoQuadroDePesos.De(CenarioDaClassificacao.ResolucaoCom(CenarioDaClassificacao.GruposDoAnexoI));

        copia.IsSuccess.Should().BeTrue();
        copia.Value!.Grupos.Select(static grupo => grupo.GrupoAreaEnem.Codigo).Should().Equal(CenarioDaClassificacao.GruposDoAnexoI.Select(static grupo => grupo.Codigo));
        copia.Value.GruposAusentes.Should().BeEmpty();
        copia.Value.Completo.Should().BeTrue();
    }

    [Fact(DisplayName = "A resolução a que faltam grupos vira o quadro incompleto, com o rótulo de cada grupo ausente")]
    public void ResolucaoIncompleta_ViraQuadroIncompleto()
    {
        Result<QuadroDePesosCitado> copia = CopiaDoQuadroDePesos.De(
            CenarioDaClassificacao.ResolucaoCom(CenarioDaClassificacao.GruposDoAnexoI.Take(3), ("TECNOLOGICA", "Tecnológica")));

        copia.Value!.GruposAusentes.Should().Equal("Tecnológica");
        copia.Value.Completo.Should().BeFalse();
    }

    [Fact(DisplayName = "A linha que o domínio recusa é a recusa da cópia")]
    public void LinhaQueODominioRecusa_EARecusaDaCopia()
    {
        ResolucaoPesoAreaEnemView cadastrada = CenarioDaClassificacao.ResolucaoCom([("", "Grupo sem código")]);

        Result<QuadroDePesosCitado> copia = CopiaDoQuadroDePesos.De(cadastrada);

        copia.IsFailure.Should().BeTrue();
    }
}
