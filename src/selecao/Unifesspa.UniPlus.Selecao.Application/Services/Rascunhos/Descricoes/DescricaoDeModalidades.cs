namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class DescricaoDeModalidades(IReadOnlyDictionary<string, ModalidadeNoCadastro> modalidades) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.Modalidades;

    public Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken) =>
        Task.FromResult(AtributosDosCodigos.DosPedidos(
            modalidades.Values, codigos, static modalidade => modalidade.Codigo, static modalidade => [modalidade.Nome, modalidade.Descricao]));
}
