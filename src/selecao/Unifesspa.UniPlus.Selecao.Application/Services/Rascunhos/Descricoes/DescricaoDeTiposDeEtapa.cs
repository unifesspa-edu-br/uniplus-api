namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeTiposDeEtapa(ITipoEtapaReader tipos) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.TiposDeEtapa;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        IReadOnlyList<TipoEtapaView> ativos = await tipos.ListarAtivosAsync(cancellationToken).ConfigureAwait(false);
        return AtributosDosCodigos.DosPedidos(ativos, codigos, static tipo => tipo.Codigo, static tipo => [tipo.Nome]);
    }
}
