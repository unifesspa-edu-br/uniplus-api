namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeTiposDeDocumento(ITipoDocumentoReader tipos) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.TiposDeDocumento;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        IReadOnlyList<TipoDocumentoView> encontrados = await tipos.ListarVivosPorCodigosAsync([.. codigos.Select(static codigo => codigo.Codigo)], cancellationToken).ConfigureAwait(false);
        return encontrados.ToDictionary(static tipo => (tipo.Codigo, (string?)null), Atributos);
    }

    private static IReadOnlyList<object?> Atributos(TipoDocumentoView tipo) =>
        [tipo.Nome, tipo.Categoria, ClassificacoesDeProtecao.ParaTokenCanonico(tipo.ClassificacaoProtecao),
            tipo.FinalidadeTratamento, tipo.HipoteseLegal];
}
