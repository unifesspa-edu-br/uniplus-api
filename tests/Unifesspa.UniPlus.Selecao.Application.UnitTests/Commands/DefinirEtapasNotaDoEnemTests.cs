namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasNotaDoEnemTests
{
    [Fact(DisplayName = "Etapa de nota do ENEM com recurso em ato é recusada pela regra do ENEM, antes da conferência da âncora")]
    public async Task Handle_NotaEnemComRecursoEmAto_RecusaPelaRegraDoEnem()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ITipoEtapaReader reader = ReaderDoTipo(TipoNoCadastro(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM", notaDeOrigemNoEnem: true));
        EtapaProcessoInput etapa = EtapaInput("Nota do ENEM", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1) with { Recursos = [RecursoEmAtoPublicado()] };
        DefinirEtapasCommand command = new(processo.Id, [etapa]);

        Result result = await Executar(command, RepositorioCom(processo), unitOfWork, reader, regraCatalogoReader: CatalogoComARegraDeRecursoEmAto());

        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto");
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Etapa nova congela a nota de origem no ENEM lida do cadastro")]
    public async Task Handle_EtapaNova_CongelaANotaDeOrigemNoEnem()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        ITipoEtapaReader reader = ReaderDoTipo(TipoNoCadastro(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM", notaDeOrigemNoEnem: true));
        reader.ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>()).Returns(TipoNoCadastro(TipoRedacaoOrigemId, "REDACAO", "Redação"));

        Result result = await ExecutarSobre(
            processo, reader,
            EtapaInput("Nota do ENEM", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1),
            EtapaInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 2));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single(e => e.TipoEtapaOrigemId == TipoProvaObjetivaOrigemId).DeclaraNotaDoEnem.Should().BeTrue();
        processo.Etapas.Single(e => e.TipoEtapaOrigemId == TipoRedacaoOrigemId).DeclaraNotaDoEnem.Should().BeFalse();
    }

    [Fact(DisplayName = "Mudar o caráter regrava os sinalizadores e mantém a nota de origem no ENEM congelada")]
    public async Task Handle_CaraterAlterado_MantemANotaDeOrigemNoEnemCongelada()
    {
        TipoEtapaSnapshot doEnem = TipoCongelado(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true);
        EtapaProcesso etapa = EtapaDoProcesso("Nota do ENEM", CaraterEtapa.Classificatoria, 1, doEnem, 1m);
        ProcessoSeletivo processo = ProcessoComAsEtapas(etapa);
        ITipoEtapaReader reader = ReaderDoTipo(TipoNoCadastro(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM"));

        Result result = await ExecutarSobre(
            processo, reader, EtapaInput("Nota do ENEM", CaraterEtapa.Ambas, TipoProvaObjetivaOrigemId, 1m, 5m, 1, etapa.Id));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.NotaDeOrigemNoEnem.Should().BeTrue(
            "a regravação dos sinalizadores não alcança a identidade congelada");
        await reader.Received().ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>());
    }

    private static RecursoDaEtapaInput RecursoEmAtoPublicado() => new(
        AncoraDoRecurso.AtoPublicado, "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1",
        2m, "diasUteis", "RESULTADO_CLASSIFICACAO", false, null, null, "semEfeitoSuspensivo", null);

    private static IRegraCatalogoReader CatalogoComARegraDeRecursoEmAto()
    {
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", TipoRegra.RegraPrazoRecurso,
                JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999, art. 59"));
        return regraCatalogoReader;
    }
}
