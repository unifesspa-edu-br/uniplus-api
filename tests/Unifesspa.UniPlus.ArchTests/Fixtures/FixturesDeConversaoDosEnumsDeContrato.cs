namespace Unifesspa.UniPlus.ArchTests.Fixtures;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Contraprova da regra que exige a conversão dos enums de contrato na montagem do modelo: os tipos que chamam a convenção
/// têm de ser aceitos, e os que montam o modelo sem chamá-la têm de ser rejeitados. As duas composições repetem a forma da
/// composição real, em que o <c>Compor</c> chama um auxiliar privado, e este é quem chama a convenção.
/// </summary>
internal static class FixturesDeConversaoDosEnumsDeContrato
{
    internal sealed class ContextoQueConfiguraAConversao(DbContextOptions opcoes) : DbContext(opcoes)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.ConfigurarConversaoDosEnumsDeContrato();
        }
    }

    internal sealed class ContextoQueNaoConfiguraAConversao(DbContextOptions opcoes) : DbContext(opcoes)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }
    }

    internal static class ComposicaoQueConfiguraAConversao
    {
        public static void Compor(ModelBuilder modelBuilder) => AplicarConvencoes(modelBuilder);

        private static void AplicarConvencoes(ModelBuilder modelBuilder) => modelBuilder.ConfigurarConversaoDosEnumsDeContrato();
    }

    internal static class ComposicaoQueNaoConfiguraAConversao
    {
        public static void Compor(ModelBuilder modelBuilder) => AplicarConvencoes(modelBuilder);

        private static void AplicarConvencoes(ModelBuilder modelBuilder) => ArgumentNullException.ThrowIfNull(modelBuilder);
    }
}
