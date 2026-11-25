namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Kernel.Results;

using Queries.ProcessosSeletivos;

using Services;

/// <summary>
/// De onde a versão que nasce parte: a primeira publicação, que não tem versão anterior, ou uma retificação, que
/// parte da versão vigente. A conferência do congelamento é a mesma para as duas operações; o que muda vem daqui,
/// e a primeira publicação responde com o neutro de cada pergunta (sem retificação, sem fatos já coletados, sem
/// tipo de ato já congelado, sem anexo vigente).
/// </summary>
internal abstract record OrigemDoCongelamento
{
    /// <summary>
    /// O momento da operação, como o documento do edital e o ato o citam ao serem recusados: "na publicação", "na
    /// retificação".
    /// </summary>
    public abstract string DescricaoDaOperacao { get; }

    /// <summary>
    /// O que impede a operação pelo estado do processo, antes de qualquer leitura e de qualquer conferência da configuração:
    /// é conflito de estado, e a recusa impede o congelamento. Fica fora da ordem dos assuntos de regra da configuração.
    /// </summary>
    public abstract Result ConferirEstado(ProcessoSeletivo processo);

    /// <summary>
    /// O instante único da operação, lido depois da trava do processo: o do relógio na primeira publicação, e, na
    /// retificação, o maior entre o relógio e a vigência da versão que ela sucede.
    /// </summary>
    public abstract Result<InstanteDoComando> LerOInstante(LeituraDoInstanteDoComando instantes);

    /// <summary>O ato que a versão emenda e o motivo; nulo quando a versão não retifica nada.</summary>
    public abstract RetificacaoDoEdital? Retificacao { get; }

    /// <summary>O número da versão que nasce: o da abertura na primeira publicação, ou o que sucede a versão vigente.</summary>
    public abstract int NumeroDaVersaoQueNasce { get; }

    /// <summary>O que a versão vigente congelou das exigências e dos fatos derivados; nulo sem versão vigente.</summary>
    public abstract CongeladoNaVersaoVigente? CongeladoNaVersaoVigente { get; }

    /// <summary>A classificação dos tipos de ato que a versão vigente já congelou, ou a falha do codec ao lê-la.</summary>
    public abstract Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> TiposJaCongelados();

    /// <summary>As fases do cronograma da versão vigente, reidratadas do registro; nulo quando não há versão publicada.</summary>
    /// <exception cref="InvalidOperationException">A versão vigente não foi lida: a conferência já a recusou antes de chegar aqui.</exception>
    public abstract IReadOnlyCollection<FaseCronograma>? FasesDaVersaoPublicada();

    /// <summary>
    /// Os anexos da versão vigente; ao pedido sem lista (<paramref name="herdaOsDaVersao"/>), são eles que a versão
    /// nova herda.
    /// </summary>
    public abstract IReadOnlyList<AnexoDoEdital> AnexosVigentes(bool herdaOsDaVersao);

    /// <summary>
    /// Os fatos que as versões publicadas já coletaram, no ponto em que a cópia refeita dos dados do item precisa deles; nulo quando não há
    /// versão publicada. A retificação os lê das versões publicadas.
    /// </summary>
    public abstract Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken);
}

/// <summary>A publicação de abertura: nenhuma versão a precede.</summary>
internal sealed record PrimeiraPublicacao : OrigemDoCongelamento
{
    public override string DescricaoDaOperacao => "na publicação";

    public override Result ConferirEstado(ProcessoSeletivo processo) =>
        TransicoesDaVersao.ConferirEstadoParaPublicar(processo.Situacao);

    public override Result<InstanteDoComando> LerOInstante(LeituraDoInstanteDoComando instantes) =>
        instantes.Ler();

    public override RetificacaoDoEdital? Retificacao => null;

    public override int NumeroDaVersaoQueNasce => VersaoConfiguracao.NumeroDaAbertura;

    public override CongeladoNaVersaoVigente? CongeladoNaVersaoVigente => null;

    public override Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> TiposJaCongelados() =>
        Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>>.Success(new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal));

    public override IReadOnlyCollection<FaseCronograma>? FasesDaVersaoPublicada() => null;

    public override IReadOnlyList<AnexoDoEdital> AnexosVigentes(bool herdaOsDaVersao) => [];

    public override Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken) =>
        Task.FromResult(Result<FatosDasVersoesPublicadas?>.Success(null));
}

/// <summary>A versão vigente sobre a qual uma retificação parte, o motivo dela e o que o codec leu da versão.</summary>
internal sealed record BaseDaRetificacao(
    VersaoConfiguracao VersaoVigente, string Motivo, Result<EnvelopeReidratado> VersaoReidratada);

/// <summary>A retificação de ato único: parte da versão vigente, e os fatos já coletados saem das versões publicadas.</summary>
internal sealed record RetificacaoDeAtoUnico(BaseDaRetificacao Base) : OrigemDoCongelamento
{
    public override string DescricaoDaOperacao => "na retificação";

    public override Result ConferirEstado(ProcessoSeletivo processo) =>
        TransicoesDaVersao.ConferirEstadoParaRetificar(processo.Id, processo.Situacao, Base.VersaoVigente, Base.Motivo);

    public override Result<InstanteDoComando> LerOInstante(LeituraDoInstanteDoComando instantes) =>
        instantes.LerNaSucessaoDe(Base.VersaoVigente);

    public override RetificacaoDoEdital? Retificacao => new(Base.VersaoVigente.AtoCriadorId, Base.Motivo);

    public override int NumeroDaVersaoQueNasce => VersaoConfiguracao.NumeroDaSucessora(Base.VersaoVigente.NumeroVersao);

    public override CongeladoNaVersaoVigente? CongeladoNaVersaoVigente =>
        ProtecaoVigenteDasExigenciasDoProcesso.DoEnvelope(Base.VersaoReidratada);

    public override Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> TiposJaCongelados() =>
        CongelamentoDosTiposDeAto.DoEnvelope(Base.VersaoReidratada);

    public override IReadOnlyCollection<FaseCronograma>? FasesDaVersaoPublicada() =>
        Base.VersaoReidratada.IsSuccess
            ? Base.VersaoReidratada.Value!.Configuracao.Cronograma.CronogramaFases
            : throw new InvalidOperationException("A versão vigente não foi lida, e a conferência da precedência não tem com o que comparar o cronograma.");

    public override IReadOnlyList<AnexoDoEdital> AnexosVigentes(bool herdaOsDaVersao) =>
        LeituraDoCertameEdital.AnexosDaVersao(Base.VersaoVigente, herdaOsDaVersao);

    public override Task<Result<FatosDasVersoesPublicadas?>> FatosJaPublicadosAsync(
        LeituraDasVersoesPublicadas versoes, ProcessoSeletivo processo, CancellationToken cancellationToken) =>
        versoes.LerAsync(processo.Id, cancellationToken);
}
