namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Um município do snapshot congelado de <see cref="ConfiguracaoBonusRegional"/> — cópia
/// por valor de um item de <c>BaseLegalBonusRegionalView.Municipios</c> (Configuração,
/// Story #1465) no momento em que o bônus foi definido. Editar ou desativar a Base Legal
/// depois não altera processos já configurados.
/// </summary>
/// <remarks>
/// <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de <see cref="OfertaCondicao"/>/
/// <see cref="VagaOfertada"/>: filho substituível por inteiro junto com o bônus que o congelou.
/// </remarks>
public sealed class ConfiguracaoBonusRegionalMunicipio : EntityBase
{
    public Guid ConfiguracaoBonusRegionalId { get; private set; }
    public string CodigoIbge { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Uf { get; private set; } = string.Empty;

    private ConfiguracaoBonusRegionalMunicipio() { }

    public static ConfiguracaoBonusRegionalMunicipio Criar(string codigoIbge, string nome, string uf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoIbge);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(uf);

        return new ConfiguracaoBonusRegionalMunicipio
        {
            CodigoIbge = codigoIbge.Trim(),
            Nome = nome.Trim(),
            Uf = uf.Trim(),
        };
    }

    internal void VincularConfiguracao(Guid configuracaoBonusRegionalId) =>
        ConfiguracaoBonusRegionalId = configuracaoBonusRegionalId;
}
