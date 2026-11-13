namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Forma das colunas da <see cref="RegraDeDeliberacao"/> embutida na banca da fase e na banca da
/// etapa, num lugar só: as duas bancas guardam a mesma regra e não podem divergir na tabela.
/// </summary>
internal static class RegraDeDeliberacaoOwnedConfiguration
{
    public static OwnedNavigationBuilder<TDono, RegraDeDeliberacao> ConfigurarRegraDeDeliberacao<TDono>(
        this OwnedNavigationBuilder<TDono, RegraDeDeliberacao> regra)
        where TDono : class
    {
        ArgumentNullException.ThrowIfNull(regra);

        regra.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        regra.Property(r => r.Modo).HasColumnName("regra_deliberacao_modo").HasConversion<int>().IsRequired();
        regra.Property(r => r.Integrantes).HasColumnName("regra_deliberacao_integrantes").IsRequired();
        regra.Property(r => r.Suplentes).HasColumnName("regra_deliberacao_suplentes").IsRequired();
        regra.Property(r => r.Quorum).HasColumnName("regra_deliberacao_quorum");
        regra.Property(r => r.Criterio).HasColumnName("regra_deliberacao_criterio").HasConversion<int>();
        regra.Property(r => r.Desempate).HasColumnName("regra_deliberacao_desempate").HasConversion<int>();
        regra.Property(r => r.Encerramento).HasColumnName("regra_deliberacao_encerramento").HasConversion<int>();
        regra.Property(r => r.SemUnanimidade).HasColumnName("regra_deliberacao_sem_unanimidade").HasConversion<int>();

        return regra;
    }
}
