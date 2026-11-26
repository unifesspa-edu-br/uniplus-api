namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeCategoriasDeDocumento(ICategoriaDocumentoReader categorias) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.CategoriasDeDocumento;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        IReadOnlyList<CategoriaDocumentoView> vivas = await categorias.ListarVivasPorCodigosAsync(AtributosDosCodigos.Textos(codigos), cancellationToken).ConfigureAwait(false);
        return AtributosDosCodigos.DosPedidos(vivas, codigos, static categoria => categoria.Codigo, static categoria => [categoria.Nome]);
    }
}
