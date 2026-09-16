namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class RascunhoDePublicacaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private const string ConteudoValido = """{"ato":{"orgao":"REITORIA"}}""";

    private static RascunhoDePublicacao Novo(string conteudo = ConteudoValido, int versao = 1) =>
        RascunhoDePublicacao.Criar(Guid.CreateVersion7(), "sub-123", conteudo, versao, Agora, RascunhoDePublicacao.Prazo).Value!;

    [Fact]
    public void Criar_GuardaOConteudoSemInterpretar()
    {
        RascunhoDePublicacao rascunho = Novo();

        rascunho.Conteudo.Should().Be(ConteudoValido);
        rascunho.Versao.Should().Be(1);
        rascunho.ExpiraEm.Should().Be(Agora.Add(RascunhoDePublicacao.Prazo));
    }

    [Fact]
    public void Criar_AceitaDocumentoVazio()
    {
        // Rascunho de formulário pela metade — e no limite, sem nada preenchido — é o caso de
        // uso, não o desvio. Recusá-lo travaria o operador justamente quando ele apagou tudo
        // para recomeçar e quer que o servidor esqueça o que havia.
        Result<RascunhoDePublicacao> resultado = RascunhoDePublicacao.Criar(
            Guid.CreateVersion7(), "sub-123", "{}", 1, Agora, RascunhoDePublicacao.Prazo);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_RecusaVersaoNaoPositiva(int versao)
    {
        Result<RascunhoDePublicacao> resultado = RascunhoDePublicacao.Criar(
            Guid.CreateVersion7(), "sub-123", ConteudoValido, versao, Agora, RascunhoDePublicacao.Prazo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RascunhoDePublicacao.VersaoInvalida");
    }

    [Fact]
    public void Criar_AferioTetoEmBytesUtf8_NaoEmCaracteres()
    {
        // Cada 'ç' ocupa dois bytes. Uma string com metade do teto em caracteres acentuados
        // cabe folgada em `string.Length` e estoura a coluna — é exatamente o caso que medir
        // caracteres deixaria passar até o SaveChanges, virando 500 no meio da gravação.
        string acentuado = new('ç', RascunhoDePublicacao.ConteudoMaxBytes / 2);
        string conteudo = $"\"{acentuado}\"";

        conteudo.Length.Should().BeLessThan(RascunhoDePublicacao.ConteudoMaxBytes);
        Encoding.UTF8.GetByteCount(conteudo).Should().BeGreaterThan(RascunhoDePublicacao.ConteudoMaxBytes);

        Result<RascunhoDePublicacao> resultado = RascunhoDePublicacao.Criar(
            Guid.CreateVersion7(), "sub-123", conteudo, 1, Agora, RascunhoDePublicacao.Prazo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RascunhoDePublicacao.ConteudoMuitoGrande");
    }

    [Fact]
    public void Substituir_TrocaODocumentoInteiro_ERenovaOPrazo()
    {
        RascunhoDePublicacao rascunho = Novo();
        DateTimeOffset depois = Agora.AddDays(10);

        Result resultado = rascunho.Substituir("""{"numero":"07/2027"}""", 2, depois, RascunhoDePublicacao.Prazo);

        resultado.IsSuccess.Should().BeTrue();
        rascunho.Conteudo.Should().Be("""{"numero":"07/2027"}""");
        rascunho.Versao.Should().Be(2);
        rascunho.ExpiraEm.Should().Be(depois.Add(RascunhoDePublicacao.Prazo));
    }

    [Fact]
    public void Substituir_RecusadoNaoAlteraNada()
    {
        RascunhoDePublicacao rascunho = Novo();
        DateTimeOffset expiracaoOriginal = rascunho.ExpiraEm;

        Result resultado = rascunho.Substituir("""{"x":1}""", 0, Agora.AddDays(1), RascunhoDePublicacao.Prazo);

        resultado.IsFailure.Should().BeTrue();
        rascunho.Conteudo.Should().Be(ConteudoValido);
        rascunho.ExpiraEm.Should().Be(expiracaoOriginal);
    }

    [Fact]
    public void Expirou_SoDepoisDoPrazo()
    {
        RascunhoDePublicacao rascunho = Novo();

        rascunho.Expirou(rascunho.ExpiraEm.AddTicks(-1)).Should().BeFalse();
        rascunho.Expirou(rascunho.ExpiraEm).Should().BeTrue();
    }

    [Fact]
    public void Criar_ExigeDono()
    {
        Action semSub = () => RascunhoDePublicacao.Criar(
            Guid.CreateVersion7(), "  ", ConteudoValido, 1, Agora, RascunhoDePublicacao.Prazo);

        semSub.Should().Throw<ArgumentException>();
    }
}
