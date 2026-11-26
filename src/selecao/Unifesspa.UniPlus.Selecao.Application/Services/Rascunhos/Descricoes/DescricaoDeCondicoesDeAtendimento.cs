namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeCondicoesDeAtendimento(ICondicaoAtendimentoReader condicoes) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.CondicoesDeAtendimento;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        IReadOnlyList<CondicaoAtendimentoView> vivas = await condicoes.ListarVivosAsync(cancellationToken).ConfigureAwait(false);
        return AtributosDosCodigos.DosPedidos(vivas, codigos, static condicao => condicao.Codigo, static condicao => [condicao.Nome]);
    }
}
