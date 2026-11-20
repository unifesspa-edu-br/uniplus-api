namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

public sealed class ModelBuilderCodigosDeContratoExtensionsTests
{
    private static readonly Lazy<IModel> ModeloCompleto = new(() => ModeloSintetico.Construir(new ModuloComEnumsDeContrato().AplicarAoModelo));

    [Fact(DisplayName = "O escalar de enum com código, sem conversor, recebe o conversor do código")]
    public void Configurar_Escalar_InstalaOConversorDoCodigo() =>
        Escalar(nameof(Turma.Modalidade)).GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();

    [Fact(DisplayName = "O escalar opcional de enum com código recebe o conversor do código")]
    public void Configurar_EscalarOpcional_InstalaOConversorDoCodigo() =>
        Escalar(nameof(Turma.Substituta)).GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();

    [Fact(DisplayName = "A propriedade de tipo próprio (`owned`) recebe o conversor do código")]
    public void Configurar_TipoProprio_InstalaOConversorDoCodigo() =>
        DoTipoProprio().GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();

    [Fact(DisplayName = "A propriedade de tipo complexo recebe o conversor do código")]
    public void Configurar_TipoComplexo_InstalaOConversorDoCodigo() =>
        DoTipoComplexo().GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();

    [Fact(DisplayName = "A propriedade de tipo complexo dentro de tipo complexo recebe o conversor do código")]
    public void Configurar_TipoComplexoAninhado_InstalaOConversorDoCodigo() =>
        DoTipoComplexoAninhado().GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();

    [Fact(DisplayName = "O elemento da coleção primitiva de enum com código recebe o conversor do código")]
    public void Configurar_ElementoDeColecaoPrimitiva_InstalaOConversorDoCodigo() =>
        Elemento(nameof(TurmaComListas.Preferidas)).GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();

    [Fact(DisplayName = "O elemento opcional da coleção primitiva de enum com código recebe o conversor do código")]
    public void Configurar_ElementoOpcionalDeColecaoPrimitiva_InstalaOConversorDoCodigo() =>
        Elemento(nameof(TurmaComListas.Eventuais)).GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();

    [Fact(DisplayName = "O elemento do vetor primitivo de enum com código recebe o conversor do código")]
    public void Configurar_ElementoDeVetorPrimitivo_InstalaOConversorDoCodigo() =>
        Elemento(nameof(TurmaComListas.Fixas)).GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();

    [Fact(DisplayName = "O escalar de outro enum com código recebe o conversor do próprio enum")]
    public void Configurar_EscalarDeOutroEnumComCodigo_InstalaOConversorDoProprioEnum() =>
        Escalar(nameof(Turma.Situacao)).GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<SituacaoDaTurma>>();

    [Fact(DisplayName = "O conversor do código configurado por instância no escalar é aceito e continua o mesmo")]
    public void Configurar_EscalarComOConversorDoCodigoPorInstancia_AceitaSemTrocar()
    {
        CodigoDeContratoValueConverter<FormatoDeAula> explicito = new();

        IModel modelo = ModeloSintetico.Construir(construtor => construtor.Entity<Turma>().Property(turma => turma.Modalidade).HasConversion(explicito));

        Propriedade<Turma>(modelo, nameof(Turma.Modalidade)).GetValueConverter().Should().BeSameAs(explicito);
    }

    [Fact(DisplayName = "O conversor do código configurado por tipo no escalar é aceito")]
    public void Configurar_EscalarComOConversorDoCodigoPorTipo_Aceita()
    {
        IModel modelo = ModeloSintetico.Construir(
            construtor => construtor.Entity<Turma>().Property(turma => turma.Modalidade).HasConversion<CodigoDeContratoValueConverter<FormatoDeAula>>());

        Propriedade<Turma>(modelo, nameof(Turma.Modalidade)).GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();
    }

    [Fact(DisplayName = "O conversor do código configurado por instância no elemento da coleção é aceito e continua o mesmo")]
    public void Configurar_ElementoComOConversorDoCodigoPorInstancia_AceitaSemTrocar()
    {
        CodigoDeContratoValueConverter<FormatoDeAula> explicito = new();

        IModel modelo = ModeloSintetico.Construir(
            construtor => construtor.Entity<TurmaComListas>().PrimitiveCollection(turma => turma.Preferidas).ElementType(elemento => elemento.HasConversion(explicito)));

        ElementoDe(modelo, nameof(TurmaComListas.Preferidas)).GetValueConverter().Should().BeSameAs(explicito);
    }

    [Fact(DisplayName = "O conversor do código configurado por tipo no elemento da coleção é aceito")]
    public void Configurar_ElementoComOConversorDoCodigoPorTipo_Aceita()
    {
        IModel modelo = ModeloSintetico.Construir(
            construtor => construtor.Entity<TurmaComListas>().PrimitiveCollection(turma => turma.Preferidas)
                .ElementType(elemento => elemento.HasConversion<CodigoDeContratoValueConverter<FormatoDeAula>>()));

        ElementoDe(modelo, nameof(TurmaComListas.Preferidas)).GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();
    }

