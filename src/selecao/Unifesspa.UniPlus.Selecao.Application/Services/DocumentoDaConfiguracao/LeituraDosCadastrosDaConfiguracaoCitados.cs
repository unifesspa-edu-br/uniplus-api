namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os cadastros do módulo Configuração que o documento cita, cada um pelo código: só o que o cadastro encontrou vivo, de modo que
/// o código ausente é o que o cadastro não tem. O tipo de etapa e o quadro de pesos que o domínio recusa copiar também ficam de fora.
/// </summary>
internal sealed record CadastrosDaConfiguracaoCitados(
    IReadOnlyDictionary<string, TipoProcessoSnapshot> TiposDeProcesso,
    IReadOnlyDictionary<string, OrigemCanonicaDaFase> FasesCanonicas,
    IReadOnlyDictionary<string, TipoDeBancaCopiado> TiposDeBanca,
    IReadOnlyDictionary<string, CategoriaDeDocumentoCitada> CategoriasDeDocumento,
    IReadOnlyDictionary<string, TipoEtapaSnapshot> TiposDeEtapa,
    IReadOnlyDictionary<string, QuadroDePesosCitado> QuadrosDePesos);

/// <summary>
/// Lê os cadastros de Configuração que o documento cita: o tipo de processo, a fase canônica, o tipo de banca, o tipo de etapa, a
/// categoria de documento e o quadro de pesos por área do ENEM da resolução. Cada código distinto vai uma vez ao cadastro, os que têm consulta em lote numa só chamada, e o cadastro de que o
/// documento não cita código não é consultado. Cada leitura é do cadastro vivo, e quem a congela é a leitura do documento.
/// </summary>
public sealed class LeituraDosCadastrosDaConfiguracaoCitados(
    ITipoProcessoReader tiposDeProcesso,
    IFaseCanonicaReader fasesCanonicas,
    ITipoBancaReader tiposDeBanca,
    ICategoriaDocumentoReader categoriasDeDocumento,
    ITipoEtapaReader tiposDeEtapa,
    IPesoAreaEnemReader pesosPorArea)
{
    internal async Task<Result<CadastrosDaConfiguracaoCitados>> LerAsync(CodigosCitadosPorCadastro codigos, CancellationToken cancellationToken)
    {
        Result<IReadOnlyDictionary<string, TipoProcessoSnapshot>> tipo = await TipoDeProcessoAsync(codigos.TipoDeProcesso, cancellationToken).ConfigureAwait(false);

        return tipo.IsFailure
            ? Result<CadastrosDaConfiguracaoCitados>.Failure(tipo.Error!)
            : Result<CadastrosDaConfiguracaoCitados>.Success(new(
                tipo.Value!,
                await FasesCanonicasAsync(codigos.FasesCanonicas, cancellationToken).ConfigureAwait(false),
                await TiposDeBancaAsync(codigos.TiposDeBanca, cancellationToken).ConfigureAwait(false),
                await CategoriasAsync(codigos.CategoriasDeDocumento, cancellationToken).ConfigureAwait(false),
                await TiposDeEtapaAsync(codigos.TiposDeEtapa, cancellationToken).ConfigureAwait(false),
                await QuadrosDePesosAsync(codigos.ResolucaoDePesos, cancellationToken).ConfigureAwait(false)));
    }

    private async Task<Result<IReadOnlyDictionary<string, TipoProcessoSnapshot>>> TipoDeProcessoAsync(string? codigo, CancellationToken cancellationToken)
    {
        TipoProcessoView? tipo = codigo is null ? null : await tiposDeProcesso.ObterAtivoPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result<IReadOnlyDictionary<string, TipoProcessoSnapshot>>.Success(new Dictionary<string, TipoProcessoSnapshot>());
        }

        Result<TipoProcessoSnapshot> snapshot = TipoProcessoSnapshot.Criar(tipo.Id, tipo.Codigo, tipo.Nome);

        return snapshot.IsFailure
            ? Result<IReadOnlyDictionary<string, TipoProcessoSnapshot>>.Failure(snapshot.Error!)
            : Result<IReadOnlyDictionary<string, TipoProcessoSnapshot>>.Success(new Dictionary<string, TipoProcessoSnapshot> { [codigo!] = snapshot.Value! });
    }

    private async Task<IReadOnlyDictionary<string, OrigemCanonicaDaFase>> FasesCanonicasAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? new Dictionary<string, OrigemCanonicaDaFase>()
            : (await fasesCanonicas.ListarVivasPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static fase => fase.Codigo, OrigemCanonicaDaFaseDoCadastro.De, StringComparer.Ordinal);

    private async Task<IReadOnlyDictionary<string, TipoDeBancaCopiado>> TiposDeBancaAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? new Dictionary<string, TipoDeBancaCopiado>()
            : (await tiposDeBanca.ListarVivosPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static tipo => tipo.Codigo.Codigo(), static tipo => CopiaDoTipoDeBanca.De(tipo), StringComparer.Ordinal);

    private async Task<IReadOnlyDictionary<string, CategoriaDeDocumentoCitada>> CategoriasAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? new Dictionary<string, CategoriaDeDocumentoCitada>()
            : (await categoriasDeDocumento.ListarVivasPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static categoria => categoria.Codigo, static categoria => new CategoriaDeDocumentoCitada(categoria.Id, categoria.Codigo, categoria.Nome), StringComparer.Ordinal);

    private async Task<IReadOnlyDictionary<string, TipoEtapaSnapshot>> TiposDeEtapaAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? new Dictionary<string, TipoEtapaSnapshot>()
            : (await tiposDeEtapa.ListarAtivosPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false))
                .Select(static tipo => (tipo.Codigo, Copia: CopiaDoTipoDeEtapa.De(tipo)))
                .Where(static tipo => tipo.Copia.IsSuccess)
                .ToDictionary(static tipo => tipo.Codigo, static tipo => tipo.Copia.Value!, StringComparer.Ordinal);

    // O quadro fica pela resolução como o documento a escreveu: o cadastro a compara pela forma em que a grava.
    private async Task<IReadOnlyDictionary<string, QuadroDePesosCitado>> QuadrosDePesosAsync(
        string? resolucao, CancellationToken cancellationToken)
    {
        ResolucaoPesoAreaEnemView? cadastrada = resolucao is null
            ? null
            : await pesosPorArea.ObterPorResolucaoAsync(resolucao, cancellationToken).ConfigureAwait(false);
        Result<QuadroDePesosCitado>? quadro = cadastrada is null ? null : CopiaDoQuadroDePesos.De(cadastrada);

        return quadro is { IsSuccess: true }
            ? new Dictionary<string, QuadroDePesosCitado>(StringComparer.Ordinal) { [resolucao!] = quadro.Value! }
            : new Dictionary<string, QuadroDePesosCitado>(StringComparer.Ordinal);
    }
}
