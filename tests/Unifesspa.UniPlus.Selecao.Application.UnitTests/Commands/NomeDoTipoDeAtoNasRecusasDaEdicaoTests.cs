namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// As recusas da edição do cronograma citam o tipo de ato pelo nome, nunca pelo código: o tipo que a versão publicada já
/// guardou usa o nome da cópia, mesmo que o catálogo o tenha mudado, e o tipo declarado pela primeira vez usa o do catálogo.
/// </summary>
public sealed class NomeDoTipoDeAtoNasRecusasDaEdicaoTests
{
    private const string NomeDaCopia = "Nome guardado na publicação";

    [Fact(DisplayName = "O tipo novo que é resultado e fica sem papel é recusado com o nome do catálogo, sem o código")]
    public async Task Handle_ResultadoSemPapel_TipoNovo_CitaONomeDoCatalogo()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Produtos = [ProdutoDaClassificacao(null)] };

        DomainError erro = (await cenario.DefinirFasesAsync(fase)).Errors.Single().Error;

        erro.Message.Should().Be("O tipo de ato 'Resultado da classificação' é resultado no catálogo e precisa de papel: o resultado sem preliminar é declarado definitivo.");
        erro.Message.Should().NotContain(AtoDaClassificacao);
    }

    [Fact(DisplayName = "O tipo que a versão publicada já guardou é recusado com o nome da cópia, e não com o do catálogo")]
    public async Task Handle_TipoQueCongela_TipoJaPublicado_CitaONomeDaCopia()
    {
        TipoDeAtoCongelado copia = new(AtoDaClassificacao, NomeDaCopia, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: true);
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComVersaoPublicadaQueCongelou(copia)
            .ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        DomainError erro = (await cenario.DefinirFasesAsync(fase)).Errors.Single().Error;

        erro.Message.Should().Be($"O tipo de ato '{NomeDaCopia}' congela a configuração e não pode ser item de resultado: a correção de um resultado não cria versão nova.");
        erro.Message.Should().NotContain(AtoDaClassificacao).And.NotContain("Resultado da classificação");
    }

    [Fact(DisplayName = "O ato que não é resultado, declarado com papel, é recusado com o nome da cópia da versão publicada")]
    public async Task Handle_PapelEmAtoQueNaoEhResultado_TipoJaPublicado_CitaONomeDaCopia()
    {
        TipoDeAtoCongelado copia = new(AtoDaClassificacao, NomeDaCopia, UnicoPorObjeto: false, EhResultado: false, CongelaConfiguracao: false);
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComVersaoPublicadaQueCongelou(copia)
            .ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        DomainError erro = (await cenario.DefinirFasesAsync(fase)).Errors.Single().Error;

        erro.Message.Should().Be($"O tipo de ato '{NomeDaCopia}' não é resultado no catálogo — só resultado recebe papel preliminar ou definitivo.");
    }

    [Fact(DisplayName = "A âncora do recurso em tipo de efeito irreversível cita o nome do catálogo, sem o código, e fala do evento que inicia o prazo do recurso")]
    public async Task Handle_AncoraEmTipoIrreversivel_CitaONomeDoCatalogo()
    {
        DomainError erro = await RecusaDaAncoraIrreversivelAsync(copia: null);

        erro.Message.Should().Be("O tipo de ato 'Resultado da classificação' tem efeito irreversível: não cabe recurso contra ele, e sim contra o resultado que o fundamenta.");
        erro.Message.Should().NotContain(AtoDaClassificacao);
    }

    [Fact(DisplayName = "A âncora do recurso em tipo de efeito irreversível já publicado cita o nome da cópia, e não o do catálogo")]
    public async Task Handle_AncoraEmTipoIrreversivel_TipoJaPublicado_CitaONomeDaCopia()
    {
        TipoDeAtoCongelado copia = new(AtoDaClassificacao, NomeDaCopia, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false);

        DomainError erro = await RecusaDaAncoraIrreversivelAsync(copia);

        erro.Message.Should().Contain($"'{NomeDaCopia}'");
        erro.Message.Should().NotContain(AtoDaClassificacao).And.NotContain("Resultado da classificação");
    }

    private static async Task<DomainError> RecusaDaAncoraIrreversivelAsync(TipoDeAtoCongelado? copia)
    {
        CenarioDoCronogramaDeFases cenario = CenarioDeTipoIrreversivel(copia);
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaRecorrivel()) with
        {
            Produtos = ProdutosPreliminarEDefinitivo(),
            RegraRecurso = RecursoAncoradoNaClassificacao(),
        };

        Result resultado = await cenario.DefinirFasesAsync(fase);

        return resultado.Errors.Single(e => e.Error.Code == "RegraRecursoFase.AncoraEmAtoIrreversivel").Error;
    }

    private static CenarioDoCronogramaDeFases CenarioDeTipoIrreversivel(TipoDeAtoCongelado? copia)
    {
        CenarioDoCronogramaDeFases rascunho = CenarioDoCronogramaDeFases.EmRascunho();

        return (copia is null ? rascunho : rascunho.ComVersaoPublicadaQueCongelou(copia))
            .ComTipoDeAto(TipoAtoDaClassificacao() with { EfeitoIrreversivel = true })
            .ComRegraNoCatalogo(RegraDePrazoDeRecurso());
    }
}
