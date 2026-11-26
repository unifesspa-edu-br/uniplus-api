namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;

using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>Mapeia a cópia de uma oferta nas colunas da tabela dona.</summary>
public static class CopiaDaOfertaDeCursoOwnedConfiguration
{
    /// <summary>Configura o check de formato na tabela dona.</summary>
    public static void ConfigurarRestricao<TOwner>(TableBuilder<TOwner> tabela, string nomeDaTabela) where TOwner : class
    {
        ArgumentNullException.ThrowIfNull(tabela);
        string nome = $"ck_{nomeDaTabela}_oferta_curso_codigo_formato";
        if (Encoding.UTF8.GetByteCount(nome) > 63)
        {
            throw new ArgumentException("O nome do check excede 63 caracteres.", nameof(nomeDaTabela));
        }

        tabela.HasCheckConstraint(nome, $"oferta_curso_codigo ~ '{FormatoDoCodigo.ExpressaoNoBanco}'");
    }

    /// <summary>Configura os campos e a classificação da cópia obrigatória.</summary>
    public static void Configure<TOwner>(OwnedNavigationBuilder<TOwner, CopiaDaOfertaDeCurso> oferta,
        string origem, ClassificacaoDeProtecao classificacao) where TOwner : class
    {
        ArgumentNullException.ThrowIfNull(oferta);
        oferta.ClassificaPorPadrao(classificacao);
        ConfigurarCurso(oferta, origem);
        ConfigurarUnidadeETurnos(oferta, origem);
    }

    private static void ConfigurarCurso<TOwner>(OwnedNavigationBuilder<TOwner, CopiaDaOfertaDeCurso> oferta, string origem) where TOwner : class
    {
        oferta.Property(copia => copia.Codigo).HasColumnName("oferta_curso_codigo")
            .HasMaxLength(CopiaDaOfertaDeCurso.TamanhoMaximoDoCodigo).HasConversion<CodigoDeCadastroValueConverter>()
            .IsRequired().HasComment($"Código da oferta de curso, copiado {origem}.");
        oferta.Property(copia => copia.CursoNome).HasColumnName("oferta_curso_nome")
            .HasMaxLength(CopiaDaOfertaDeCurso.TamanhoMaximoDoNomeDoCurso).IsRequired()
            .HasComment($"Nome do curso da oferta, copiado {origem}.");
        oferta.Property(copia => copia.CursoGrau).HasColumnName("oferta_curso_grau")
            .HasMaxLength(CopiaDaOfertaDeCurso.TamanhoMaximoDoGrauDoCurso).IsRequired()
            .HasComment($"Grau do curso da oferta, copiado {origem}.");
    }

    private static void ConfigurarUnidadeETurnos<TOwner>(OwnedNavigationBuilder<TOwner, CopiaDaOfertaDeCurso> oferta, string origem) where TOwner : class
    {
        oferta.Property(copia => copia.UnidadeOfertanteNome).HasColumnName("oferta_unidade_ofertante_nome")
            .HasMaxLength(CopiaDaOfertaDeCurso.TamanhoMaximoDoNomeDaUnidadeOfertante).IsRequired()
            .HasComment($"Nome da unidade ofertante, copiado {origem}.");
        oferta.PrimitiveCollection(copia => copia.Turnos).HasColumnName("oferta_turnos")
            .UsePropertyAccessMode(PropertyAccessMode.Property)
            .HasColumnType($"character varying({CopiaDaOfertaDeCurso.TamanhoMaximoDoTurno})[]").IsRequired()
            .HasComment($"Turnos que a oferta ocupa (tokens), copiados {origem}.");
    }
}
