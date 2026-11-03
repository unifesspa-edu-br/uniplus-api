namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class GuardaDeEdicaoTests
{
    [Fact(DisplayName = "Em rascunho a guarda libera a edição sem exigir precondição")]
    public void Rascunho_LiberaSemPrecondicao() =>
        GuardaDeEdicao.MutacaoBloqueada(StatusProcesso.Rascunho, sessao: null, PrecondicaoIfMatch.Ausente).Should().BeNull();

    [Fact(DisplayName = "Publicado sem sessão manda usar a retificação")]
    public void PublicadoSemSessao_Bloqueia() =>
        GuardaDeEdicao.MutacaoBloqueada(StatusProcesso.Publicado, sessao: null, PrecondicaoIfMatch.Ausente)!
            .Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");

    [Theory(DisplayName = "Status fora de rascunho e publicado não edita, com ou sem sessão")]
    [InlineData(StatusProcesso.Nenhum)]
    [InlineData(StatusProcesso.Encerrado)]
    [InlineData(StatusProcesso.Cancelado)]
    public void StatusForaDaListaDePermissao_Bloqueia(StatusProcesso status) =>
        GuardaDeEdicao.MutacaoBloqueada(status, sessao: null, PrecondicaoIfMatch.Ausente)!
            .Code.Should().Be("ProcessoSeletivo.MutacaoForaDeEstadoEditavel");

    [Fact(DisplayName = "Sob sessão a guarda exige a precondição da revisão corrente")]
    public void SobSessao_ExigeAPrecondicao()
    {
        RascunhoRetificacao sessao = NovaSessao();

        GuardaDeEdicao.MutacaoBloqueada(StatusProcesso.Publicado, sessao, PrecondicaoIfMatch.Ausente).Should().NotBeNull();
        GuardaDeEdicao.MutacaoBloqueada(StatusProcesso.Publicado, sessao, PrecondicaoIfMatch.DeTags([sessao.ETag])).Should().BeNull();
    }

    [Fact(DisplayName = "Encerrar sem sessão recusa antes de olhar a precondição")]
    public void EncerrarSemSessao_RecusaAntesDaPrecondicao() =>
        GuardaDeEdicao.PendenciaDaSessao(StatusProcesso.Publicado, sessao: null, PrecondicaoIfMatch.DeTags(["\"x:1\""]))!
            .Code.Should().Be("RascunhoRetificacao.NaoAberta");

    [Fact(DisplayName = "Encerrar com sessão aplica a mesma guarda de mutação")]
    public void EncerrarComSessao_AplicaAGuarda()
    {
        RascunhoRetificacao sessao = NovaSessao();

        GuardaDeEdicao.PendenciaDaSessao(StatusProcesso.Publicado, sessao, PrecondicaoIfMatch.DeTags([sessao.ETag])).Should().BeNull();
        GuardaDeEdicao.PendenciaDaSessao(StatusProcesso.Publicado, sessao, PrecondicaoIfMatch.DeTags(["\"x:1\""])).Should().NotBeNull();
    }

    private static RascunhoRetificacao NovaSessao() => ProcessoEmEdicaoDeTeste.EmSessao().Rascunho!;
}
