namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoCongelado;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O quadro de pesos por área do ENEM da resolução citada, lido do cadastro vivo na forma da exportação da versão publicada.
/// A resolução que o cadastro não conhece, não tem pesos para todos os grupos ou tem forma recusada fica sem o quadro.
/// </summary>
public sealed class DescricaoDePesosPorAreaDoEnem(IPesoAreaEnemReader pesos, IEsquemaDoDocumentoDaConfiguracao esquema) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.PesosPorAreaDoEnem;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        Dictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> atributos = [];
        foreach (string resolucao in AtributosDosCodigos.Textos(codigos))
        {
            QuadroDePesosResolvido resolvido = await QuadroDePesosResolvido.DaResolucaoAsync(resolucao, pesos, cancellationToken).ConfigureAwait(false);
            if (resolvido.Quadro.Count > 0)
            {
                atributos.Add((resolucao, null), [QuadroNaFormaDaExportacao(resolvido.Quadro)]);
            }
        }

        return atributos;
    }

    private JsonArray QuadroNaFormaDaExportacao(IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro) =>
        ProjecaoDeClassificacao.ProjetarQuadroDePesos(
            AreasDoEnemNoDocumentoCongelado.QuadroDePesos(quadro), esquema.Forma.Propriedades["classificacao"]);
}
