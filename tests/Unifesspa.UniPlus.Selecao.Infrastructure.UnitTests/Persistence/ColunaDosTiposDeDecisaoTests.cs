namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

/// <summary>
/// A coluna dos tipos de decisão guarda o código de contrato de cada tipo, e o rastreamento percebe a lista que o
/// domínio altera no lugar, com <c>Clear</c> seguido de <c>AddRange</c>. Não abre conexão: só o modelo e o rastreador.
/// </summary>
public sealed class ColunaDosTiposDeDecisaoTests
{
    private const string Campo = "_tiposDeDecisao";

    [Fact(DisplayName = "O elemento da lista é gravado pelo código de contrato do tipo, na coluna text[] com default vazio")]
    public void Modelo_GravaOElementoPeloCodigoDeContrato()
    {
        using ContextoDeTeste contexto = Novo();

        IProperty propriedade = contexto.Model.FindEntityType(typeof(EntidadeDeTeste))!.FindProperty(Campo)!;

        propriedade.GetColumnName().Should().Be("tipos_de_decisao");
        propriedade.GetColumnType().Should().Be("text[]");
        propriedade.GetDefaultValueSql().Should().Be("'{}'::text[]");
        propriedade.IsNullable.Should().BeFalse();
        propriedade.GetElementType()!.GetValueConverter().Should().BeOfType<CodigoDeContratoValueConverter<TipoMotivoDeDecisao>>();
    }

    [Fact(DisplayName = "O mapeamento do elemento da lista usa o conversor do código de contrato, e não a conversão do enum para o nome")]
    public void Modelo_MapeamentoDoElementoUsaOConversorDoCodigo()
    {
        using ContextoDeTeste contexto = Novo();

        IProperty propriedade = contexto.Model.FindEntityType(typeof(EntidadeDeTeste))!.FindProperty(Campo)!;

        propriedade.GetTypeMapping().ElementTypeMapping!.Converter.Should().BeOfType<CodigoDeContratoValueConverter<TipoMotivoDeDecisao>>();
    }

    [Fact(DisplayName = "Esvaziar e repreencher a lista rastreada com outros tipos marca a coluna como modificada")]
    public void ListaRastreada_EsvaziarERepreencher_MarcaAColunaModificada()
    {
        using ContextoDeTeste contexto = Novo();
        EntidadeDeTeste entidade = new([TipoMotivoDeDecisao.AnaliseDocumental]);
        contexto.Attach(entidade);

        entidade.Substituir([TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.Heteroidentificacao]);
        contexto.ChangeTracker.DetectChanges();

        contexto.Entry(entidade).Property(Campo).IsModified.Should().BeTrue();
    }

    [Fact(DisplayName = "Esvaziar e repreencher a lista rastreada com os mesmos tipos não marca a coluna como modificada")]
    public void ListaRastreada_EsvaziarERepreencherIgual_NaoMarcaAColuna()
    {
        using ContextoDeTeste contexto = Novo();
        EntidadeDeTeste entidade = new([TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.Classificacao]);
        contexto.Attach(entidade);

        entidade.Substituir([TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.Classificacao]);
        contexto.ChangeTracker.DetectChanges();

        contexto.Entry(entidade).Property(Campo).IsModified.Should().BeFalse();
    }

    [Fact(DisplayName = "Esvaziar a lista rastreada marca a coluna como modificada")]
    public void ListaRastreada_Esvaziar_MarcaAColunaModificada()
    {
        using ContextoDeTeste contexto = Novo();
        EntidadeDeTeste entidade = new([TipoMotivoDeDecisao.AnaliseDocumental]);
        contexto.Attach(entidade);

        entidade.Substituir([]);
        contexto.ChangeTracker.DetectChanges();

        contexto.Entry(entidade).Property(Campo).IsModified.Should().BeTrue();
    }

    private static ContextoDeTeste Novo() =>
        new(new DbContextOptionsBuilder<ContextoDeTeste>().UseNpgsql("Host=sem-conexao;Database=modelo").Options);

    private sealed class EntidadeDeTeste(IEnumerable<TipoMotivoDeDecisao> tipos)
    {
        private readonly List<TipoMotivoDeDecisao> _tiposDeDecisao = [.. tipos];

        private EntidadeDeTeste()
            : this([])
        {
        }

        public Guid Id { get; private set; } = Guid.CreateVersion7();

        public IReadOnlyList<TipoMotivoDeDecisao> TiposDeDecisao => _tiposDeDecisao.AsReadOnly();

        public void Substituir(IEnumerable<TipoMotivoDeDecisao> novos)
        {
            List<TipoMotivoDeDecisao> substitutos = [.. novos];
            _tiposDeDecisao.Clear();
            _tiposDeDecisao.AddRange(substitutos);
        }
    }

    private sealed class ContextoDeTeste(DbContextOptions<ContextoDeTeste> opcoes) : DbContext(opcoes)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntidadeDeTeste>(static builder =>
            {
                builder.HasKey(e => e.Id);
                builder.MapearTiposDeDecisao();
            });
            modelBuilder.ConfigurarConversaoDosEnumsDeContrato();
        }
    }
}
