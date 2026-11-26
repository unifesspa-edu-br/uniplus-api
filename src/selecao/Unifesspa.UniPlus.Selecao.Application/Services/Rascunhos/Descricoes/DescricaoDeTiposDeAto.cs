namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>
/// O catálogo de tipos de ato é lido na versão vigente no dia de hoje, no fuso institucional, como na importação. Sem o dia
/// (fuso institucional não reconhecido) nenhum nome é descrito, e a exportação segue sem recusa.
/// </summary>
public sealed class DescricaoDeTiposDeAto(ITipoAtoPublicadoReader tipos, LeituraDoInstanteDoComando leituraDoInstante) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.TiposDeAto;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        Result<InstanteDoComando> instante = leituraDoInstante.Ler();
        IReadOnlyList<TipoAtoPublicadoView> vigentes = instante.IsFailure
            ? []
            : await tipos.ListarVigentesAsync(AtributosDosCodigos.Textos(codigos), instante.Value.Dia, cancellationToken).ConfigureAwait(false);
        return AtributosDosCodigos.DosPedidos(vigentes, codigos, static tipo => tipo.Codigo, static tipo => [tipo.Nome]);
    }
}
