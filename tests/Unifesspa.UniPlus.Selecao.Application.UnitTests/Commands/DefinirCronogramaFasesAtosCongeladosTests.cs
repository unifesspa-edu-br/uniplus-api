namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// A edição de um processo que já tem versão publicada: o tipo de ato que a versão publicada declarou vale
/// pela classificação que ela congelou, não pela do catálogo de hoje, e a versão que o codec não lê é recusada
/// com a falha do codec.
/// </summary>
public sealed class DefinirCronogramaFasesAtosCongeladosTests
{
    [Fact(DisplayName = "Tipo já declarado na versão publicada vale pela classificação congelada: o catálogo que passou a congelar não recusa a edição")]
    public async Task Handle_TipoJaDeclaradoQueOCatalogoPassouACongelar_AceitaPelaClassificacaoCongelada()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComVersaoPublicadaQueCongelou(ClassificacaoCongeladaComoComum())
            .ComTipoDeAto(TipoAtoDaClassificacao() with { CongelaConfiguracao = true });
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Tipo declarado pela primeira vez continua conferido pelo catálogo, mesmo com versão publicada")]
    public async Task Handle_TipoNovoEmProcessoPublicado_ConfereOCatalogo()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComVersaoPublicadaQueCongelou(new TipoDeAtoCongelado("OUTRO_ATO", UnicoPorObjeto: false, EhResultado: false, CongelaConfiguracao: false))
            .ComTipoDeAto(TipoAtoDaClassificacao() with { CongelaConfiguracao = true });
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado");
    }

    [Fact(DisplayName = "Versão publicada que o codec não lê é recusada com a falha do codec, sem cair no catálogo")]
    public async Task Handle_VersaoPublicadaIlegivel_RecusaComAFalhaDoCodec()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComVersaoPublicadaIlegivel(new DomainError("Envelope.CodecDesconhecido", "O codec não lê esta versão."))
            .ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("Envelope.CodecDesconhecido");
    }

    [Theory(DisplayName = "Com o tipo fora do catálogo hoje, a âncora nova é recusada e a que o agregado já tinha segue aceita")]
    [InlineData(false, "ProdutoDaFase.AtoNaoEncontradoNoCatalogo")]
    [InlineData(true, null)]
    public async Task Handle_AncoraEmTipoSemVersaoVigenteNoCatalogo_RecusaSoANovaEmRelacaoAoAgregado(bool ancoraJaNoAgregado, string? codigoEsperado)
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput semAncora, FaseCronogramaInput comAncora) = CenarioDaAncora();
        (await cenario.DefinirFasesAsync(ancoraJaNoAgregado ? comAncora : semAncora)).IsSuccess.Should().BeTrue();
        cenario.ComVersaoPublicadaQueCongelou(ClassificacaoCongeladaComoComum());

        // O catálogo deixa de ter versão vigente do tipo: a irreversibilidade dele passa a ser desconhecida.
        cenario.SemVersaoVigenteDoTipoDeAto(AtoDaClassificacao);
        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(comAncora);

        ConferirRecusaOuAceite(resultado, codigoEsperado);
    }

    // O grafo publicado vem de um processo à parte, porque a gravação seguinte reconcilia a fase no lugar.
    [Fact(DisplayName = "A âncora que só a versão publicada tem segue aceita quando o catálogo perde o tipo e o agregado não a tem mais")]
    public async Task Handle_AncoraSoNaVersaoPublicada_CatalogoSemOTipo_Aceita()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput semAncora, FaseCronogramaInput comAncora) = CenarioDaAncora();
        ProcessoSeletivo publicado = CenarioDoCronogramaDeFases.NovoProcessoEmRascunho();
        (await cenario.DefinirEmOutroProcessoAsync(publicado, comAncora)).IsSuccess.Should().BeTrue();
        (await cenario.DefinirFasesAsync(comAncora)).IsSuccess.Should().BeTrue();
        cenario.ComVersaoPublicadaDoGrafo(publicado, ClassificacaoCongeladaComoComum());
        (await cenario.DefinirFasesAsync(semAncora)).IsSuccess.Should().BeTrue();
        cenario.SemVersaoVigenteDoTipoDeAto(AtoDaClassificacao);

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(comAncora);

        resultado.IsSuccess.Should().BeTrue(MensagemDaRecusa(resultado));
    }

    // A versão publicada congelou o tipo, mas ainda não tinha a âncora: ela entra na sessão.
    [Fact(DisplayName = "Na sessão de retificação, a âncora declarada numa edição anterior segue aceita quando o catálogo perde o tipo")]
    public async Task Handle_AncoraDeclaradaNaSessao_CatalogoSemOTipo_Aceita()
    {
        (CenarioDoCronogramaDeFases cenario, _, FaseCronogramaInput comAncora) = CenarioDaAncora();
        cenario.ComVersaoPublicadaQueCongelou(ClassificacaoCongeladaComoComum());
        Result<MutacaoAceita> primeira = await cenario.DefinirFasesAsync(comAncora);
        primeira.IsSuccess.Should().BeTrue(primeira.Error?.Message);
        cenario.SemVersaoVigenteDoTipoDeAto(AtoDaClassificacao);

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(comAncora);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private static TipoDeAtoCongelado ClassificacaoCongeladaComoComum() =>
        new(AtoDaClassificacao, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false);

    private static (CenarioDoCronogramaDeFases Cenario, FaseCronogramaInput SemAncora, FaseCronogramaInput ComAncora) CenarioDaAncora()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComTipoDeAto(TipoAtoDaClassificacao())
            .ComRegraNoCatalogo(RegraDePrazoDeRecurso());
        FaseCronogramaInput semAncora = cenario.CadastrarFase(FaseCanonicaRecorrivel()) with { Produtos = ProdutosPreliminarEDefinitivo() };

        return (cenario, semAncora, semAncora with { RegraRecurso = RecursoAncoradoNaClassificacao() });
    }

    private static void ConferirRecusaOuAceite(Result<MutacaoAceita> resultado, string? codigoEsperado)
    {
        if (codigoEsperado is null)
        {
            resultado.IsSuccess.Should().BeTrue(MensagemDaRecusa(resultado));
            return;
        }

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(codigoEsperado);
    }

    private static string? MensagemDaRecusa(Result<MutacaoAceita> resultado) =>
        resultado.Errors.Count > 0 ? resultado.Errors[0].Error.Message : resultado.Error?.Message;
}
