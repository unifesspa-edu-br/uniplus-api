namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As regras do catálogo, o vocabulário de dados do desempate e os tipos de ato vigentes que o documento cita.</summary>
internal sealed record CadastrosDoCatalogoCitados(
    IReadOnlyDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> Regras,
    IReadOnlyDictionary<string, DescritorFatoCandidato> DadosDoDesempate,
    IReadOnlySet<string> TiposDeAto);

/// <summary>
/// Lê o que o documento cita do catálogo de regras e do cadastro de tipos de ato: cada regra pelo código e pela versão, o vocabulário
/// de dados do candidato (só quando o desempate por condição é citado) e os tipos de ato com versão vigente no dia da leitura.
/// </summary>
public sealed class LeituraDosCadastrosDoCatalogoCitados(
    IRegraCatalogoReader regras,
    ITipoAtoPublicadoReader tiposDeAto,
    IFatoCandidatoReader dadosDoCandidato)
{
    internal async Task<CadastrosDoCatalogoCitados> LerAsync(CodigosCitadosPorCadastro codigos, DateOnly dia, CancellationToken cancellationToken) => new(
        await LerRegrasAsync(codigos.Regras, cancellationToken).ConfigureAwait(false),
        await DadosDoDesempateAsync(codigos.Regras, cancellationToken).ConfigureAwait(false),
        await TiposDeAtoAsync(codigos.TiposDeAto, dia, cancellationToken).ConfigureAwait(false));

    internal async Task<IReadOnlyDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada>> LerRegrasAsync(
        IReadOnlyList<(string Codigo, string Versao)> citadas, CancellationToken cancellationToken) =>
        citadas.Count == 0
            ? new Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada>()
            : (await regras.ListarPorReferenciasAsync(citadas, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static regra => (regra.Codigo, regra.Versao), static regra => new RegraDoCatalogoCitada(regra.Tipo, regra.Referencia(), regra.DeclaracaoDaRegra));

    // O vocabulário só é lido quando o documento cita o desempate por condição sobre dado; sem a citação, fica vazio.
    private async Task<IReadOnlyDictionary<string, DescritorFatoCandidato>> DadosDoDesempateAsync(
        IReadOnlyList<(string Codigo, string Versao)> citadas, CancellationToken cancellationToken) =>
        citadas.Any(static regra => regra.Codigo == RegraDeDesempate.CondicaoSobreDado.Codigo())
            ? (await FatosDoDesempate.LerAsync(dadosDoCandidato, cancellationToken).ConfigureAwait(false)).Vocabulario
            : new Dictionary<string, DescritorFatoCandidato>();

    private async Task<HashSet<string>> TiposDeAtoAsync(IReadOnlyList<string> codigos, DateOnly dia, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? []
            : [.. (await tiposDeAto.ListarVigentesAsync(codigos, dia, cancellationToken).ConfigureAwait(false)).Select(static tipo => tipo.Codigo)];
}
