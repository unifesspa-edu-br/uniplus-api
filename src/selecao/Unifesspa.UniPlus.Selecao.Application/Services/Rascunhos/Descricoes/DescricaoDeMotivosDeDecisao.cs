namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class DescricaoDeMotivosDeDecisao(IMotivoDeDecisaoRepository motivos) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.MotivosDeDecisao;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        IReadOnlyList<MotivoDeDecisao> encontrados = await motivos.ListarVivosPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false);
        return encontrados.ToDictionary(static motivo => (motivo.Codigo.Valor, (string?)motivo.Tipo.Codigo()), Atributos);
    }

    // O rascunho antecede a primeira publicação do edital.
    private static IReadOnlyList<object?> Atributos(MotivoDeDecisao motivo) =>
        [motivo.Descricao, motivo.Subclassificacao, motivo.Sentido.Codigo(), motivo.Sentido.Rotulo(), 1];
}
