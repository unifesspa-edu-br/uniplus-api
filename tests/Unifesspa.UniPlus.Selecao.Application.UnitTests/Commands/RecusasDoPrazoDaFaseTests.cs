namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

public sealed class RecusasDoPrazoDaFaseTests
{
    [Fact]
    public async Task Handle_PrazoESuspensividadeInvalidos_AcumulaRecusas()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComTipoDeAto(TipoAtoDaClassificacao()).ComRegraNoCatalogo(RegraDePrazoDeRecurso());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaRecorrivel()) with
        {
            Produtos = [ProdutoDaClassificacao(PapelProdutoFaseCodigo.Preliminar)],
            RegraRecurso = RecursoAncoradoNaClassificacao() with { PrazoValor = 0, SuspensividadePrimeiraInstanciaValor = 1 },
        };

        Result resultado = await cenario.DefinirFasesAsync(fase);

        resultado.Errors.Select(static erro => erro.Error.Code).Should().Equal(
            "RegraRecursoFase.PrazoNaoPositivo", "RegraRecursoFase.SuspensividadeIncompleta");
    }
}
