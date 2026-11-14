namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

using Queries.ProcessosSeletivos;

/// <summary>
/// De onde a versão que nasce parte: a primeira publicação, que não tem versão anterior, ou uma retificação, que
/// parte da versão vigente. A conferência do congelamento é a mesma para as três operações; o que muda vem daqui,
/// e a primeira publicação responde com o neutro de cada pergunta (sem retificação, sem fatos já coletados, sem
/// tipo de ato já congelado, sem anexo vigente).
/// </summary>
internal abstract record OrigemDoCongelamento
{
    /// <summary>O ato que a versão emenda e o motivo; nulo quando a versão não retifica nada.</summary>
    public abstract RetificacaoDoEdital? Retificacao { get; }

    /// <summary>O que a versão vigente congelou das exigências e dos fatos derivados; nulo sem versão vigente.</summary>
    public abstract CongeladoNaVersaoVigente? CongeladoNaVersaoVigente { get; }

    /// <summary>A classificação dos tipos de ato que a versão vigente já congelou, ou a falha do codec ao lê-la.</summary>
    public abstract Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> TiposJaCongelados();

    /// <summary>
    /// Os anexos da versão vigente; ao pedido sem lista (<paramref name="herdaOsDaVersao"/>), são eles que a versão
    /// nova herda.
    /// </summary>
    public abstract IReadOnlyList<AnexoDoEdital> AnexosVigentes(bool herdaOsDaVersao);

    /// <summary>
    /// Os fatos que as versões publicadas já coletaram, no ponto em que a recópia precisa deles; nulo quando não há
    /// versão publicada. A retificação sem sessão os lê das versões; o fechamento os tem no rascunho da sessão.
    /// </summary>
    public abstract Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken);

    /// <summary>O item da configuração que chegou com o eixo trocado sem ter passado pela escrita; nulo quando não há.</summary>
    public abstract DomainError? ItemComEixoTrocado(ProcessoSeletivo processo);
}

/// <summary>A publicação de abertura: nenhuma versão a precede.</summary>
internal sealed record PrimeiraPublicacao : OrigemDoCongelamento
{
    public override RetificacaoDoEdital? Retificacao => null;

    public override CongeladoNaVersaoVigente? CongeladoNaVersaoVigente => null;

    public override Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> TiposJaCongelados() =>
        Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>>.Success(new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal));

    public override IReadOnlyList<AnexoDoEdital> AnexosVigentes(bool herdaOsDaVersao) => [];

    public override Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken) =>
        Task.FromResult(Result<FatosDasVersoesPublicadas?>.Success(null));

    public override DomainError? ItemComEixoTrocado(ProcessoSeletivo processo) => null;
}

/// <summary>A versão vigente sobre a qual uma retificação parte, o motivo dela e o que o codec leu da versão.</summary>
internal sealed record BaseDaRetificacao(
    VersaoConfiguracao VersaoVigente, string Motivo, Result<EnvelopeReidratado> VersaoReidratada);

/// <summary>O que as duas formas de retificar têm em comum: a base lida da cadeia de versões.</summary>
internal abstract record OrigemRetificadora(BaseDaRetificacao Base) : OrigemDoCongelamento
{
    public override RetificacaoDoEdital? Retificacao => new(Base.VersaoVigente.AtoCriadorId, Base.Motivo);

    public override CongeladoNaVersaoVigente? CongeladoNaVersaoVigente =>
        ProtecaoVigenteDasExigenciasDoProcesso.DoEnvelope(Base.VersaoReidratada);

    public override Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> TiposJaCongelados() =>
        CongelamentoDosTiposDeAto.DoEnvelope(Base.VersaoReidratada);

    public override IReadOnlyList<AnexoDoEdital> AnexosVigentes(bool herdaOsDaVersao) =>
        LeituraDoCertameEdital.AnexosDaVersao(Base.VersaoVigente, herdaOsDaVersao);
}

/// <summary>A retificação de ato único, sem sessão editorial: os fatos já coletados saem das versões publicadas.</summary>
internal sealed record RetificacaoDeAtoUnico(BaseDaRetificacao Base) : OrigemRetificadora(Base)
{
    public override Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken) =>
        versoes.LerAsync(processo.Id, cancellationToken);

    public override DomainError? ItemComEixoTrocado(ProcessoSeletivo processo) => null;
}

/// <summary>O fechamento da sessão editorial: os fatos já coletados foram guardados no rascunho, na abertura.</summary>
internal sealed record FechamentoDaSessao(BaseDaRetificacao Base, FatosDasVersoesPublicadas Publicadas) : OrigemRetificadora(Base)
{
    public override Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken) =>
        Task.FromResult(Result<FatosDasVersoesPublicadas?>.Success(Publicadas));

    // Defesa para o item que chegou à configuração sem passar pela escrita, que já o conferiu.
    public override DomainError? ItemComEixoTrocado(ProcessoSeletivo processo) =>
        Publicadas.PrimeiroItemComEixoTrocado(processo.FatosColetados, processo.GruposColetados)?.ParaErro();
}
