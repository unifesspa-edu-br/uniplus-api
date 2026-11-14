namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Interceptors;

/// <summary>O que a Seleção acrescenta ao contexto único: as configurações, a sequência do número de inscrição e o interceptador do histórico das obrigatoriedades legais.</summary>
public sealed class SelecaoContribuicao : IContribuicaoDoModulo
{
    public const string NomeDoEsquema = "selecao";

    /// <summary>
    /// Sequência do número de inscrição: gerado só na primeira submissão, sem exigência de não ter lacunas (um
    /// <c>nextval</c> de uma submissão que falhe antes de gravar não é reaproveitado) — <c>bigint</c> nativo do
    /// Postgres, nunca um contador em memória nem uma coluna identity da tabela, porque o número precisa existir ANTES
    /// da linha gravada (o domínio o recebe pronto em <see cref="Domain.Entities.Inscricao.Submeter"/>).
    /// </summary>
    public const string SequenciaDeNumeroDeInscricao = "numero_inscricao_seq";

    public string Esquema => NomeDoEsquema;

    public void AplicarAoModelo(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SelecaoContribuicao).Assembly);
        modelBuilder.HasSequence<long>(SequenciaDeNumeroDeInscricao, NomeDoEsquema)
            .StartsAt(1)
            .IncrementsBy(1);
    }

    // Roda depois do soft delete e da auditoria: uma exclusão convertida em atualização também gera linha no histórico.
    public void AdicionarInterceptadores(DbContextOptionsBuilder options, IServiceProvider servicos)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(servicos);

        options.AddInterceptors(servicos.GetRequiredService<ObrigatoriedadeLegalHistoricoInterceptor>());
    }
}
