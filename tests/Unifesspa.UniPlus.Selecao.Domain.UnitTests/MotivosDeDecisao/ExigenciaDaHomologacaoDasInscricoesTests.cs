namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

public sealed class ExigenciaDaHomologacaoDasInscricoesTests
{
    private const string Codigo = "SEM_RESULTADO_INEP";
    private const string Campo = "fases[0].regraDeHomologacao.alternativas[0].condicoes[0].motivoDesfavoravelCodigo";

    [Fact]
    public void Faltas_TipoNaoDeclarado_RecusaNaRegra()
    {
        ConfiguracaoDeclarada configuracao = Configuracao(Codigo, declaraTipo: false);
        FieldError falta = Faltas(configuracao).Should().ContainSingle().Which;
        falta.Field.Should().Be("fases[0].regraDeHomologacao");
        falta.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado);
    }

    [Fact]
    public void Faltas_CondicaoSemMotivo_RecusaNoCampo()
    {
        FieldError falta = Faltas(Configuracao((string?)null)).Should().ContainSingle().Which;
        falta.Field.Should().Be(Campo);
        falta.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
        falta.Error.Message.Should().Contain("não informa");
    }

    [Theory]
    [InlineData(TipoMotivoDeDecisao.HomologacaoDasInscricoes, SentidoDoMotivo.Favoravel, "favorável")]
    [InlineData(TipoMotivoDeDecisao.IsencaoDaTaxa, SentidoDoMotivo.Desfavoravel, "outro tipo")]
    public void Faltas_MotivoInadequado_Explica(TipoMotivoDeDecisao tipo, SentidoDoMotivo sentido, string mensagem)
    {
        ConfiguracaoDeclarada configuracao = Configuracao(Codigo);
        string detalhe = Faltas(configuracao, Escolha(configuracao, tipo, sentido)).Should().ContainSingle().Which.Error.Message;
        detalhe.Should().Contain(mensagem).And.NotContain(Codigo);
    }

    [Fact]
    public void Faltas_MotivoAusenteOuInativo_Recusa()
    {
        ConfiguracaoDeclarada configuracao = Configuracao(Codigo);
        MotivoDoProcesso escolha = Escolha(configuracao, TipoMotivoDeDecisao.HomologacaoDasInscricoes, SentidoDoMotivo.Desfavoravel);
        MotivosEscolhidos motivos = new([new MotivoEscolhidoComOrigem(escolha, null)], 1);
        LugaresDaDecisao lugares = LugaresDaDecisao.Da(configuracao);
        string detalhe = Faltas(configuracao).Should().ContainSingle().Which.Error.Message;
        detalhe.Should().Contain("não foi escolhido").And.NotContain(Codigo);
        motivos.Desativados(lugares).Should().ContainSingle();
        Faltas(configuracao, [.. motivos.QueCompoemAVersao(lugares)]).Should().ContainSingle();
    }

    [Fact]
    public void Faltas_MotivoDesfavoravelEscolhido_Aceita()
    {
        ConfiguracaoDeclarada configuracao = Configuracao(Codigo);
        MotivoDeDecisao origem = ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, Codigo, SentidoDoMotivo.Desfavoravel);
        LugarDaDecisao lugar = LugarDaDecisao.DaFase(configuracao.Cronograma.CronogramaFases[0].FaseCanonicaOrigemId);
        MotivoDoProcesso escolhido = MotivoDoProcesso.Copiar(Guid.NewGuid(), lugar, origem, 1, null);
        MotivosEscolhidos motivos = new([new MotivoEscolhidoComOrigem(escolhido, origem)], 1);
        Faltas(configuracao, escolhido).Should().BeEmpty();
        ItensDosMotivosDeDecisao.Avaliar(configuracao, motivos)[2].Ok.Should().BeTrue();
    }

    [Fact]
    public void Faltas_TiposDeDecisaoRecusados_SemJulgarEPainelPendente()
    {
        ConfiguracaoDeclarada configuracao = Configuracao(Codigo, declaraTipo: false, ComPartesRecusadas("fases[0].tiposDeDecisao"));
        Faltas(configuracao).Should().BeEmpty();
        ItensDosMotivosDeDecisao.Avaliar(configuracao, new MotivosEscolhidos([], 1))[2].Ok.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Faltas_RegraRecusadaInteira_SemJulgarEPainelPendente(bool declaraTipo)
    {
        ConfiguracaoDeclarada configuracao = Configuracao(Codigo, declaraTipo, ComPartesRecusadas("fases[0].regraDeHomologacao"));

        Faltas(configuracao).Should().BeEmpty();
        ItensDosMotivosDeDecisao.Avaliar(configuracao, new MotivosEscolhidos([], 1))[2].Ok.Should().BeFalse();
    }

    [Fact]
    public void Painel_RegraRecusadaComMotivoEscolhido_FicaPendente()
    {
        ConfiguracaoDeclarada configuracao = Configuracao(Codigo, declaraTipo: true, ComPartesRecusadas("fases[0].regraDeHomologacao"));
        MotivoDeDecisao origem = ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, Codigo, SentidoDoMotivo.Desfavoravel);
        LugarDaDecisao lugar = LugarDaDecisao.DaFase(configuracao.Cronograma.CronogramaFases[0].FaseCanonicaOrigemId);
        MotivosEscolhidos motivos = new([new MotivoEscolhidoComOrigem(MotivoDoProcesso.Copiar(Guid.NewGuid(), lugar, origem, 1, null), origem)], 1);
        ItensDosMotivosDeDecisao.Avaliar(configuracao, motivos)[2].Ok.Should().BeFalse();
    }

    [Fact]
    public void Faltas_RegraSemAlternativas_NaoExigeMotivo()
    {
        ConfiguracaoDeclarada configuracao = Configuracao(RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO", []));
        Faltas(configuracao).Should().BeEmpty();
        ItensDosMotivosDeDecisao.Avaliar(configuracao, new MotivosEscolhidos([], 1))[2].Ok.Should().BeTrue();
    }

    [Fact]
    public void Faltas_CondicoesEmVariasAlternativas_PreservaOrdem()
    {
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO", [Alternativa(Condicao(null), Condicao(null)), Alternativa(Condicao(null))]);
        ConfiguracaoDeclarada configuracao = Configuracao(regra);
        Faltas(configuracao).Select(static falta => falta.Field).Should().Equal(Campo,
            "fases[0].regraDeHomologacao.alternativas[0].condicoes[1].motivoDesfavoravelCodigo",
            "fases[0].regraDeHomologacao.alternativas[1].condicoes[0].motivoDesfavoravelCodigo");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    public void Faltas_FasesDeMesmaOrigemSoUmaDeclaraOTipo_AOutraRecebeTipoNaoDeclaradoESoAQueDeclaraEhJulgada(int ordemDaQueDeclara, int posicaoDaQueNaoDeclara)
    {
        ConfiguracaoDeclarada configuracao = ConfiguracaoDeFasesDeMesmaOrigem(ordemDaQueDeclara, OrigemNoDocumento.Identidade);

        FieldError[] faltas = Faltas(configuracao);

        int posicaoDaQueDeclara = 1 - posicaoDaQueNaoDeclara;
        faltas.Select(static falta => (falta.Field, falta.Error.Code)).Should().BeEquivalentTo(
        [
            ($"fases[{posicaoDaQueNaoDeclara}].regraDeHomologacao", MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado),
            ($"fases[{posicaoDaQueDeclara}].regraDeHomologacao.alternativas[0].condicoes[0].motivoDesfavoravelCodigo", MotivoDeDecisaoErrorCodes.CoberturaInsuficiente),
        ]);
    }

    [Fact]
    public void Faltas_FaseDeMesmaOrigemComTiposRecusados_NaoSuspendeOJulgamentoDaOutra()
    {
        ConfiguracaoDeclarada configuracao = ConfiguracaoDeFasesDeMesmaOrigem(ordemDaQueDeclara: null, ComPartesRecusadas("fases[0].tiposDeDecisao"));

        FieldError falta = Faltas(configuracao).Should().ContainSingle().Which;

        falta.Field.Should().Be("fases[1].regraDeHomologacao");
        falta.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado);
    }

    private static ConfiguracaoDeclarada ConfiguracaoDeFasesDeMesmaOrigem(int? ordemDaQueDeclara, OrigemNoDocumento origem)
    {
        Guid origemId = Guid.CreateVersion7();
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO", [Alternativa(Condicao(null))]);
        FaseCronograma[] fases = [.. new[] { 1, 2 }.Select(ordem => FaseDaOrigem(origemId, regra, ordem, declaraTipo: ordem == ordemDaQueDeclara))];

        return Lida(fases, [], origem);
    }

    private static FaseCronograma FaseDaOrigem(Guid origemId, RegraDeHomologacaoDasInscricoes regra, int ordem, bool declaraTipo)
    {
        ModeloDeFase modelo = (new ModeloDeFase("HOMOLOGACAO_INSCRICOES", ordem) { OrigemId = origemId }).Homologando(regra);

        return (declaraTipo ? modelo : modelo with { TiposDeDecisao = null }).Adotada();
    }

    private static CondicaoDeHomologacao Condicao(string? codigo) => CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, 2026, codigo, null).Value!;

    private static ConfiguracaoDeclarada Configuracao(string? codigo, bool declaraTipo = true, OrigemNoDocumento? origem = null) =>
        Configuracao(RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO", [Alternativa(Condicao(codigo))]), declaraTipo, origem);

    private static ConfiguracaoDeclarada Configuracao(RegraDeHomologacaoDasInscricoes regra, bool declaraTipo = true, OrigemNoDocumento? origem = null)
    {
        ModeloDeFase modelo = new ModeloDeFase("HOMOLOGACAO_INSCRICOES", 1).Homologando(regra);
        if (!declaraTipo)
        {
            modelo = modelo with { TiposDeDecisao = null };
        }

        return Lida([modelo.Adotada()], [], origem ?? OrigemNoDocumento.Identidade);
    }

    private static MotivoDoProcesso Escolha(ConfiguracaoDeclarada configuracao, TipoMotivoDeDecisao tipo, SentidoDoMotivo sentido)
    {
        LugarDaDecisao lugar = LugarDaDecisao.DaFase(configuracao.Cronograma.CronogramaFases[0].FaseCanonicaOrigemId);
        MotivoDeDecisao motivo = ProcessoParaMotivos.Motivo(tipo, Codigo, sentido, tipo == TipoMotivoDeDecisao.IsencaoDaTaxa ? FundamentoIsencaoCodigo.CadastroUnico : null);
        return MotivoDoProcesso.Copiar(Guid.NewGuid(), lugar, motivo, 1, null);
    }

    private static FieldError[] Faltas(ConfiguracaoDeclarada configuracao, params MotivoDoProcesso[] escolhidos)
    {
        ExigenciaDaHomologacaoDasInscricoes exigencia = new();
        LugaresDaDecisao lugares = LugaresDaDecisao.Da(configuracao);

        return [.. exigencia.TiposNaoDeclarados(configuracao, lugares), .. exigencia.FaltasDeCobertura(configuracao, lugares, escolhidos)];
    }
}
