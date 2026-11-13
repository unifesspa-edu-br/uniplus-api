namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A forma da manifestação técnica, conferida antes de qualquer leitura, com as recusas num único lote (ADR-0125).</summary>
public sealed class PedidoDeManifestacaoTests
{
    [Fact(DisplayName = "A manifestação bem formada sai com o item, o ciclo lido e o conteúdo")]
    public void Ler_BemFormada()
    {
        Guid id = Guid.CreateVersion7();

        PedidoDeManifestacao pedido = PedidoDeManifestacao.Ler(new(new ItemInformado("FATO", id), 2, new ConteudoInformado("INADEQUADA", " O dado é sensível. "))).Value!;

        pedido.Item.Should().Be(new ItemDaProtecao(CadastroDoCatalogo.Fato, id));
        pedido.Ciclo.Should().Be(new CicloDaAprovacao(2));
        (pedido.Conteudo.Parecer, pedido.Conteudo.Texto!.Valor).Should().Be((ParecerDaManifestacao.Inadequada, "O dado é sensível."));
    }

    [Fact(DisplayName = "O cadastro, o ciclo, o parecer e o texto inválidos saem juntos, cada um no seu campo")]
    public void Ler_FormaInvalida_AcumulaRecusas()
    {
        Result<PedidoDeManifestacao> pedido = PedidoDeManifestacao.Ler(
            new(new ItemInformado("MODALIDADE", Guid.NewGuid()), null, new ConteudoInformado("TALVEZ", new string('a', 2001))));

        pedido.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("cadastro", ProtecaoDoCatalogoErrorCodes.CadastroInvalido),
            ("ciclo", ProtecaoDoCatalogoErrorCodes.CicloInvalido),
            ("parecer", ProtecaoDoCatalogoErrorCodes.ParecerInvalido),
            ("texto", ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoMuitoLongo));
    }

    [Theory(DisplayName = "Os pareceres desfavoráveis sem texto são recusados no campo texto; o adequado dispensa o texto")]
    [InlineData("RECOMENDA_AJUSTE", false)]
    [InlineData("INADEQUADA", false)]
    [InlineData("ADEQUADA", true)]
    public void Ler_TextoNoParecer(string parecer, bool aceito)
    {
        Result<PedidoDeManifestacao> pedido = PedidoDeManifestacao.Ler(new(new ItemInformado("TIPO_DOCUMENTO", Guid.NewGuid()), 1, new ConteudoInformado(parecer)));

        pedido.IsSuccess.Should().Be(aceito);
        pedido.Errors.Select(static e => e.Error.Code).Should().Equal(aceito ? [] : new[] { ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoObrigatorio });
    }
}
