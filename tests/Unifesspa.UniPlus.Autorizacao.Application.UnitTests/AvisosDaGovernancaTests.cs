namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
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

    [Fact(DisplayName = "O emissor não emite sem destinatário válido, sem quem praticou o ato e sem repetir identidade")]
    public async Task Emissor_AplicaExclusoesEDeduplica()
    {
        RegistroDeAvisosNaMemoria registro = new();
        EmissorDeAvisos emissor = new(registro, NullLogger<EmissorDeAvisos>.Instance);
        Guid fato = Guid.NewGuid();
        ModeloDeAviso modelo = Modelo(TipoDeAviso.PermissoesRemovidas);

        IReadOnlyList<EnviarEmail> vazio = await emissor.EmitirAsync(
            new PedidoDeAviso(modelo, fato, string.Empty, [new DestinatarioDoAviso(Pedro, "endereco-invalido")], []), CancellationToken.None);
        IReadOnlyList<EnviarEmail> emitidos = await emissor.EmitirAsync(
            new PedidoDeAviso(
                modelo,
                fato,
                string.Empty,
                [
                    new DestinatarioDoAviso(Rebeca, "rebeca-lima12@uniplus-test.local"),
                    new DestinatarioDoAviso(Rebeca, "rebeca-lima12@uniplus-test.local"),
                    new DestinatarioDoAviso(Pedro, "pedro-vitor@uniplus-test.local"),
                ],
                [Pedro]),
            CancellationToken.None);
        IReadOnlyList<EnviarEmail> repetidos = await emissor.EmitirAsync(
            new PedidoDeAviso(modelo, fato, string.Empty, [new DestinatarioDoAviso(Rebeca, "rebeca-lima12@uniplus-test.local")], []),
            CancellationToken.None);

        vazio.Should().BeEmpty();
        emitidos.Should().ContainSingle();
        repetidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os códigos das enumerações da versão ida e volta, e o desconhecido é recusado")]
    public void Codigos_IdaEVolta()
    {
        foreach (CategoriaDaRemocao c in Enum.GetValues<CategoriaDaRemocao>())
        {
            CategoriaDaRemocaoCodigo.FromCodigo(c.ToCodigo()).Should().Be(c);
            CategoriaDaRemocaoCodigo.TentarLer(c.ToCodigo()).Should().Be(c);
        }

        foreach (EtapaDaVersao e in Enum.GetValues<EtapaDaVersao>())
        {
            EtapaDaVersaoCodigo.FromCodigo(e.ToCodigo()).Should().Be(e);
        }

        foreach (FormaDoCancelamento f in Enum.GetValues<FormaDoCancelamento>())
        {
            FormaDoCancelamentoCodigo.FromCodigo(f.ToCodigo()).Should().Be(f);
        }

        foreach (OrigemDaVersao o in Enum.GetValues<OrigemDaVersao>())
        {
            OrigemDaVersaoCodigo.FromCodigo(o.ToCodigo()).Should().Be(o);
        }

        CategoriaDaRemocaoCodigo.TentarLer("inventada").Should().BeNull();
        Action[] recusas =
        [
            () => CategoriaDaRemocaoCodigo.FromCodigo("x"),
            () => ((CategoriaDaRemocao)99).ToCodigo(),
            () => EtapaDaVersaoCodigo.FromCodigo("x"),
            () => ((EtapaDaVersao)99).ToCodigo(),
            () => FormaDoCancelamentoCodigo.FromCodigo("x"),
            () => ((FormaDoCancelamento)99).ToCodigo(),
            () => OrigemDaVersaoCodigo.FromCodigo("x"),
            () => ((OrigemDaVersao)99).ToCodigo(),
        ];
        foreach (Action recusa in recusas)
        {
            recusa.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
