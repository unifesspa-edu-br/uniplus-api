namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

public sealed class NomeDaCategoriaNaBancaDaFaseTests
{
    [Fact]
    public async Task MontarAsync_CategoriaDoCadastro_CopiaONome()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();
        Guid tipoId = Guid.NewGuid();
        Guid categoriaId = Guid.NewGuid();
        cenario.TipoBancaReader.ObterPorIdAsync(tipoId, Arg.Any<CancellationToken>()).Returns(Tipo(tipoId));
        cenario.CategoriaDocumentoReader.ObterPorIdAsync(categoriaId, Arg.Any<CancellationToken>())
            .Returns(new CategoriaDocumentoView(categoriaId, "RENDA", "Renda", null, 0));
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { BancasRequeridas = [new(tipoId, [categoriaId])] };
        CadastrosDasFases cadastros = new(cenario.FaseCanonicaReader, cenario.UnidadeResponsavel, cenario.TipoBancaReader,
            cenario.CategoriaDocumentoReader, cenario.RegraCatalogoReader, cenario.FatoCandidatoReader);

        Result<List<BancaRequerida>> resultado = await new MontagemDasBancasDaFase(cadastros, new()).MontarAsync(fase, 0, [], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().CandidatosAtendidos.Should().ContainSingle().Which.Nome.Should().Be("Renda");
    }

    private static TipoBancaView Tipo(Guid id) => new(id, TipoDeBanca.AnaliseDocumental, "Banca", null, null,
        PapelDaBanca.AvaliaOMerito, false, false, []);
}
