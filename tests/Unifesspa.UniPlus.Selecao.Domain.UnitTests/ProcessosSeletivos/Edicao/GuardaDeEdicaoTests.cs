namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class GuardaDeEdicaoTests
{
    [Fact(DisplayName = "Em rascunho a guarda libera a edição")]
    public void Rascunho_Libera() =>
        SituacaoDoProcessoSeletivo.Rascunho.RecusaDeEdicaoForaDoRascunho().Should().BeNull();

    [Fact(DisplayName = "Publicado manda mudar a configuração pela retificação")]
    public void Publicado_Bloqueia() =>
        SituacaoDoProcessoSeletivo.Publicado.RecusaDeEdicaoForaDoRascunho()!
            .Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");

    [Theory(DisplayName = "Situação fora de rascunho e publicado não edita")]
    [InlineData(SituacaoDoProcessoSeletivo.Encerrado, "Encerrado")]
    [InlineData(SituacaoDoProcessoSeletivo.Cancelado, "Cancelado")]
    public void SituacaoForaDaListaDePermissao_Bloqueia(SituacaoDoProcessoSeletivo situacao, string rotulo)
    {
        DomainError recusa = situacao.RecusaDeEdicaoForaDoRascunho()!;
        recusa.Code.Should().Be("ProcessoSeletivo.MutacaoForaDeEstadoEditavel");
        recusa.Message.Should().Contain($"Um processo em {rotulo}");
    }
}
