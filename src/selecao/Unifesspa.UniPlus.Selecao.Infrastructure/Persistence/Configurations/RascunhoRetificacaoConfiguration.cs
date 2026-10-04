namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Domain.Entities;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Regras.Formularios;

public sealed class RascunhoRetificacaoConfiguration : IEntityTypeConfiguration<RascunhoRetificacao>
{
    /// <summary>
    /// O índice que <b>garante</b> a unicidade da sessão editorial — não a checagem em
    /// memória, que perde a corrida entre duas aberturas concorrentes que leram o agregado
    /// antes de qualquer uma gravar.
    /// </summary>
    public const string IndiceUnicoPorProcesso = "ux_rascunhos_retificacao_processo";

    public void Configure(EntityTypeBuilder<RascunhoRetificacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("rascunhos_retificacao");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ProcessoSeletivoId).IsRequired();

        // O teto da coluna espelha RascunhoRetificacao.MotivoMaxLength, que já é o MENOR
        // dos dois limites que o motivo atravessa (o de Publicações, no fechamento). O
        // domínio recusa antes de chegar aqui — a coluna é o backstop, não a regra.
        builder.Property(r => r.Motivo)
            .HasMaxLength(RascunhoRetificacao.MotivoMaxLength)
            .IsRequired();

        builder.Property(r => r.VersaoBaseId).IsRequired();
        builder.Property(r => r.NumeroVersaoBase).IsRequired();
        builder.Property(r => r.VersaoBaseComIdentificadorLegivel).IsRequired();

        // Sem default: a abertura sempre lê as versões publicadas, e não há valor a fabricar para uma
        // sessão que não as leu.
        builder.Property(r => r.FatosDasVersoesPublicadas)
            .HasConversion(FatosDasVersoesPublicadasConverter, FatosDasVersoesPublicadasComparer)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(r => r.AbertoEm).IsRequired();
        builder.Property(r => r.AbertoPorSub).HasMaxLength(255).IsRequired();
        builder.Property(r => r.Revisao).IsRequired();

        // UNIQUE simples, não parcial: o rascunho é APAGADO no fechamento e no descarte —
        // não há histórico de sessões encerradas convivendo na tabela, e portanto nada a
        // filtrar. Um índice parcial aqui sugeriria um estado "sessão morta" que não
        // existe.
        builder.HasIndex(r => r.ProcessoSeletivoId)
            .IsUnique()
            .HasDatabaseName(IndiceUnicoPorProcesso);
    }

    private static readonly ValueConverter<FatosDasVersoesPublicadas, string> FatosDasVersoesPublicadasConverter =
        new(fatos => Serializar(fatos), json => Desserializar(json));

    // O valor é imutável depois da abertura; a comparação pelo JSON basta para o rastreamento.
    private static readonly ValueComparer<FatosDasVersoesPublicadas> FatosDasVersoesPublicadasComparer =
        new(
            (a, b) => Serializar(a) == Serializar(b),
            fatos => Serializar(fatos).GetHashCode(StringComparison.Ordinal),
            fatos => Desserializar(Serializar(fatos)));

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>A finalidade vai pelo token do formulário, e não pelo ordinal do enum.</summary>
    private static string Serializar(FatosDasVersoesPublicadas? fatos) =>
        fatos is null
            ? string.Empty
            : JsonSerializer.Serialize(
                new FatosDasVersoesPublicadasJson(
                    fatos.Garantidos.ToDictionary(static par => EstruturaFormulario.ParaToken(par.Key), static par => par.Value.ToArray(), StringComparer.Ordinal),
                    [.. fatos.ItensDaInscricaoNaBase],
                    fatos.GruposDaInscricaoNaBase.ToDictionary(static par => par.Key, static par => par.Value.ToArray(), StringComparer.Ordinal)),
                JsonOptions);

    private static FatosDasVersoesPublicadas Desserializar(string json)
    {
        FatosDasVersoesPublicadasJson lido = JsonSerializer.Deserialize<FatosDasVersoesPublicadasJson>(json, JsonOptions)
            ?? throw new JsonException("A coluna dos fatos das versões publicadas está vazia.");
        return FatosDasVersoesPublicadas.Reconstituir(
            lido.Garantidos.ToDictionary(static par => FinalidadeDoToken(par.Key), static par => (IReadOnlyCollection<string>)par.Value),
            lido.ItensDaInscricaoNaBase,
            lido.GruposDaInscricaoNaBase.ToDictionary(static par => par.Key, static par => (IReadOnlyCollection<string>)par.Value, StringComparer.Ordinal));
    }

    /// <summary>Token fora do vocabulário estoura na carga, alto e cedo, em vez de virar finalidade nenhuma.</summary>
    private static FinalidadeFormulario FinalidadeDoToken(string token) =>
        EstruturaFormulario.FinalidadeDoToken(token) is var finalidade && finalidade != FinalidadeFormulario.Nenhuma
            ? finalidade
            : throw new JsonException($"Finalidade '{token}' fora do vocabulário dos formulários.");

    private sealed record FatosDasVersoesPublicadasJson(
        Dictionary<string, string[]> Garantidos,
        string[] ItensDaInscricaoNaBase,
        Dictionary<string, string[]> GruposDaInscricaoNaBase);
}
