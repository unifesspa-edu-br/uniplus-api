namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Forma das colunas dos <see cref="ParametrosRegraPrazoRecurso"/> embutidos, a mesma na regra de recurso da fase e no recurso da
/// etapa: declarar num lugar só impede que as duas tabelas guardem os parâmetros com nomes ou larguras diferentes.
/// </summary>
/// <remarks>
/// Os dois prazos são <c>numeric(18,4)</c>, a precisão que o validador do pedido, o documento e o registro da versão publicada
/// também aceitam. O prazo da 2ª instância é opcional: sem instância superior, as duas colunas ficam nulas. A unidade, o efeito
/// suspensivo e a guarda da vaga são gravados pelo código de contrato, por convenção do modelo, sem conversão declarada aqui,
/// e cada uma das quatro colunas (as duas unidades de prazo, o efeito suspensivo e a guarda da vaga) aceita só os códigos do
/// próprio tipo (<see cref="RestringirCodigosDosParametrosDoRecurso"/>). A unidade do prazo da 2ª instância aceita também
/// nulo, que é a ausência de instância superior.
/// </remarks>
internal static class ParametrosDoRecursoOwnedConfiguration
{
    private const int PrecisaoDoPrazo = 18;
    private const int EscalaDoPrazo = 4;
    private const string ColunaDoEfeitoSuspensivo = "efeito_suspensivo";
    private const string ColunaDaGuardaDaVaga = "guarda_da_vaga";
    private const string PrefixoDoPrazo = "prazo";
    private const string PrefixoDoPrazoDaSegundaInstancia = "prazo_segunda_instancia";
    private const string SufixoDaColunaDaUnidade = "_unidade";
    private const string ColunaDaUnidadeDoPrazo = PrefixoDoPrazo + SufixoDaColunaDaUnidade;
    private const string ColunaDaUnidadeDaSegundaInstancia = PrefixoDoPrazoDaSegundaInstancia + SufixoDaColunaDaUnidade;
    // O nome da restrição da 2ª instância não repete "prazo" para caber nos 63 bytes que o PostgreSQL aceita em um identificador.
    private const string SufixoDaRestricaoDaUnidadeDaSegundaInstancia = "unidade_da_segunda_instancia";

    /// <summary>
    /// Restringe, na tabela dona, o efeito suspensivo, a guarda da vaga e as unidades dos dois prazos aos códigos de contrato
    /// de cada tipo. Cada restrição olha uma só coluna: nenhuma delas liga uma escolha a outra declaração do recurso.
    /// </summary>
    public static void RestringirCodigosDosParametrosDoRecurso<TDono>(TableBuilder<TDono> tabela, string nomeDaTabela)
        where TDono : class
    {
        ArgumentNullException.ThrowIfNull(tabela);

        RestringirEfeitoSuspensivoEGuardaDaVaga(tabela, nomeDaTabela);
        RestringirUnidadesDosPrazos(tabela, nomeDaTabela);
    }

    public static OwnedNavigationBuilder<TDono, ParametrosRegraPrazoRecurso> ConfigurarParametrosDoRecurso<TDono>(
        this OwnedNavigationBuilder<TDono, ParametrosRegraPrazoRecurso> parametros)
        where TDono : class
    {
        ArgumentNullException.ThrowIfNull(parametros);

        parametros.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        parametros.OwnsOne(x => x.PrazoDeInterposicao, prazo => ConfigurarPrazo(prazo, PrefixoDoPrazo));
        parametros.Navigation(x => x.PrazoDeInterposicao).IsRequired();
        parametros.OwnsOne(x => x.PrazoDaSegundaInstancia, prazo => ConfigurarPrazo(prazo, PrefixoDoPrazoDaSegundaInstancia));
        parametros.Property(x => x.AdmiteInstanciaSuperior).HasComment("Indica se há recurso à instância superior.").HasColumnName("admite_instancia_superior").IsRequired();
        parametros.Property(x => x.EfeitoSuspensivo).HasComment("Efeito suspensivo do recurso na 1ª instância.").HasColumnName(ColunaDoEfeitoSuspensivo).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).IsRequired();
        parametros.Property(x => x.GuardaDaVaga).HasComment("Guarda da vaga durante o recurso.").HasColumnName(ColunaDaGuardaDaVaga).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).IsRequired();

        return parametros;
    }

    private static void ConfigurarPrazo(OwnedNavigationBuilder<ParametrosRegraPrazoRecurso, PrazoDoRecurso> prazo, string prefixo)
    {
        prazo.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        prazo.Property(x => x.Valor).HasComment("Duração do prazo de recurso na unidade informada.").HasColumnName($"{prefixo}_valor").HasPrecision(PrecisaoDoPrazo, EscalaDoPrazo);
        prazo.Property(x => x.Unidade).HasComment("Unidade do prazo.").HasColumnName(prefixo + SufixoDaColunaDaUnidade).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo);
    }

    private static void RestringirEfeitoSuspensivoEGuardaDaVaga<TDono>(TableBuilder<TDono> tabela, string nomeDaTabela)
        where TDono : class
    {
        tabela.HasCheckConstraint($"ck_{nomeDaTabela}_{ColunaDoEfeitoSuspensivo}", CodigosDoTipo<EfeitoSuspensivo>(ColunaDoEfeitoSuspensivo));
        tabela.HasCheckConstraint($"ck_{nomeDaTabela}_{ColunaDaGuardaDaVaga}", CodigosDoTipo<GuardaDaVaga>(ColunaDaGuardaDaVaga));
    }

    private static void RestringirUnidadesDosPrazos<TDono>(TableBuilder<TDono> tabela, string nomeDaTabela)
        where TDono : class
    {
        tabela.HasCheckConstraint($"ck_{nomeDaTabela}_{ColunaDaUnidadeDoPrazo}", CodigosDoTipo<UnidadePrazo>(ColunaDaUnidadeDoPrazo));
        tabela.HasCheckConstraint(
            $"ck_{nomeDaTabela}_{SufixoDaRestricaoDaUnidadeDaSegundaInstancia}",
            CodigosDoTipo<UnidadePrazo>(ColunaDaUnidadeDaSegundaInstancia));
    }

    private static string CodigosDoTipo<TEnum>(string coluna)
        where TEnum : struct, Enum =>
        $"{coluna} IN ({SqlDosCodigosDeContrato.Lista<TEnum>()})";
}
