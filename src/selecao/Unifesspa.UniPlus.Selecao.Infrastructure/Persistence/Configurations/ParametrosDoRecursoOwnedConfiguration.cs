namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Forma das colunas dos <see cref="ParametrosRegraPrazoRecurso"/> embutidos, a mesma na regra de recurso da fase e no recurso da
/// etapa: declarar num lugar só impede que as duas tabelas guardem os parâmetros com nomes ou larguras diferentes.
/// </summary>
/// <remarks>
/// Os dois prazos são <c>numeric(18,4)</c>, a precisão que o validador do pedido, o documento e o registro da versão publicada
/// também aceitam. O prazo da 2ª instância é opcional: sem instância superior, as duas colunas ficam nulas. A unidade, o efeito
/// suspensivo e a guarda da vaga são gravados pelo código de contrato, por convenção do modelo, sem conversão declarada aqui.
/// </remarks>
internal static class ParametrosDoRecursoOwnedConfiguration
{
    private const int PrecisaoDoPrazo = 18;
    private const int EscalaDoPrazo = 4;

    public static OwnedNavigationBuilder<TDono, ParametrosRegraPrazoRecurso> ConfigurarParametrosDoRecurso<TDono>(
        this OwnedNavigationBuilder<TDono, ParametrosRegraPrazoRecurso> parametros)
        where TDono : class
    {
        ArgumentNullException.ThrowIfNull(parametros);

        parametros.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        parametros.OwnsOne(x => x.PrazoDeInterposicao, prazo => ConfigurarPrazo(prazo, "prazo"));
        parametros.Navigation(x => x.PrazoDeInterposicao).IsRequired();
        parametros.OwnsOne(x => x.PrazoDaSegundaInstancia, prazo => ConfigurarPrazo(prazo, "prazo_segunda_instancia"));
        parametros.Property(x => x.AdmiteInstanciaSuperior).HasComment("Indica se há recurso à instância superior.").HasColumnName("admite_instancia_superior").IsRequired();
        parametros.Property(x => x.EfeitoSuspensivo).HasComment("Efeito do recurso sobre o andamento do processo. Código: SEM_EFEITO_SUSPENSIVO.").HasColumnName("efeito_suspensivo").IsRequired();
        parametros.Property(x => x.GuardaDaVaga).HasComment("Guarda da vaga durante o recurso: ATE_O_RESULTADO_DEFINITIVO ou NAO_GUARDA.").HasColumnName("guarda_da_vaga").IsRequired();

        return parametros;
    }

    private static void ConfigurarPrazo(OwnedNavigationBuilder<ParametrosRegraPrazoRecurso, PrazoDoRecurso> prazo, string prefixo)
    {
        prazo.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        prazo.Property(x => x.Valor).HasComment("Duração do prazo de recurso na unidade informada.").HasColumnName($"{prefixo}_valor").HasPrecision(PrecisaoDoPrazo, EscalaDoPrazo);
        prazo.Property(x => x.Unidade).HasComment("Unidade do prazo: HORAS, DIAS ou DIAS_UTEIS.").HasColumnName($"{prefixo}_unidade");
    }
}
