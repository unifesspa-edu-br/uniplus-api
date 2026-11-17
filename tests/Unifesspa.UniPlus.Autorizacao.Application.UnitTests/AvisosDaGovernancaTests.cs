namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

public sealed class AvisosDaGovernancaTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";

    private static readonly Identidade Rebeca = new(Emissor, "rebeca.lima");
    private static readonly Identidade Pedro = new(Emissor, "pedro.vitor");

    private static ModeloDeAviso Modelo(TipoDeAviso tipo, int? versao = 2, params string[] permissoes) =>
        new(tipo, "Análise <de> atendimento", versao, permissoes, "https://uniplus.test/perfis/1?a=1&b=2");

    private static EmissorDeAvisos NovoEmissor(IRegistroDeAvisos registro) =>
        new(registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance);

    private static PedidoDeAviso Pedido(Guid fato, IReadOnlyList<DestinatarioDoAviso> destinatarios, IReadOnlyCollection<Identidade> excluidos) =>
        new(Modelo(TipoDeAviso.PermissoesRemovidas), fato, string.Empty, destinatarios, excluidos);

    [Fact(DisplayName = "O identificador do aviso é estável e muda com qualquer componente da chave")]
    public void IdDoAviso_EstavelEDistinto()
    {
        Guid fato = Guid.NewGuid();
        Guid base1 = IdDoAviso.De(new ChaveDoAviso(TipoDeAviso.PermissoesRemovidas, fato, string.Empty, Rebeca));

        IdDoAviso.De(new ChaveDoAviso(TipoDeAviso.PermissoesRemovidas, fato, string.Empty, Rebeca)).Should().Be(base1);
        IdDoAviso.De(new ChaveDoAviso(TipoDeAviso.AprovacoesDescartadas, fato, string.Empty, Rebeca)).Should().NotBe(base1);
        IdDoAviso.De(new ChaveDoAviso(TipoDeAviso.PermissoesRemovidas, fato, string.Empty, Pedro)).Should().NotBe(base1);
        IdDoAviso.De(new ChaveDoAviso(TipoDeAviso.PermissoesRemovidas, fato, "marco", Rebeca)).Should().NotBe(base1);
        IdDoAviso.De(new ChaveDoAviso(TipoDeAviso.PermissoesRemovidas, Guid.NewGuid(), string.Empty, Rebeca)).Should().NotBe(base1);
    }

    [Fact(DisplayName = "O identificador de uma chave conhecida tem sempre o mesmo valor")]
    public void IdDoAviso_ChaveConhecida_TemValorFixo()
    {
        ChaveDoAviso chave = new(TipoDeAviso.PermissoesRemovidas, Guid.Parse("0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b"), string.Empty, Rebeca);

        IdDoAviso.De(chave).Should().Be(Guid.Parse("c5b1005e-07f5-824a-a021-dd66c99adcfa"));
    }

    [Fact(DisplayName = "Os componentes da chave não se confundem quando o texto contém o separador")]
    public void IdDoAviso_ComponentesNaoSeConfundem()
    {
        Guid fato = Guid.NewGuid();

        Guid a = IdDoAviso.De(new ChaveDoAviso(TipoDeAviso.PermissoesRemovidas, fato, string.Empty, new Identidade("ab", "c")));
        Guid b = IdDoAviso.De(new ChaveDoAviso(TipoDeAviso.PermissoesRemovidas, fato, string.Empty, new Identidade("a", "bc")));

        a.Should().NotBe(b);
    }

    [Theory(DisplayName = "Cada tipo de aviso tem código, finalidade, assunto e abertura próprios")]
    [InlineData(TipoDeAviso.PermissoesRemovidas, "permissoes_removidas")]
    [InlineData(TipoDeAviso.VersaoCanceladaPelaRemocao, "versao_cancelada_pela_remocao")]
    [InlineData(TipoDeAviso.AprovacoesDescartadas, "aprovacoes_descartadas")]
    [InlineData(TipoDeAviso.PerfilSuspenso, "perfil_suspenso")]
    public void Tipo_TemCodigoEFinalidade(TipoDeAviso tipo, string codigo)
    {
        tipo.ToCodigo().Should().Be(codigo);
        tipo.Finalidade().Should().Be($"perfil.{codigo}");
        RedacaoDoAviso.Assunto(tipo).Should().NotBeNullOrWhiteSpace();
        RedacaoDoAviso.CorpoEmTexto(Modelo(tipo)).Should().Contain("Análise <de> atendimento").And.Contain("Versão: 2");
    }

    [Fact(DisplayName = "O tipo de aviso desconhecido é recusado em todo lugar que o interpreta")]
    public void Tipo_Desconhecido_Lanca()
    {
        const TipoDeAviso desconhecido = (TipoDeAviso)99;

        Action[] usos =
        [
            () => desconhecido.ToCodigo(),
            () => RedacaoDoAviso.Assunto(desconhecido),
            () => RedacaoDoAviso.CorpoEmTexto(Modelo(desconhecido)),
            () => RedacaoDoAviso.CorpoEmHtml(Modelo(desconhecido)),
        ];

        foreach (Action uso in usos)
        {
            uso.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [Fact(DisplayName = "O corpo em HTML escapa o nome e o link e omite o que não há")]
    public void CorpoEmHtml_EscapaEOmite()
    {
        string completo = RedacaoDoAviso.CorpoEmHtml(Modelo(TipoDeAviso.PermissoesRemovidas, 3, "Operar inscrições <b>"));
        string simples = RedacaoDoAviso.CorpoEmHtml(Modelo(TipoDeAviso.PermissoesRemovidas, null));

        completo.Should().Contain("&lt;de&gt;").And.Contain("a=1&amp;b=2").And.Contain("Versão: <b>3</b>")
            .And.Contain("&lt;b&gt;");
        simples.Should().NotContain("Versão:").And.NotContain("Permissões:");
        RedacaoDoAviso.CorpoEmTexto(Modelo(TipoDeAviso.PermissoesRemovidas, null)).Should().NotContain("Versão:");
    }

    [Fact(DisplayName = "O emissor não emite para o endereço inválido")]
    public async Task Emissor_EnderecoInvalido_NaoEmite()
    {
        IReadOnlyList<EnviarEmail> emitidos = await NovoEmissor(new RegistroDeAvisosNaMemoria())
            .EmitirAsync(Pedido(Guid.NewGuid(), [new DestinatarioDoAviso(Pedro, "endereco-invalido")], []), CancellationToken.None);

        emitidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O emissor não emite para quem praticou o ato nem repete a identidade")]
    public async Task Emissor_AplicaExclusoesEDeduplica()
    {
        DestinatarioDoAviso rebeca = new(Rebeca, "rebeca-lima12@uniplus-test.local");
        PedidoDeAviso pedido = Pedido(Guid.NewGuid(), [rebeca, rebeca, new DestinatarioDoAviso(Pedro, "pedro-vitor@uniplus-test.local")], [Pedro]);

        IReadOnlyList<EnviarEmail> emitidos = await NovoEmissor(new RegistroDeAvisosNaMemoria()).EmitirAsync(pedido, CancellationToken.None);

        emitidos.Should().ContainSingle();
    }

    [Fact(DisplayName = "O emissor não repete o aviso que o registro já tem")]
    public async Task Emissor_AvisoJaRegistrado_NaoRepete()
    {
        EmissorDeAvisos emissor = NovoEmissor(new RegistroDeAvisosNaMemoria());
        PedidoDeAviso pedido = Pedido(Guid.NewGuid(), [new DestinatarioDoAviso(Rebeca, "rebeca-lima12@uniplus-test.local")], []);

        await emissor.EmitirAsync(pedido, CancellationToken.None);
        IReadOnlyList<EnviarEmail> repetidos = await emissor.EmitirAsync(pedido, CancellationToken.None);

        repetidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O código da categoria da remoção vai e volta")]
    public void CategoriaDaRemocao_CodigoIdaEVolta()
    {
        foreach (CategoriaDaRemocao categoria in Enum.GetValues<CategoriaDaRemocao>())
        {
            CategoriaDaRemocaoCodigo.FromCodigo(categoria.ToCodigo()).Should().Be(categoria);
            CategoriaDaRemocaoCodigo.TentarLer(categoria.ToCodigo()).Should().Be(categoria);
        }
    }

    [Fact(DisplayName = "O código da etapa da versão vai e volta")]
    public void EtapaDaVersao_CodigoIdaEVolta()
    {
        foreach (EtapaDaVersao etapa in Enum.GetValues<EtapaDaVersao>())
        {
            EtapaDaVersaoCodigo.FromCodigo(etapa.ToCodigo()).Should().Be(etapa);
        }
    }

    [Fact(DisplayName = "O código da forma do cancelamento vai e volta")]
    public void FormaDoCancelamento_CodigoIdaEVolta()
    {
        foreach (FormaDoCancelamento forma in Enum.GetValues<FormaDoCancelamento>())
        {
            FormaDoCancelamentoCodigo.FromCodigo(forma.ToCodigo()).Should().Be(forma);
        }
    }

    [Fact(DisplayName = "O código da origem da versão vai e volta")]
    public void OrigemDaVersao_CodigoIdaEVolta()
    {
        foreach (OrigemDaVersao origem in Enum.GetValues<OrigemDaVersao>())
        {
            OrigemDaVersaoCodigo.FromCodigo(origem.ToCodigo()).Should().Be(origem);
        }
    }

    [Fact(DisplayName = "O código desconhecido das enumerações da versão é recusado")]
    public void Codigo_Desconhecido_Recusa()
    {
        CategoriaDaRemocaoCodigo.TentarLer("inventada").Should().BeNull();
        Action[] recusas =
        [
            () => CategoriaDaRemocaoCodigo.FromCodigo("x"),
            () => EtapaDaVersaoCodigo.FromCodigo("x"),
            () => FormaDoCancelamentoCodigo.FromCodigo("x"),
            () => OrigemDaVersaoCodigo.FromCodigo("x"),
        ];

        foreach (Action recusa in recusas)
        {
            recusa.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [Fact(DisplayName = "O valor fora das enumerações da versão não tem código")]
    public void Valor_ForaDaEnumeracao_Recusa()
    {
        Action[] recusas =
        [
            () => ((CategoriaDaRemocao)99).ToCodigo(),
            () => ((EtapaDaVersao)99).ToCodigo(),
            () => ((FormaDoCancelamento)99).ToCodigo(),
            () => ((OrigemDaVersao)99).ToCodigo(),
        ];

        foreach (Action recusa in recusas)
        {
            recusa.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
