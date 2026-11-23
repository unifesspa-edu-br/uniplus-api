namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

public interface IDescricaoDeCadastro
{
    string Cadastro { get; }

    Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken);
}
