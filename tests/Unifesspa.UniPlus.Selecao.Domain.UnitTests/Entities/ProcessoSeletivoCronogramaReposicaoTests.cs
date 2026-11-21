namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioProcessoSeletivoCronograma;

public sealed class ProcessoSeletivoCronogramaReposicaoTests
{
    [Fact(DisplayName = "Cronograma vencedor substitui integralmente a coleção — Definir novamente troca tudo")]
    public void DefinirCronograma_SubstituiIntegralmente()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma primeira = new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Criar();
        processo.DefinirCronogramaFases([primeira], []).IsSuccess.Should().BeTrue();

        FaseCronograma segunda = new ModeloDeFase("HOMOLOGACAO_INSCRICOES", 1).Criar();
        Result resultado = processo.DefinirCronogramaFases([segunda], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle().Which.Codigo.Should().Be("HOMOLOGACAO_INSCRICOES");
    }
}
