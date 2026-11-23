namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeUnidades(IUnidadeReader unidades) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.Unidades;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        Dictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> atributos = [];
        foreach ((string Codigo, string? Complemento) codigo in codigos)
        {
            UnidadeView? unidade = await unidades.ObterPorCodigoAsync(codigo.Codigo, cancellationToken).ConfigureAwait(false);
            if (unidade is not null)
            {
                atributos.Add(codigo, [unidade.Nome, unidade.Sigla]);
            }
        }

        return atributos;
    }
}
