namespace Unifesspa.UniPlus.Ingresso.IntegrationTests.Convocacoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Monolito.Persistencia;

public sealed class ConvocacaoModeloTests
{
    [Fact]
    public void Modelo_IndicesUnicos_DeveUsarOsNomesConfigurados()
    {
        using MonolitoDbContext contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<MonolitoDbContext>());
        IModel modelo = contexto.GetService<IDesignTimeModel>().Model;
        IEntityType chamada = modelo.FindEntityType(typeof(Chamada))!;
        IEntityType protocolo = modelo.GetEntityTypes().Single(tipo => tipo.ClrType == typeof(ProtocoloConvocacao));

        chamada.GetIndexes().Single(indice => indice.IsUnique).GetDatabaseName()
            .Should().Be("ux_chamadas_edital_id_numero");
        protocolo.GetIndexes().Single(indice => indice.IsUnique).GetDatabaseName()
            .Should().Be("ux_convocacoes_protocolo");
    }

    [Fact(DisplayName = "O modelo materializa a convocação pelo construtor sem parâmetros, que não registra evento")]
    public void Modelo_MaterializaAConvocacaoPeloConstrutorSemParametros()
    {
        using MonolitoDbContext contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<MonolitoDbContext>());
        IEntityType tipo = contexto.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Convocacao))!;

        ConstructorBinding binding = tipo.ConstructorBinding.Should().BeOfType<ConstructorBinding>().Subject;

        binding.ParameterBindings.Should().BeEmpty();
    }
    [Theory]
    [InlineData(typeof(Convocacao), "convocacoes")]
    [InlineData(typeof(Matricula), "matriculas")]
    public void Modelo_Oferta_ColunasObrigatoriasComLimitesEProtecao(Type tipo, string tabela)
    {
        using MonolitoDbContext contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<MonolitoDbContext>());
        IEntityType dono = contexto.GetService<IDesignTimeModel>().Model.FindEntityType(tipo)!;
        IEntityType copia = dono.FindNavigation("OfertaCurso")!.TargetEntityType;
        StoreObjectIdentifier identificador = StoreObjectIdentifier.Table(tabela, "ingresso");
        ConferirColuna(copia, identificador, nameof(CopiaDaOfertaDeCurso.Codigo), "oferta_curso_codigo", 50);
        ConferirColuna(copia, identificador, nameof(CopiaDaOfertaDeCurso.CursoNome), "oferta_curso_nome", 200);
        ConferirColuna(copia, identificador, nameof(CopiaDaOfertaDeCurso.CursoGrau), "oferta_curso_grau", 60);
        ConferirColuna(copia, identificador, nameof(CopiaDaOfertaDeCurso.UnidadeOfertanteNome), "oferta_unidade_ofertante_nome", 250);
        ConferirRestricaoEConversor(dono, copia, tabela);
    }

    private static void ConferirRestricaoEConversor(IEntityType dono, IEntityType copia, string tabela)
    {
        StoreObjectIdentifier identificador = StoreObjectIdentifier.Table(tabela, "ingresso");
        IProperty turnos = copia.FindProperty(nameof(CopiaDaOfertaDeCurso.Turnos))!;
        turnos.GetColumnType().Should().Be("character varying(30)[]");
        ConferirColuna(copia, identificador, nameof(CopiaDaOfertaDeCurso.Turnos), "oferta_turnos", null);
        copia.FindProperty(nameof(CopiaDaOfertaDeCurso.Codigo))!.GetValueConverter().Should().BeOfType<CodigoDeCadastroValueConverter>();
        dono.GetCheckConstraints().Single(item => item.Name == $"ck_{tabela}_oferta_curso_codigo_formato").Sql
            .Should().Be($"oferta_curso_codigo ~ '{FormatoDoCodigo.ExpressaoNoBanco}'");
    }

    private static void ConferirColuna(IEntityType copia, StoreObjectIdentifier tabela, string nome, string coluna, int? limite)
    {
        IProperty propriedade = copia.FindProperty(nome)!;
        propriedade.GetColumnName(tabela).Should().Be(coluna);
        propriedade.GetMaxLength().Should().Be(limite);
        propriedade.IsColumnNullable(tabela).Should().BeFalse();
        ClassificacaoDeProtecaoNoModelo.Resolver(propriedade).Should().Be(ClassificacaoDeProtecao.Pessoal);
        propriedade.GetComment().Should().NotBeNullOrWhiteSpace();
    }
}
