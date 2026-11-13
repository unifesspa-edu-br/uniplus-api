namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// O que a fase publica: o <see cref="DefinirCronogramaFasesCommandHandler"/> confere cada produto contra o
/// catálogo de tipos de ato (o papel só cabe em ato que o catálogo classifica como resultado) e acumula as
/// recusas, cada uma no campo do produto que a causou (ADR-0125).
/// </summary>
public sealed class DefinirCronogramaFasesProdutosDaFaseTests
{
    [Fact(DisplayName = "CA-02: produto cujo tipo de ato não tem versão vigente no catálogo é recusado com ProdutoDaFase.AtoNaoEncontradoNoCatalogo")]
    public async Task Handle_ProdutoSemVersaoVigenteNoCatalogo_Recusa()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().SemVersaoVigenteDoTipoDeAto(AtoDaClassificacao);
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Field == "fases[0].produtos[0].atoCodigo"
                && e.Error.Code == "ProdutoDaFase.AtoNaoEncontradoNoCatalogo");
    }

    [Fact(DisplayName = "Tipo classificado como resultado declarado sem papel pela fase é recusado com ProcessoSeletivo.ResultadoDeclaradoSemPapel")]
    public async Task Handle_ResultadoSemPapel_Recusa()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Produtos = [ProdutoDaClassificacao(null)] };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "fases[0].produtos[0].papel"
            && e.Error.Code == "ProcessoSeletivo.ResultadoDeclaradoSemPapel");
    }

    // A 2ª fase declara papel num ato que o catálogo não classifica como resultado: o field precisa levar o
    // índice da fase E o do produto dentro dela.
    [Fact(DisplayName = "ADR-0125: recusa de produto na SEGUNDA fase do payload carrega o índice da fase e o do item no field")]
    public async Task Handle_SegundaFaseComPapelEmAtoQueNaoEhResultado_PrefixaIndiceNoField()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComTipoDeAto(TipoAtoDaClassificacao()).ComTipoDeAto(TipoAtoDoComunicado());
        FaseCronogramaInput primeira = cenario.CadastrarFase(FaseCanonicaRecorrivel());
        FaseCronogramaInput cadastrada = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());
        FaseCronogramaInput segunda = cadastrada with { Ordem = 2, Produtos = [ProdutoDoComunicado(PapelProdutoFaseCodigo.Definitivo)] };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(primeira, segunda);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(["fases[1].produtos[0].papel"]);
        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProdutoDaFase.PapelEmAtoQueNaoEhResultado");
    }

    // A fase canônica de resultado com a janela NO_EDITAL sem início nem fim dispara JanelaObrigatoriaEmDataNoEdital
    // (sem field próprio, porque afeta os dois lados da janela); a promessa de parecer individual sem produto
    // nenhum dispara ParecerIndividualSemResultado (field "emiteParecerIndividual") na MESMA fase. Os dois
    // precisam ficar rastreáveis à fase de índice 0, inclusive o que não tem campo específico.
    [Fact(DisplayName = "ADR-0125: erro sem field próprio (JanelaObrigatoriaEmDataNoEdital) é prefixado só com fases[i], nunca fica com field null")]
    public async Task Handle_ErroSemFieldProprio_PrefixaComIndiceDaFaseSemDeixarFieldNulo()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();
        FaseCronogramaInput cadastrada = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());
        FaseCronogramaInput fase = cadastrada with { Inicio = null, Fim = null, Produtos = [], EmiteParecerIndividual = true };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(["fases[0]", "fases[0].emiteParecerIndividual"]);
        resultado.Errors.Should().NotContain(e => e.Field == null);
    }

    [Fact(DisplayName = "CA-02 (contraprova): produto SEM papel num ato que não é resultado é aceito")]
    public async Task Handle_ProdutoSemPapelEmAtoQueNaoEhResultado_Aceita()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDoComunicado());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Produtos = [ProdutoDoComunicado(null)] };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Should().ContainSingle()
            .Which.ProduzResultado.Should().BeFalse("publicar aviso não torna a fase produtora de resultado");
    }

    [Fact(DisplayName = "CA-02: papel fora do vocabulário é recusado com ProdutoDaFase.PapelDesconhecido, sem virar produto sem papel")]
    public async Task Handle_PapelForaDoVocabulario_Recusa()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Produtos = [ProdutoDaClassificacao("PRELIMINARY")] };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsFailure.Should().BeTrue(
            "aceitar o token desconhecido como ausência de papel transformaria erro de digitação em fase que deixa de produzir resultado, em silêncio");
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Field == "fases[0].produtos[0].papel"
                && e.Error.Code == "ProdutoDaFase.PapelDesconhecido");
    }

    [Fact(DisplayName = "ADR-0125: dois produtos com problemas distintos acumulam as duas recusas, cada uma com o índice do item")]
    public async Task Handle_DoisProdutosComProblemasDistintos_AcumulaAsDuasRecusas()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComTipoDeAto(TipoAtoDoComunicado()).SemVersaoVigenteDoTipoDeAto("ATO_INEXISTENTE");
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with
        {
            Produtos = [ProdutoDoComunicado(PapelProdutoFaseCodigo.Preliminar), new ProdutoDaFaseInput("ATO_INEXISTENTE", null)],
        };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["fases[0].produtos[0].papel", "fases[0].produtos[1].atoCodigo"],
            "uma coleção mal declarada erra em vários itens ao mesmo tempo, e devolver só o primeiro faria o operador descobrir os demais numa sequência de tentativas");
    }

    [Fact(DisplayName = "O catálogo é lido UMA vez por código, mesmo quando duas fases publicam o mesmo ato")]
    public async Task Handle_MesmoAtoEmDuasFases_ResolveOCatalogoUmaVez()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput primeira = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());
        FaseCronogramaInput segunda = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Ordem = 2 };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(primeira, segunda);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await cenario.TipoAtoPublicadoReader.Received(1)
            .ObterVigenteAsync(AtoDaClassificacao, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
}
