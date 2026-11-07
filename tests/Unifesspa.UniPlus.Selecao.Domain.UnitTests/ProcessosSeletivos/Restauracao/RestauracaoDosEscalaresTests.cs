namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class RestauracaoDosEscalaresTests
{
    [Fact(DisplayName = "O grafo sem localidade não apaga a viva, e a convenção de contagem ausente é reposta")]
    public void Restaurar_GrafoSemLocalidade_MantemALocalidadeViva()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);
        LocalidadeRegente localidadeViva = processo.Localidade;

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, OrdemDasRecusasDoGrafoTests.GrafoComProblemas([]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Localidade.Should().BeSameAs(localidadeViva, "a localidade só é reposta quando o grafo a traz");
        processo.AlgoritmoContagemPrazo.Should().BeNull("a convenção ausente da versão congelada é reposta, não preservada");
    }
}
