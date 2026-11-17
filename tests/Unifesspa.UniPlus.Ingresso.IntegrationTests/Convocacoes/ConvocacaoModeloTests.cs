namespace Unifesspa.UniPlus.Ingresso.IntegrationTests.Convocacoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Monolito.Persistencia;

public sealed class ConvocacaoModeloTests
{
    [Fact(DisplayName = "O modelo materializa a convocação pelo construtor sem parâmetros, que não registra evento")]
    public void Modelo_MaterializaAConvocacaoPeloConstrutorSemParametros()
    {
        using MonolitoDbContext contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<MonolitoDbContext>());
        IEntityType tipo = contexto.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Convocacao))!;

        ConstructorBinding binding = tipo.ConstructorBinding.Should().BeOfType<ConstructorBinding>().Subject;

        binding.Constructor.GetParameters().Should().BeEmpty();
    }
}
