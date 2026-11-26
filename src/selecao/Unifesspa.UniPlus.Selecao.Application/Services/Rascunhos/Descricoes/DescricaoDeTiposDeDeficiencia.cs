namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeTiposDeDeficiencia(ITipoDeficienciaReader tipos) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.TiposDeDeficiencia;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        IReadOnlyList<TipoDeficienciaView> vivos = await tipos.ListarVivosAsync(cancellationToken).ConfigureAwait(false);
        return AtributosDosCodigos.DosPedidos(vivos, codigos, static tipo => tipo.Codigo, static tipo => [tipo.Nome]);
    }
}
