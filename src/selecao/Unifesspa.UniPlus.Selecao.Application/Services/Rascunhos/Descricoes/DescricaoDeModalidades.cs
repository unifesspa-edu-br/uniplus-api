namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeModalidades(IModalidadeReader modalidades) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.Modalidades;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        IReadOnlyList<ModalidadeView> vivas = await modalidades.ListarVivosAsync(cancellationToken).ConfigureAwait(false);
        return AtributosDosCodigos.DosPedidos(vivas, codigos, static modalidade => modalidade.Codigo, static modalidade => [modalidade.Descricao]);
    }
}
