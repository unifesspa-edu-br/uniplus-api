namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A classificação que a edição do cronograma e das etapas confere para um tipo de ato: a congelada na
/// versão publicada, quando o tipo já foi declarado, e a do catálogo de hoje só para o tipo novo.
/// </summary>
/// <remarks>
/// É a mesma regra da retificação e do seu fechamento. Sem ela, mudar o catálogo depois da publicação
/// faria a edição recusar o que o fechamento da sessão aceita, e o processo publicado deixaria de poder
/// ser retificado. A versão é lida na primeira consulta, e não antes: a edição que não toca produto não
/// depende dela. Uma versão que o codec não lê é recusada com a falha do codec, nunca resolvida pelo
/// catálogo de hoje. Um processo sem versão publicada não tem o que congelar, e todo tipo é novo.
/// </remarks>
internal sealed class ClassificacaoDosTiposNaEdicao(
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRegistroCodecsEnvelope registroCodecs,
    Guid processoSeletivoId)
{
    private Result<EnvelopeReidratado?>? _versaoPublicada;

    /// <summary>
    /// A classificação congelada do tipo, ou <c>null</c> quando o tipo não consta na versão publicada
    /// e a classificação vale pelo catálogo.
    /// </summary>
    public async Task<Result<ClassificacaoDoTipoDeAto?>> DoTipoJaDeclaradoAsync(string codigo, CancellationToken cancellationToken)
    {
        Result<EnvelopeReidratado?> versao = await LerAsync(cancellationToken).ConfigureAwait(false);
        if (versao.IsFailure)
        {
            return Result<ClassificacaoDoTipoDeAto?>.Failure(versao.Error!);
        }

        if (versao.Value is not { } envelope)
        {
            return Result<ClassificacaoDoTipoDeAto?>.Success(null);
        }

        IReadOnlyDictionary<string, TipoDeAtoCongelado> congelados = CongelamentoDosTiposDeAto
            .DoEnvelope(Result<EnvelopeReidratado>.Success(envelope)).Value!;
        return Result<ClassificacaoDoTipoDeAto?>.Success(
            congelados.TryGetValue(codigo, out TipoDeAtoCongelado? congelado) ? congelado.Classificacao : null);
    }

    /// <summary>
    /// Se a versão publicada já tem, na fase de código dado, a âncora de recurso no ato dado. A âncora que
    /// a versão já tem não é revista pelo que o catálogo diz hoje do ato.
    /// </summary>
    public async Task<Result<bool>> AncoraDaFaseJaPublicadaAsync(string faseCodigo, string atoCodigo, CancellationToken cancellationToken)
    {
        Result<EnvelopeReidratado?> versao = await LerAsync(cancellationToken).ConfigureAwait(false);
        if (versao.IsFailure)
        {
            return Result<bool>.Failure(versao.Error!);
        }

        bool jaPublicada = versao.Value?.Grafo.CronogramaFases.Any(fase =>
            string.Equals(fase.Codigo, faseCodigo, StringComparison.Ordinal)
            && fase.RegraRecurso is { } regra
            && fase.Produtos.Any(p => p.Id == regra.ProdutoAncoraId && string.Equals(p.AtoCodigo, atoCodigo, StringComparison.Ordinal))) ?? false;
        return Result<bool>.Success(jaPublicada);
    }

    /// <summary>Se a versão publicada já tem, na etapa de identidade dada, a âncora de recurso no ato dado.</summary>
    public async Task<Result<bool>> AncoraDaEtapaJaPublicadaAsync(Guid etapaId, string atoCodigo, CancellationToken cancellationToken)
    {
        Result<EnvelopeReidratado?> versao = await LerAsync(cancellationToken).ConfigureAwait(false);
        if (versao.IsFailure)
        {
            return Result<bool>.Failure(versao.Error!);
        }

        bool jaPublicada = versao.Value?.Grafo.Etapas.Any(etapa =>
            etapa.Id == etapaId
            && etapa.Recursos.Any(recurso => etapa.Produtos.Any(p =>
                p.Id == recurso.ProdutoAncoraId && string.Equals(p.AtoCodigo, atoCodigo, StringComparison.Ordinal)))) ?? false;
        return Result<bool>.Success(jaPublicada);
    }

    private async Task<Result<EnvelopeReidratado?>> LerAsync(CancellationToken cancellationToken)
    {
        if (_versaoPublicada is { } lida)
        {
            return lida;
        }

        VersaoConfiguracao? versaoAtual = await processoSeletivoRepository
            .ObterVersaoAtualAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (versaoAtual is null)
        {
            _versaoPublicada = Result<EnvelopeReidratado?>.Success(null);
        }
        else
        {
            Result<EnvelopeReidratado> reidratada = registroCodecs.Reidratar(versaoAtual);
            _versaoPublicada = reidratada.IsFailure
                ? Result<EnvelopeReidratado?>.Failure(reidratada.Error!)
                : Result<EnvelopeReidratado?>.Success(reidratada.Value);
        }

        return _versaoPublicada;
    }
}
