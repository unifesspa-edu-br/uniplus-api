namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class CopiaDaOfertaDeCursoOwnedConfigurationTests
{
    private static readonly IModel Modelo = ModeloSintetico.Construir(Configurar);
    private static IEntityType Copia => Modelo.FindEntityType(typeof(CopiaDaOfertaDeCurso))!;
    private static readonly StoreObjectIdentifier Tabela = StoreObjectIdentifier.Table("ofertas", null);

    [Theory]
    [InlineData(nameof(CopiaDaOfertaDeCurso.Codigo), "oferta_curso_codigo", 50)]
    [InlineData(nameof(CopiaDaOfertaDeCurso.CursoNome), "oferta_curso_nome", 200)]
    [InlineData(nameof(CopiaDaOfertaDeCurso.CursoGrau), "oferta_curso_grau", 60)]
    [InlineData(nameof(CopiaDaOfertaDeCurso.UnidadeOfertanteNome), "oferta_unidade_ofertante_nome", 250)]
    public void Configurar_CamposDaCopia_MapeiaColunasObrigatorias(string nome, string coluna, int limite)
    {
        IProperty propriedade = Copia.FindProperty(nome)!;
        propriedade.GetColumnName(Tabela).Should().Be(coluna);
        propriedade.GetMaxLength().Should().Be(limite);
        propriedade.IsColumnNullable(Tabela).Should().BeFalse();
        propriedade.GetComment().Should().Contain("do cadastro");
        ClassificacaoDeProtecaoNoModelo.Resolver(propriedade).Should().Be(ClassificacaoDeProtecao.Pessoal);
    }

    [Fact]
    public void Configurar_Turnos_ComparaConteudoEOrdem()
    {
        IProperty turnos = Copia.FindProperty(nameof(CopiaDaOfertaDeCurso.Turnos))!;
        ValueComparer comparador = turnos.GetValueComparer();
        IReadOnlyList<string> original = Array.AsReadOnly(new[] { "MATUTINO", "NOTURNO" });
        object instantaneo = comparador.Snapshot(original)!;

        comparador.Equals(original, instantaneo).Should().BeTrue();
        comparador.Equals(original, new[] { "NOTURNO", "MATUTINO" }).Should().BeFalse();
    }

    [Fact]
    public void Configurar_Turnos_MapeiaListaObrigatoria()
    {
        IProperty turnos = Copia.FindProperty(nameof(CopiaDaOfertaDeCurso.Turnos))!;
        turnos.GetColumnName(Tabela).Should().Be("oferta_turnos");
        turnos.GetColumnType().Should().Be("character varying(30)[]");
        turnos.IsColumnNullable(Tabela).Should().BeFalse();
        ClassificacaoDeProtecaoNoModelo.Resolver(turnos).Should().Be(ClassificacaoDeProtecao.Pessoal);
    }

    [Fact]
    public void Configurar_Codigo_InstalaConversorERestricao()
    {
        Copia.FindProperty(nameof(CopiaDaOfertaDeCurso.Codigo))!.GetValueConverter().Should().BeOfType<CodigoDeCadastroValueConverter>();
        ICheckConstraint restricao = Modelo.FindEntityType(typeof(Dona))!.GetCheckConstraints().Single();
        restricao.Name.Should().Be("ck_ofertas_oferta_curso_codigo_formato");
        restricao.Sql.Should().Be($"oferta_curso_codigo ~ '{FormatoDoCodigo.ExpressaoNoBanco}'");
        restricao.Name.Length.Should().BeLessThanOrEqualTo(63);
    }

    private static void Configurar(ModelBuilder modelo)
    {
        EntityTypeBuilder<Dona> dona = modelo.Entity<Dona>();
        dona.ToTable("ofertas", tabela => CopiaDaOfertaDeCursoOwnedConfiguration.ConfigurarRestricao(tabela, "ofertas"));
        dona.HasKey(item => item.Id);
        dona.OwnsOne(item => item.Oferta, oferta => CopiaDaOfertaDeCursoOwnedConfiguration.Configure(oferta, "do cadastro", ClassificacaoDeProtecao.Pessoal));
        dona.Navigation(item => item.Oferta).IsRequired();
    }

    private sealed class Dona
    {
        public Guid Id { get; set; }
        public CopiaDaOfertaDeCurso Oferta { get; set; } = null!;
    }
}
