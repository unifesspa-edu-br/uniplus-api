namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Os tipos de ato que a edição do cronograma e das etapas consulta, na vigência de uma única leitura do
/// relógio: todo produto e toda âncora da coleção resolvem contra o mesmo instante (ADR-0068). O tipo que
/// a versão publicada já declara vale pela classificação congelada nela; o catálogo de hoje só vale para
/// o tipo declarado pela primeira vez.
/// </summary>
internal sealed class AtosConsultadosNaEdicao
{
    private readonly LeituraMemoizada<string, TipoAtoPublicadoView> _tiposVigentes;

    public AtosConsultadosNaEdicao(
        ITipoAtoPublicadoReader tipoAtoPublicadoReader,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        Guid processoSeletivoId,
        DateTimeOffset agora)
    {
        DateOnly hoje = DateOnly.FromDateTime(agora.UtcDateTime);
        _tiposVigentes = new LeituraMemoizada<string, TipoAtoPublicadoView>(
            (codigo, cancellationToken) => tipoAtoPublicadoReader.ObterVigenteAsync(codigo, hoje, cancellationToken),
            StringComparer.Ordinal);
        Congelados = new ClassificacaoDosTiposNaEdicao(processoSeletivoRepository, registroCodecs, processoSeletivoId);
    }

    public AtosConsultadosNaEdicao(
        ITipoAtoPublicadoReader tipoAtoPublicadoReader,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        Guid processoSeletivoId,
        TimeProvider timeProvider)
        : this(tipoAtoPublicadoReader, processoSeletivoRepository, registroCodecs, processoSeletivoId, timeProvider.GetUtcNow())
    {
    }

    /// <summary>A classificação congelada na versão publicada e as âncoras que ela já tem.</summary>
    public ClassificacaoDosTiposNaEdicao Congelados { get; }

    /// <summary>O tipo de ato com versão vigente no catálogo; <c>null</c> quando não há.</summary>
    public Task<TipoAtoPublicadoView?> TipoVigenteAsync(string atoCodigo, CancellationToken cancellationToken) =>
        _tiposVigentes.ObterAsync(atoCodigo, cancellationToken);

    /// <summary>
    /// A classificação do tipo de ato: a congelada na versão publicada ou, para o tipo novo, a do catálogo
    /// vigente. Falha quando a versão publicada não é legível.
    /// </summary>
    public async Task<Result<ClassificacaoDoAtoConsultado>> ClassificacaoAsync(string atoCodigo, CancellationToken cancellationToken)
    {
        Result<ClassificacaoDoTipoDeAto?> congelada = await Congelados
            .DoTipoJaDeclaradoAsync(atoCodigo, cancellationToken).ConfigureAwait(false);
        if (congelada.IsFailure)
        {
            return Result<ClassificacaoDoAtoConsultado>.ValidationFailure(congelada.Errors);
        }

        return congelada.Value is { } daVersaoPublicada
            ? Result<ClassificacaoDoAtoConsultado>.Success(new ClassificacaoDoAtoConsultado.Conhecida(daVersaoPublicada))
            : await ClassificacaoDoCatalogoAsync(atoCodigo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ClassificacaoDoAtoConsultado>> ClassificacaoDoCatalogoAsync(string atoCodigo, CancellationToken cancellationToken)
    {
        TipoAtoPublicadoView? vigente = await TipoVigenteAsync(atoCodigo, cancellationToken).ConfigureAwait(false);
        return Result<ClassificacaoDoAtoConsultado>.Success(vigente is null
            ? new ClassificacaoDoAtoConsultado.SemVersaoVigente()
            : new ClassificacaoDoAtoConsultado.Conhecida(ResolucaoDosTiposDeAtoUnicos.ClassificacaoDe(vigente)));
    }
}
