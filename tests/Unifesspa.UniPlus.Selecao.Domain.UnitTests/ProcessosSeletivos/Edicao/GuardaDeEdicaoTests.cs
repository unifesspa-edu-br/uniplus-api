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
        StatusProcesso.Rascunho.RecusaDeEdicaoSemSessao().Should().BeNull();

    [Fact(DisplayName = "Publicado sem sessão manda usar a retificação")]
    public void PublicadoSemSessao_Bloqueia() =>
        StatusProcesso.Publicado.RecusaDeEdicaoSemSessao()!
            .Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");

    [Theory(DisplayName = "Status fora de rascunho e publicado não edita, com ou sem sessão")]
    [InlineData(StatusProcesso.Nenhum)]
    [InlineData(StatusProcesso.Encerrado)]
    [InlineData(StatusProcesso.Cancelado)]
    public void StatusForaDaListaDePermissao_Bloqueia(StatusProcesso status) =>
        status.RecusaDeEdicaoSemSessao()!
            .Code.Should().Be("ProcessoSeletivo.MutacaoForaDeEstadoEditavel");

    [Fact(DisplayName = "Sob sessão a guarda exige a precondição da revisão corrente")]
    public void SobSessao_ExigeAPrecondicao()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.EmSessao();
        RascunhoRetificacao sessao = processo.Rascunho!;

        processo.MutacaoBloqueada(PrecondicaoIfMatch.Ausente).Should().NotBeNull();
        processo.MutacaoBloqueada(PrecondicaoIfMatch.DeTags([sessao.ETag])).Should().BeNull();
    }

    [Fact(DisplayName = "Encerrar sem sessão recusa antes de olhar a precondição")]
    public void EncerrarSemSessao_RecusaAntesDaPrecondicao() =>
        ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _).PendenciaDaSessao(PrecondicaoIfMatch.DeTags(["\"x:1\""]))!
            .Code.Should().Be("RascunhoRetificacao.NaoAberta");

    [Fact(DisplayName = "Encerrar com sessão aplica a mesma guarda de mutação")]
    public void EncerrarComSessao_AplicaAGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.EmSessao();
        RascunhoRetificacao sessao = processo.Rascunho!;

        processo.PendenciaDaSessao(PrecondicaoIfMatch.DeTags([sessao.ETag])).Should().BeNull();
        processo.PendenciaDaSessao(PrecondicaoIfMatch.DeTags(["\"x:1\""])).Should().NotBeNull();
    }
}
