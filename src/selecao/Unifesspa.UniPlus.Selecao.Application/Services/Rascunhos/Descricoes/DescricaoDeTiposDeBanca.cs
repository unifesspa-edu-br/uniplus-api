namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeTiposDeBanca(ITipoBancaReader tipos) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.TiposDeBanca;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        IReadOnlyList<TipoBancaView> vivos = await tipos.ListarVivosPorCodigosAsync(AtributosDosCodigos.Textos(codigos), cancellationToken).ConfigureAwait(false);
        return AtributosDosCodigos.DosPedidos(vivos, codigos, static tipo => tipo.Codigo.Codigo(), static tipo => [tipo.Nome]);
    }
}
