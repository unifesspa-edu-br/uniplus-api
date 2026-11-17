namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>A divulgação pública e a taxa de inscrição do processo.</summary>
public sealed class DivulgacaoETaxaDeclaradas
{
    internal DivulgacaoETaxaDeclaradas(ConfiguracaoDivulgacao? configuracaoDivulgacao, ConfiguracaoTaxaInscricao? configuracaoTaxaInscricao)
    {
        ConfiguracaoDivulgacao = configuracaoDivulgacao;
        ConfiguracaoTaxaInscricao = configuracaoTaxaInscricao;
    }

    public ConfiguracaoDivulgacao? ConfiguracaoDivulgacao { get; }

    public ConfiguracaoTaxaInscricao? ConfiguracaoTaxaInscricao { get; }
}
