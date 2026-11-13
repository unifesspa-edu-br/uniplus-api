namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// A resolução das dependências cross-módulo do <see cref="DefinirCronogramaFasesCommandHandler"/> — fase canônica,
/// tipo de banca e categoria de documento, do cadastro de Configuração; regra, do catálogo de regras — e os
/// erros nomeados que cada resolução malsucedida produz.
/// </summary>
public sealed class DefinirCronogramaFasesResolucaoDosCadastrosTests
{
    [Fact(DisplayName = "Handle com lista de fases vazia devolve a causa de domínio, não a recusa de forma")]
    public async Task Handle_FasesVazias_DevolveCausaDeDominio()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CronogramaFasesVazio",
            "a regra de forma no validator tornava esta causa inalcançável por um cliente HTTP");
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().SemOProcessoNoRepositorio();

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com FaseCanonicaId que não resolve no cadastro retorna FaseCronograma.FaseCanonicaNaoEncontrada")]
    public async Task Handle_FaseCanonicaNaoEncontrada_Recusa()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();
        cenario.FaseCanonicaReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((FaseCanonicaView?)null);

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(EntradaDaFase(Guid.CreateVersion7()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.FaseCanonicaNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com fase conforme resolve e persiste — devolve o ETag da sessão (ou null em rascunho)")]
    public async Task Handle_FaseConforme_PersisteERetornaSucesso()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        cenario.Processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Should().ContainSingle().Which.Codigo.Should().Be("RESULTADO_FINAL");
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com categoria de documento fora do cadastro recusa nomeando o recorte da banca")]
    public async Task Handle_CategoriaDoRecorteForaDoCadastro_RecusaComErroNomeado()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput fase = FaseComBancaCujaCategoriaSaiuDoCadastro(cenario);

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Error.Code == "FaseCronograma.CategoriaDocumentoNaoEncontrada"
                && e.Field == "fases[0].bancasRequeridas[0].categoriasDocumentoIds");
        cenario.Processo.CronogramaFases.Should().BeEmpty("uma categoria fora do cadastro não pode entrar no edital congelado");
    }

    [Fact(DisplayName = "A parada numa resolução cross-módulo LEVA JUNTO as recusas de produto já acumuladas")]
    public async Task Handle_ParadaCrossModulo_PreservaRecusasAcumuladas()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDoComunicado());
        FaseCronogramaInput[] fases = FasesComPapelIndevidoETipoDeBancaMorto(cenario);

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fases);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
            ["ProdutoDaFase.PapelEmAtoQueNaoEhResultado", "FaseCronograma.TipoBancaNaoEncontrado"]);
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["fases[0].produtos[0].papel", "fases[1].bancasRequeridas[0].tipoBancaId"],
            "parar na primeira resolução cross-módulo não apaga defeito já diagnosticado, e nenhum erro sai sem campo, que a ADR-0023 não admite");
    }

    [Fact(DisplayName = "A interrupção cross-módulo carrega o campo que a localiza, mesmo sem recusa acumulada junto")]
    public async Task Handle_InterrupcaoCrossModulo_CarregaOCampoQueALocaliza()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        cenario.RegraCatalogoReader.ObterAsync("REGRA_INEXISTENTE", "v1", Arg.Any<CancellationToken>()).Returns((RegraCatalogo?)null);
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with
        {
            RegraRecurso = RecursoAncoradoNaClassificacao() with { RegraCodigo = "REGRA_INEXISTENTE" },
        };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("fases[0].regraRecurso.regraCodigo");
    }

    private static FaseCronogramaInput FaseComBancaCujaCategoriaSaiuDoCadastro(CenarioDoCronogramaDeFases cenario)
    {
        Guid tipoBancaId = Guid.CreateVersion7();
        Guid categoriaViva = Guid.CreateVersion7();
        Guid categoriaRemovida = Guid.CreateVersion7();
        cenario.TipoBancaReader.ObterPorIdAsync(tipoBancaId, Arg.Any<CancellationToken>())
            .Returns(new TipoBancaView(tipoBancaId, "BANCA_ANALISE_DOCUMENTAL", "Banca de análise documental", null, null));
        cenario.CategoriaDocumentoReader.ObterPorIdAsync(categoriaViva, Arg.Any<CancellationToken>())
            .Returns(new CategoriaDocumentoView(categoriaViva, "RENDA", "Renda", null, 0));
        cenario.CategoriaDocumentoReader.ObterPorIdAsync(categoriaRemovida, Arg.Any<CancellationToken>())
            .Returns((CategoriaDocumentoView?)null);

        return cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with
        {
            BancasRequeridas = [new BancaRequeridaInput(tipoBancaId, [categoriaViva, categoriaRemovida])],
        };
    }

    // A 1ª fase erra o papel de um produto; a 2ª referencia um tipo de banca morto: a segunda interrompe a
    // passada, e a primeira já tinha diagnóstico produzido.
    private static FaseCronogramaInput[] FasesComPapelIndevidoETipoDeBancaMorto(CenarioDoCronogramaDeFases cenario)
    {
        cenario.TipoBancaReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TipoBancaView?)null);
        FaseCronogramaInput primeira = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with
        {
            Produtos = [ProdutoDoComunicado(PapelProdutoFaseCodigo.Definitivo)],
        };
        FaseCronogramaInput segunda = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with
        {
            Ordem = 2,
            BancasRequeridas = [new BancaRequeridaInput(Guid.CreateVersion7(), [])],
        };

        return [primeira, segunda];
    }
}