    [Fact(DisplayName = "A coleção de objetos, mapeada como documento com conversor, não é tocada nem recusada")]
    public void Configurar_ColecaoDeObjetosComConversor_NaoToca()
    {
        ConversorDasAnotacoesEmJson conversor = new();

        IModel modelo = ModeloSintetico.Construir(
            construtor => construtor.Entity<TurmaComAnotacoes>().Property(turma => turma.Anotacoes).HasConversion(conversor));

        Propriedade<TurmaComAnotacoes>(modelo, nameof(TurmaComAnotacoes.Anotacoes)).GetValueConverter().Should().BeSameAs(conversor);
    }

    [Theory(DisplayName = "Enum sem código, texto, `Guid` e inteiro ficam sem conversor da convenção")]
    [InlineData(nameof(Turma.Periodo))]
    [InlineData(nameof(Turma.Titulo))]
    [InlineData(nameof(Turma.Responsavel))]
    [InlineData(nameof(Turma.Vagas))]
    public void Configurar_PropriedadeSemEnumComCodigo_NaoTocaNaPropriedade(string nome)
    {
        IProperty propriedade = Escalar(nome);

        propriedade.GetValueConverter().Should().BeNull();
        propriedade.GetProviderClrType().Should().BeNull();
    }

    [Fact(DisplayName = "A convenção não define tamanho nem tipo de coluna")]
    public void Configurar_NaoDefineTamanhoNemTipoDeColuna()
    {
        IProperty escalar = Escalar(nameof(Turma.Modalidade));
        IElementType elemento = Elemento(nameof(TurmaComListas.Preferidas));

        escalar.GetMaxLength().Should().BeNull();
        escalar.FindAnnotation(RelationalAnnotationNames.ColumnType).Should().BeNull();
        elemento.GetMaxLength().Should().BeNull();
        elemento.FindAnnotation(RelationalAnnotationNames.ColumnType).Should().BeNull();
    }

    [Fact(DisplayName = "Chamar a convenção de novo no mesmo `ModelBuilder` não falha nem troca o conversor")]
    public void Configurar_ChamadaRepetida_NaoFalhaNemTrocaOConversor()
    {
        ValueConverter? depoisDaPrimeiraChamada = null;

        IModel modelo = ModeloSintetico.Construir(construtor =>
        {
            new ModuloComEnumsDeContrato().AplicarAoModelo(construtor);
            construtor.ConfigurarConversaoDosEnumsDeContrato();
            depoisDaPrimeiraChamada = construtor.Model.FindEntityType(typeof(Turma))!.FindProperty(nameof(Turma.Modalidade))!.GetValueConverter();
        });

        depoisDaPrimeiraChamada.Should().NotBeNull();
        Propriedade<Turma>(modelo, nameof(Turma.Modalidade)).GetValueConverter().Should().BeSameAs(depoisDaPrimeiraChamada);
    }

    [Fact(DisplayName = "A composição do modelo do contexto único já aplica a convenção")]
    public void Compor_ModeloDoContextoUnico_TrazOConversorDoCodigo()
    {
        using ContextoDeTeste contexto = ContextoDeTeste.Criar(new ModuloComEnumsDeContrato());

        IProperty propriedade = Propriedade<Turma>(contexto.GetService<IDesignTimeModel>().Model, nameof(Turma.Modalidade));

        propriedade.GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<FormatoDeAula>>();
    }

    [Fact(DisplayName = "`ModelBuilder` nulo é recusado")]
    public void Configurar_ModelBuilderNulo_Lanca() =>
        FluentActions.Invoking(() => ((ModelBuilder)null!).ConfigurarConversaoDosEnumsDeContrato())
            .Should().Throw<ArgumentNullException>();

    private static IProperty Propriedade<TEntidade>(IModel modelo, string nome) =>
        modelo.FindEntityType(typeof(TEntidade))!.FindProperty(nome)!;

    private static IElementType ElementoDe(IModel modelo, string nome) =>
        Propriedade<TurmaComListas>(modelo, nome).GetElementType()!;

    private static IProperty Escalar(string nome) => Propriedade<Turma>(ModeloCompleto.Value, nome);

    private static IElementType Elemento(string nome) => ElementoDe(ModeloCompleto.Value, nome);

    private static IProperty DoTipoProprio() => Propriedade<Sala>(ModeloCompleto.Value, nameof(Sala.Uso));

    private static IProperty DoTipoComplexo() =>
        ComplexoDoMaterial().ComplexType.FindProperty(nameof(Material.Formato))!;

    private static IProperty DoTipoComplexoAninhado() =>
        ComplexoDoMaterial().ComplexType.FindComplexProperty(nameof(Material.Rodape))!.ComplexType.FindProperty(nameof(Rodape.Impressao))!;

    private static IComplexProperty ComplexoDoMaterial() =>
        ModeloCompleto.Value.FindEntityType(typeof(TurmaComMaterial))!.FindComplexProperty(nameof(TurmaComMaterial.Material))!;
}
