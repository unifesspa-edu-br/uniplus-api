namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Os cadastros e as portas que o handler das etapas consulta, com dublê inerte para tudo o que o teste não
/// descreve. O leitor de tipos de etapa resolve os dois tipos usados nos testes, Prova Objetiva e Redação: um
/// dublê inerte devolveria tipo inativo e toda etapa seria recusada antes de o cenário começar. A chamada ao
/// handler fica toda aqui, de modo que uma mudança de assinatura dele se faz num lugar só.
/// </summary>
internal sealed class CenarioDasEtapas(IProcessoSeletivoRepository repository, ISelecaoUnitOfWork unitOfWork)
{
    public static readonly Guid TipoProvaObjetivaOrigemId = new("019fee1e-7000-7000-8000-000000000001");
    public static readonly Guid TipoRedacaoOrigemId = new("019fee1e-7000-7000-8000-000000000002");

    public IProcessoSeletivoRepository Repositorio { get; } = repository;

    public ISelecaoUnitOfWork UnitOfWork { get; } = unitOfWork;

    public ITipoEtapaReader TiposDeEtapa { get; set; } = ReaderPadrao();

    public ITipoBancaReader TiposDeBanca { get; set; } = Substitute.For<ITipoBancaReader>();

    public IRegraCatalogoReader Regras { get; set; } = Substitute.For<IRegraCatalogoReader>();

    public ITipoAtoPublicadoReader TiposDeAto { get; set; } = Substitute.For<ITipoAtoPublicadoReader>();

    public IRegistroCodecsEnvelope Codecs { get; set; } = Substitute.For<IRegistroCodecsEnvelope>();

    public IFasesIniciadasDoProcesso FasesIniciadas { get; set; } = FasesIniciadasDeTeste.Nenhuma();

    public TimeProvider Relogio { get; set; } = TimeProvider.System;

    /// <summary>
    /// O cenário de um processo que o repositório devolve, com o que gravar descrito num dublê novo. O cadastro que
    /// o caso não informa fica com o dublê inerte, e o de tipos de etapa com os dois tipos usados nos testes.
    /// </summary>
    public static CenarioDasEtapas Para(
        ProcessoSeletivo processo,
        ITipoAtoPublicadoReader? tiposDeAto = null,
        IRegraCatalogoReader? regras = null,
        ITipoEtapaReader? tiposDeEtapa = null)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        CenarioDasEtapas cenario = new(repositorio, Substitute.For<ISelecaoUnitOfWork>());
        cenario.TiposDeAto = tiposDeAto ?? cenario.TiposDeAto;
        cenario.Regras = regras ?? cenario.Regras;
        cenario.TiposDeEtapa = tiposDeEtapa ?? cenario.TiposDeEtapa;
        return cenario;
    }

    /// <summary>Resolve os dois tipos usados nos testes — Prova Objetiva e Redação.</summary>
    public static ITipoEtapaReader ReaderPadrao()
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", null, true, true, false));
        reader.ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoRedacaoOrigemId, "REDACAO", "Redação", null, true, true, false));
        return reader;
    }

    public Task<Result<MutacaoAceita>> DefinirAsync(DefinirEtapasCommand command) =>
        DefinirEtapasCommandHandler.Handle(command, Repositorio, Definicao(), UnitOfWork, Relogio, CancellationToken.None);

    private DefinicaoDasEtapas Definicao() => new(
        TiposDeEtapa,
        TiposDeBanca,
        Regras,
        new SubstituicaoDasEtapas(TiposDeAto, Repositorio, Codecs, new LeituraDasFasesIniciadasNaEdicao(FasesIniciadas), UnitOfWork));
}
