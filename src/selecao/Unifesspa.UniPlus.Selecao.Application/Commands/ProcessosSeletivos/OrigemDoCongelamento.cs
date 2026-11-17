namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ProcessosSeletivos.Publicacao;
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
    /// <summary>
    /// O momento da operação, como o documento do edital e o ato o citam ao serem recusados: "na publicação", "na
    /// retificação" ou "no fechamento da retificação".
    /// </summary>
    public abstract string DescricaoDaOperacao { get; }

    /// <summary>
    /// O que impede a operação pelo estado do processo, antes de qualquer leitura e de qualquer conferência da configuração:
    /// é conflito de estado, e a recusa impede o congelamento. Fica fora da ordem das famílias de regra da configuração.
    /// </summary>
    public abstract Result ConferirEstado(ProcessoSeletivo processo);

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
    /// Os fatos que as versões publicadas já coletaram, no ponto em que a cópia refeita dos dados do item precisa deles; nulo quando não há
    /// versão publicada. A retificação sem sessão os lê das versões; o fechamento os tem no rascunho da sessão.
    /// </summary>
    public abstract Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken);

    /// <summary>
    /// O item da configuração que chegou com o eixo trocado sem ter passado pela escrita, na ordem de entrada; vazio quando não
    /// há. Recebe só os itens vigentes, os que o catálogo ainda deixa coletar, com os campos dos grupos.
    /// </summary>
    public abstract IReadOnlyList<DomainError> ItensComEixoTrocado(IReadOnlyList<FatoColetado> itensVigentes);

    /// <summary>
    /// O que só a operação recusa depois de a configuração estar conferida, e que se acumula às causas dela; sem recusa
    /// própria, o neutro é a lista vazia. <paramref name="instante"/> é o único da operação.
    /// </summary>
    public virtual Task<IReadOnlyList<FieldError>> RecusasDaOperacaoAsync(
        LeituraDaBaseDaRetificacao leitura, ProcessoSeletivo processo, DateTimeOffset instante, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FieldError>>([]);
}

/// <summary>A publicação de abertura: nenhuma versão a precede.</summary>
internal sealed record PrimeiraPublicacao : OrigemDoCongelamento
{
    public override string DescricaoDaOperacao => "na publicação";

    public override Result ConferirEstado(ProcessoSeletivo processo) =>
        TransicoesDaVersao.ConferirEstadoParaPublicar(EstadoDaConfiguracao.De(processo));

    public override RetificacaoDoEdital? Retificacao => null;

    public override CongeladoNaVersaoVigente? CongeladoNaVersaoVigente => null;

    public override Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> TiposJaCongelados() =>
        Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>>.Success(new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal));

    public override IReadOnlyList<AnexoDoEdital> AnexosVigentes(bool herdaOsDaVersao) => [];

    public override Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken) =>
        Task.FromResult(Result<FatosDasVersoesPublicadas?>.Success(null));

    public override IReadOnlyList<DomainError> ItensComEixoTrocado(IReadOnlyList<FatoColetado> itensVigentes) => [];
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
    public override string DescricaoDaOperacao => "na retificação";

    public override Result ConferirEstado(ProcessoSeletivo processo) =>
        TransicoesDaVersao.ConferirEstadoParaRetificar(EstadoDaConfiguracao.De(processo), Base.VersaoVigente, Base.Motivo);

    public override Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken) =>
        versoes.LerAsync(processo.Id, cancellationToken);

    public override IReadOnlyList<DomainError> ItensComEixoTrocado(IReadOnlyList<FatoColetado> itensVigentes) => [];
}

/// <summary>O fechamento da sessão editorial: os fatos já coletados foram guardados no rascunho, na abertura.</summary>
internal sealed record FechamentoDaSessao(BaseDaRetificacao Base, FatosDasVersoesPublicadas Publicadas) : OrigemRetificadora(Base)
{
    public override string DescricaoDaOperacao => "no fechamento da retificação";

    public override Result ConferirEstado(ProcessoSeletivo processo) =>
        TransicoesDaVersao.ConferirEstadoParaFechar(EstadoDaConfiguracao.De(processo), Base.VersaoVigente, processo.Rascunho!);

    // A fase pode ter começado depois da edição do cronograma: a unidade trocada e a etapa removida são conferidas
    // contra o que a versão publicada tem no instante do fechamento.
    public override Task<IReadOnlyList<FieldError>> RecusasDaOperacaoAsync(
        LeituraDaBaseDaRetificacao leitura, ProcessoSeletivo processo, DateTimeOffset instante, CancellationToken cancellationToken) =>
        leitura.RecusasPorFaseIniciadaAsync(processo, instante, cancellationToken);

    public override Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken) =>
        Task.FromResult(Result<FatosDasVersoesPublicadas?>.Success(Publicadas));

    // Defesa para o item que chegou à configuração sem passar pela escrita, que já o conferiu.
    public override IReadOnlyList<DomainError> ItensComEixoTrocado(IReadOnlyList<FatoColetado> itensVigentes) =>
        [.. Publicadas.ItensComEixoTrocado(itensVigentes, []).Select(static troca => troca.ParaErro())];
}
