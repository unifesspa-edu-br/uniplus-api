namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

public sealed class UniPlusDbContextTests
{
    [Fact(DisplayName = "O modelo atribui a cada tipo o esquema do módulo que o registrou")]
    public void Modelo_AtribuiOEsquemaDoModuloAosTiposQueEleRegistrou()
    {
        using ContextoDeTeste contexto = ContextoDeTeste.Criar(new ModuloDeTeste<EntidadeDoModuloA>("modulo_a"), new ModuloDeTeste<EntidadeDoModuloB>("modulo_b"));

        EsquemaDe<EntidadeDoModuloA>(contexto).Should().Be("modulo_a");
        EsquemaDe<EntidadeDoModuloB>(contexto).Should().Be("modulo_b");
    }

    [Fact(DisplayName = "O modelo guarda a idempotência no esquema plataforma")]
    public void Modelo_GuardaAIdempotenciaNoEsquemaDaPlataforma()
    {
        using ContextoDeTeste contexto = ContextoDeTeste.Criar(new ModuloDeTeste<EntidadeDoModuloA>("modulo_a"));

        EsquemaDe<IdempotencyEntry>(contexto).Should().Be(UniPlusDbContext.EsquemaDaPlataforma);
    }

    [Fact(DisplayName = "O modelo guarda a função de normalização para comparação no esquema plataforma")]
    public void Modelo_GuardaANormalizacaoParaComparacaoNoEsquemaDaPlataforma()
    {
        using ContextoDeTeste contexto = ContextoDeTeste.Criar(new ModuloDeTeste<EntidadeDoModuloA>("modulo_a"));

        IDbFunction funcao = contexto.GetService<IDesignTimeModel>().Model
            .FindDbFunction(typeof(FuncoesDaPlataforma).GetMethod(nameof(FuncoesDaPlataforma.NormalizarParaComparacao))!)!;

        funcao.Schema.Should().Be(UniPlusDbContext.EsquemaDaPlataforma);
        funcao.Name.Should().Be("normalizar_para_comparacao");
    }

    [Fact(DisplayName = "Dois módulos com o mesmo esquema são recusados")]
    public void Modelo_RecusaEsquemaRepetido()
    {
        using ContextoDeTeste contexto = ContextoDeTeste.Criar(new ModuloDeTeste<EntidadeDoModuloA>("repetido"), new ModuloDeTeste<EntidadeDoModuloB>("repetido"));

        Action construir = () => _ = contexto.Model;

        construir.Should().Throw<InvalidOperationException>().WithMessage("*repetido*");
    }

    [Fact(DisplayName = "Um módulo não pode usar o esquema da plataforma")]
    public void Modelo_RecusaEsquemaDaPlataformaEmModulo()
    {
        using ContextoDeTeste contexto = ContextoDeTeste.Criar(new ModuloDeTeste<EntidadeDoModuloA>(UniPlusDbContext.EsquemaDaPlataforma));

        Action construir = () => _ = contexto.Model;

        construir.Should().Throw<InvalidOperationException>().WithMessage("*plataforma*");
    }

    [Fact(DisplayName = "O descarte limpa o rastreamento do escopo inteiro")]
    public void DescartarAlteracoesNaoSalvas_LimpaORastreamento()
    {
        using ContextoDeTeste contexto = ContextoDeTeste.Criar(new ModuloDeTeste<EntidadeDoModuloA>("modulo_a"));
        contexto.Set<EntidadeDoModuloA>().Add(new EntidadeDoModuloA { Id = Guid.CreateVersion7() });

        contexto.DescartarAlteracoesNaoSalvas();

        contexto.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static string? EsquemaDe<TEntidade>(DbContext contexto) =>
        contexto.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TEntidade))!.GetSchema();
}
