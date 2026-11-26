namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>O rótulo atual de cada participação da reserva, que o catálogo do código dá sem consultar outro cadastro.</summary>
public sealed class DescricaoDaParticipacaoDaReserva : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.ParticipacoesDaReserva;

    public Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        Dictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> rotulos = [];
        foreach ((string Codigo, string? Complemento) codigo in codigos)
        {
            if (CatalogoDe<ParticipacaoDaReserva>.TentarObter(codigo.Codigo, out ParticipacaoDaReserva participacao))
            {
                rotulos.Add(codigo, [participacao.Rotulo()]);
            }
        }

        return Task.FromResult<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>>(rotulos);
    }
}
