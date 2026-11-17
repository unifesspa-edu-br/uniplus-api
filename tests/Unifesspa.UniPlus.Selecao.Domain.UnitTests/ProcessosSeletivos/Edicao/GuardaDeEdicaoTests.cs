namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class GuardaDeEdicaoTests
{
    [Fact(DisplayName = "Em rascunho a guarda libera a edição")]
    public void Rascunho_Libera() =>
        StatusProcesso.Rascunho.RecusaDeEdicaoForaDoRascunho().Should().BeNull();

    [Fact(DisplayName = "Publicado manda mudar a configuração pela retificação")]
    public void Publicado_Bloqueia() =>
        StatusProcesso.Publicado.RecusaDeEdicaoForaDoRascunho()!
            .Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");

    [Theory(DisplayName = "Status fora de rascunho e publicado não edita")]
    [InlineData(StatusProcesso.Nenhum)]
    [InlineData(StatusProcesso.Encerrado)]
    [InlineData(StatusProcesso.Cancelado)]
    public void StatusForaDaListaDePermissao_Bloqueia(StatusProcesso status) =>
        status.RecusaDeEdicaoForaDoRascunho()!
            .Code.Should().Be("ProcessoSeletivo.MutacaoForaDeEstadoEditavel");
}
