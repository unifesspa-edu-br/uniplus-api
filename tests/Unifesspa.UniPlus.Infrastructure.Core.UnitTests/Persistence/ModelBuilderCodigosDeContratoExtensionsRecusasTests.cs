namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

// Cada caso monta o próprio modelo, para que um defeito não esconda o outro: a primeira ocorrência interrompe a convenção.
public sealed class ModelBuilderCodigosDeContratoExtensionsRecusasTests
{
    [Fact(DisplayName = "`HasConversion<string>()` no escalar de enum com código é recusado, com o tipo e a propriedade")]
    public void Configurar_EscalarComConversaoParaTexto_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<Turma>().Property(turma => turma.Modalidade).HasConversion<string>())
            .Should().Contain(nameof(Turma)).And.Contain(nameof(Turma.Modalidade));

    [Fact(DisplayName = "`HasConversion<string>()` no escalar opcional de enum com código é recusado, com o tipo e a propriedade")]
    public void Configurar_EscalarOpcionalComConversaoParaTexto_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<Turma>().Property(turma => turma.Substituta).HasConversion<string>())
            .Should().Contain(nameof(Turma)).And.Contain(nameof(Turma.Substituta));

    [Fact(DisplayName = "Conversor diferente do código, por instância, é recusado, com o tipo, a propriedade e o conversor")]
    public void Configurar_EscalarComOutroConversorPorInstancia_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<Turma>().Property(turma => turma.Modalidade).HasConversion(new ConversorDoNomeEmCaixaAlta()))
            .Should().Contain(nameof(Turma)).And.Contain(nameof(Turma.Modalidade)).And.Contain(nameof(ConversorDoNomeEmCaixaAlta));

    [Fact(DisplayName = "Conversor diferente do código, por tipo, é recusado, com o tipo, a propriedade e o conversor")]
    public void Configurar_EscalarComOutroConversorPorTipo_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<Turma>().Property(turma => turma.Modalidade).HasConversion<ConversorDoNomeEmCaixaAlta>())
            .Should().Contain(nameof(Turma)).And.Contain(nameof(Turma.Modalidade)).And.Contain(nameof(ConversorDoNomeEmCaixaAlta));

    [Fact(DisplayName = "Conversor diferente do código no escalar opcional é recusado")]
    public void Configurar_EscalarOpcionalComOutroConversor_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<Turma>().Property(turma => turma.Substituta).HasConversion<ConversorDoNomeEmCaixaAlta>())
            .Should().Contain(nameof(Turma)).And.Contain(nameof(Turma.Substituta));

    [Fact(DisplayName = "`HaveConversion<string>()` nas convenções do modelo é recusado, com o tipo e a propriedade")]
    public void Configurar_ConversaoParaTextoDasConvencoesDoModelo_Lanca() =>
        MensagemDaRecusa(
                construtor => construtor.Entity<Turma>(),
                convencoes => convencoes.Properties<FormatoDeAula>().HaveConversion<string>())
            .Should().Contain(nameof(Turma)).And.Contain(nameof(Turma.Modalidade));

    [Fact(DisplayName = "`HaveConversion` de outro conversor nas convenções do modelo é recusado, com o tipo, a propriedade e o conversor")]
    public void Configurar_OutroConversorDasConvencoesDoModelo_Lanca() =>
        MensagemDaRecusa(
                construtor => construtor.Entity<Turma>(),
                convencoes => convencoes.Properties<FormatoDeAula>().HaveConversion<ConversorDoNomeEmCaixaAlta>())
            .Should().Contain(nameof(Turma)).And.Contain(nameof(Turma.Modalidade)).And.Contain(nameof(ConversorDoNomeEmCaixaAlta));

    [Fact(DisplayName = "`HasConversion<string>()` no elemento da coleção primitiva é recusado, com o tipo, a coleção e a palavra elemento")]
    public void Configurar_ElementoComConversaoParaTexto_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<TurmaComListas>().PrimitiveCollection(turma => turma.Preferidas)
                .ElementType(elemento => elemento.HasConversion<string>()))
            .Should().Contain(nameof(TurmaComListas)).And.Contain(nameof(TurmaComListas.Preferidas)).And.Contain("elemento");

    [Fact(DisplayName = "Conversor diferente do código no elemento da coleção primitiva é recusado, com a palavra elemento")]
    public void Configurar_ElementoComOutroConversor_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<TurmaComListas>().PrimitiveCollection(turma => turma.Preferidas)
                .ElementType(elemento => elemento.HasConversion(new ConversorDoNomeEmCaixaAlta())))
            .Should().Contain(nameof(TurmaComListas.Preferidas)).And.Contain("elemento").And.Contain(nameof(ConversorDoNomeEmCaixaAlta));

    [Fact(DisplayName = "Conversor posto na coleção inteira é recusado: a coleção de enum com código é coleção primitiva")]
    public void Configurar_ColecaoInteiraComConversorPorInstancia_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<TurmaComListas>().Property(turma => turma.Preferidas).HasConversion(new ConversorDaListaPelosNomes()))
            .Should().Contain(nameof(TurmaComListas)).And.Contain(nameof(TurmaComListas.Preferidas)).And.Contain("coleção primitiva");

    [Fact(DisplayName = "Conversão para texto posta na coleção inteira é recusada: a coleção de enum com código é coleção primitiva")]
    public void Configurar_ColecaoInteiraComConversaoParaTexto_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<TurmaComListas>().Property(turma => turma.Preferidas).HasConversion<string>())
            .Should().Contain(nameof(TurmaComListas)).And.Contain(nameof(TurmaComListas.Preferidas)).And.Contain("coleção primitiva");

    [Fact(DisplayName = "Conversor da coleção inteira posto pelas convenções do modelo é recusado: a coleção de enum com código é coleção primitiva")]
    public void Configurar_ColecaoInteiraComConversorDasConvencoesDoModelo_Lanca() =>
        MensagemDaRecusa(
                construtor => construtor.Entity<TurmaComListas>(),
                convencoes => convencoes.Properties<List<FormatoDeAula>>().HaveConversion<ConversorDaListaPelosNomes>())
            .Should().Contain(nameof(TurmaComListas)).And.Contain(nameof(TurmaComListas.Preferidas)).And.Contain("coleção primitiva");

    [Fact(DisplayName = "`HasConversion<string>()` dentro de tipo próprio (`owned`) é recusado, com o tipo e a propriedade")]
    public void Configurar_TipoProprioComConversaoParaTexto_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<TurmaComSala>().OwnsOne(turma => turma.Sala, sala => sala.Property(s => s.Uso).HasConversion<string>()))
            .Should().Contain(nameof(Sala)).And.Contain(nameof(Sala.Uso));

    [Fact(DisplayName = "`HasConversion<string>()` dentro de tipo complexo é recusado, com o tipo e a propriedade")]
    public void Configurar_TipoComplexoComConversaoParaTexto_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<TurmaComMaterial>()
                .ComplexProperty(turma => turma.Material, material => material.Property(m => m.Formato).HasConversion<string>()))
            .Should().Contain(nameof(Material)).And.Contain(nameof(Material.Formato));

    [Fact(DisplayName = "`HasConversion<string>()` dentro de tipo complexo aninhado é recusado, com o tipo e a propriedade")]
    public void Configurar_TipoComplexoAninhadoComConversaoParaTexto_Lanca() =>
        MensagemDaRecusa(construtor => construtor.Entity<TurmaComMaterial>().ComplexProperty(
                turma => turma.Material,
                material => material.ComplexProperty(m => m.Rodape, rodape => rodape.Property(r => r.Impressao).HasConversion<string>())))
            .Should().Contain(nameof(Rodape)).And.Contain(nameof(Rodape.Impressao));

    private static string MensagemDaRecusa(Action<ModelBuilder> configurar, Action<ModelConfigurationBuilder>? convencoes = null) =>
        FluentActions.Invoking(() => ModeloSintetico.Construir(configurar, convencoes))
            .Should().Throw<InvalidOperationException>().Which.Message;
}
