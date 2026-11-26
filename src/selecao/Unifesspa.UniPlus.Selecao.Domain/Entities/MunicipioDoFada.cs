namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Um município do snapshot congelado de <see cref="ConfiguracaoDoFada"/> — cópia
/// por valor de um item de <c>BaseLegalDoFadaView.Municipios</c> (Configuração)
/// no momento em que o Fator de Atenuação das Desigualdades de Acesso (FADA) foi definido.
/// Editar ou desativar a Base Legal depois não altera processos já configurados.
/// </summary>
/// <remarks>
/// <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de <see cref="CondicaoOfertada"/>/
/// <see cref="VagaOfertada"/>: filho substituível por inteiro junto com o FADA que o congelou.
/// </remarks>
public sealed class MunicipioDoFada : EntityBase
{
    public Guid ConfiguracaoDoFadaId { get; private set; }
    public string CodigoIbge { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Uf { get; private set; } = string.Empty;

    private MunicipioDoFada() { }

    public static MunicipioDoFada Criar(string codigoIbge, string nome, string uf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoIbge);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(uf);

        return new MunicipioDoFada
        {
            CodigoIbge = codigoIbge.Trim(),
            Nome = nome.Trim(),
            Uf = uf.Trim(),
        };
    }

    internal void VincularConfiguracao(Guid configuracaoDoFadaId) =>
        ConfiguracaoDoFadaId = configuracaoDoFadaId;
}
