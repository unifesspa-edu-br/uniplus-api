namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

public sealed class EffectiveGrantPorTitularidadeTests
{
    [Fact(DisplayName = "A titularidade tem só a permissão: sem escopo, validade, concedente, identificador nem perfil")]
    public void PorTitularidade_DadosValidos_GuardaSoAPermissao()
    {
        EffectiveGrant grant = EffectiveGrant.PorTitularidade("autorizacao:pedidos:ler-proprios").Value!;

        (grant.PermissaoCodigo, grant.Fonte, grant.Escopo).Should()
            .Be(("autorizacao:pedidos:ler-proprios", FonteGrant.Titularidade, EscopoDaPermissaoEfetiva.Nenhum));
        (grant.ValidoAte, grant.ConcedidoPor).Should().Be(((DateTimeOffset?)null, (UsuarioRef?)null));
        (grant.GrantId, grant.PerfilId, grant.VersaoId).Should().Be(((Guid?)null, (Guid?)null, (Guid?)null));
    }

    [Theory(DisplayName = "A titularidade tem sempre o escopo nenhum, qualquer que seja a permissão")]
    [InlineData("autorizacao:pedidos:ler-proprios")]
    [InlineData("  selecao:inscricoes:consultar  ")]
    public void PorTitularidade_TemEscopoNenhum_EmQualquerEntrada(string permissao)
    {
        EscopoDaPermissaoEfetiva escopo = EffectiveGrant.PorTitularidade(permissao).Value!.Escopo;

        escopo.Should().Be(EscopoDaPermissaoEfetiva.Nenhum);
        (escopo.UnidadeId, escopo.ProcessoId, escopo.ChamadaId, escopo.Institucional)
            .Should().Be(((Guid?)null, (Guid?)null, (Guid?)null, false));
    }
}
