namespace Unifesspa.UniPlus.Ingresso.IntegrationTests.Convocacoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.ValueObjects;
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

        binding.Constructor.GetParameters().Should().BeEmpty();
    }
}
