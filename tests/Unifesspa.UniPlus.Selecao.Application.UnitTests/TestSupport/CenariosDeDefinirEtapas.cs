namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class CenariosDeDefinirEtapas
{
    internal static readonly Guid TipoProvaObjetivaOrigemId = new("019fee1e-7000-7000-8000-000000000001");
    internal static readonly Guid TipoRedacaoOrigemId = new("019fee1e-7000-7000-8000-000000000002");

    /// <summary>Resolve os dois tipos usados nos testes dos cenários de DefinirEtapas — Prova Objetiva e Redação.</summary>
    internal static ITipoEtapaReader ReaderPadrao()
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", null, true, true, false, false, []));
        reader.ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoRedacaoOrigemId, "REDACAO", "Redação", null, true, true, false, false, []));
        return reader;
    }

    internal static TipoEtapaSnapshot TipoEtapaProvaObjetiva() =>
        TipoEtapaSnapshot.Criar(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    /// <summary>
    /// Invoca o handler preenchendo com dublê inerte toda dependência que o cenário não
    /// descreve. Sem isto, cada caso repete a lista inteira de colaboradores, o dublê inerte
    /// fica soterrado no que o teste quer dizer, e acrescentar uma dependência ao handler
    /// obriga a reescrever todos os casos do arquivo de uma vez.
    /// </summary>
    /// <remarks>
    /// O leitor de tipos de etapa cai no <see cref="ReaderPadrao"/>, que resolve os dois tipos
    /// usados aqui: um dublê inerte devolveria tipo inativo e toda etapa seria recusada antes
    /// de o cenário começar. Os demais ficam inertes de propósito — um dublê de
    /// <see cref="ITipoAtoPublicadoReader"/> não resolve ato nenhum, então o caso que declara
    /// produtos precisa stubar o catálogo e passá-lo aqui, sob pena de recusa por ato fora do
    /// catálogo em vez do que ele pretendia exercitar.
    /// </remarks>
    internal static Task<Result> Executar(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository repository,
        IUnitOfWork unitOfWork,
        ITipoEtapaReader? tipoEtapaReader = null,
        ITipoBancaReader? tipoBancaReader = null,
        IRegraCatalogoReader? regraCatalogoReader = null,
        ITipoAtoPublicadoReader? tipoAtoPublicadoReader = null,
        IRegistroCodecsEnvelope? registroCodecs = null) =>
        DefinirEtapasCommandHandler.Handle(
            command,
            repository,
            new DefinicaoDasEtapas(
                tipoEtapaReader ?? ReaderPadrao(),
                tipoBancaReader ?? Substitute.For<ITipoBancaReader>(),
                regraCatalogoReader ?? Substitute.For<IRegraCatalogoReader>(),
                tipoAtoPublicadoReader ?? Substitute.For<ITipoAtoPublicadoReader>(),
                repository,
                registroCodecs ?? Substitute.For<IRegistroCodecsEnvelope>(),
                new MotivosDosLugaresQueSaem(MotivoDeTeste.SemEscolha())),
            unitOfWork,
            TimeProvider.System,
            CancellationToken.None);

    /// <summary>A definição das etapas sobre o repositório e a escolha de motivos dados, com os cadastros que resolvem os dois tipos de etapa.</summary>
    internal static DefinicaoDasEtapas DefinicaoDasEtapasComMotivos(IProcessoSeletivoRepository repository, IMotivoDoProcessoRepository motivos) => new(
        ReaderPadrao(),
        Substitute.For<ITipoBancaReader>(),
        Substitute.For<IRegraCatalogoReader>(),
        Substitute.For<ITipoAtoPublicadoReader>(),
        repository,
        Substitute.For<IRegistroCodecsEnvelope>(),
        new MotivosDosLugaresQueSaem(motivos));

    /// <summary>Certame recém-criado, ainda sem etapa alguma.</summary>
    internal static ProcessoSeletivo ProcessoSemEtapas() => ProcessoSeletivo.Criar(
        "PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    /// <summary>A etapa classificatória de prova objetiva na ordem 1, de peso 1 e sem faixa de nota.</summary>
    internal static EtapaProcesso EtapaDeProvaObjetiva(string nome) => EtapaProcesso.Criar(
        new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    /// <summary>Certame recém-criado que já tem <paramref name="etapa"/> como única etapa.</summary>
    internal static ProcessoSeletivo ProcessoComAEtapa(EtapaProcesso etapa)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        processo.DefinirEtapas([etapa]);

        return processo;
    }

    /// <summary>Repositório que devolve <paramref name="processo"/> para a mutação.</summary>
    internal static IProcessoSeletivoRepository RepositorioCom(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return repository;
    }

    /// <summary>Cadastro em que nenhum tipo de etapa existe ativo.</summary>
    internal static ITipoEtapaReader ReaderSemTipoAtivo()
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);

        return reader;
    }

    /// <summary>
    /// A recusa da substituição das etapas não grava nada e descarta o rastreamento, como toda recusa da substituição:
    /// o SaveChangesAsync automático do Wolverine persistiria qualquer mutação parcial que o rastreamento ainda guardasse.
    /// </summary>
    internal static async Task RecusouSemGravarAsync(Result result, IUnitOfWork unitOfWork, string codigo)
    {
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(codigo);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    /// <summary>Entrada de catálogo de um tipo de ato que divulga resultado.</summary>
    internal static TipoAtoPublicadoView AtoDeResultado(string codigo) => new(
        codigo, $"Ato de {codigo}", CongelaConfiguracao: false, UnicoPorObjeto: false,
        EfeitoIrreversivel: false, EhResultado: true);
}
