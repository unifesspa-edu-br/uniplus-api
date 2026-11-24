namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os cadastros e o registro de codecs que a definição das etapas consulta além do tipo de etapa. O que o
/// cenário não informa fica como dublê inerte.
/// </summary>
internal sealed record CadastrosDeApoioDeDefinirEtapas(
    ITipoBancaReader? TiposDeBanca = null,
    IRegraCatalogoReader? Regras = null,
    ITipoAtoPublicadoReader? Atos = null,
    IRegistroCodecsEnvelope? Codecs = null)
{
    /// <summary>A definição das etapas sobre o repositório dado, com dublê inerte no que o cenário não informou.</summary>
    internal DefinicaoDasEtapas Definicao(ITipoEtapaReader tipoEtapa, IProcessoSeletivoRepository repository) => new(
        tipoEtapa,
        TiposDeBanca ?? Substitute.For<ITipoBancaReader>(),
        Regras ?? Substitute.For<IRegraCatalogoReader>(),
        Atos ?? Substitute.For<ITipoAtoPublicadoReader>(),
        repository,
        Codecs ?? Substitute.For<IRegistroCodecsEnvelope>(),
        new MotivosDosLugaresQueSaem(MotivoDeTeste.SemEscolha()));
}

internal static class CenariosDeDefinirEtapas
{
    internal static readonly Guid TipoProvaObjetivaOrigemId = new("019fee1e-7000-7000-8000-000000000001");
    internal static readonly Guid TipoRedacaoOrigemId = new("019fee1e-7000-7000-8000-000000000002");

    /// <summary>Resolve os dois tipos usados nos testes dos cenários de DefinirEtapas — Prova Objetiva e Redação.</summary>
    internal static ITipoEtapaReader ReaderPadrao()
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(TipoEtapaViewDeTeste.Com(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA").Chamado("Prova Objetiva").Construir());
        reader.ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>())
            .Returns(TipoEtapaViewDeTeste.Com(TipoRedacaoOrigemId, "REDACAO").Chamado("Redação").Construir());
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
    internal static async Task<Result> Executar(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository repository,
        IUnitOfWork unitOfWork,
        ITipoEtapaReader? tipoEtapaReader = null,
        CadastrosDeApoioDeDefinirEtapas? cadastros = null)
    {
        Result resultado = await DefinirEtapasCommandHandler.Handle(
            command,
            repository,
            (cadastros ?? new CadastrosDeApoioDeDefinirEtapas()).Definicao(tipoEtapaReader ?? ReaderPadrao(), repository),
            unitOfWork,
            TimeProvider.System,
            CancellationToken.None);
        return resultado;
    }

    /// <summary>A definição das etapas sobre o repositório e a escolha de motivos dados, com os cadastros que resolvem os dois tipos de etapa.</summary>
    internal static DefinicaoDasEtapas DefinicaoDasEtapasComMotivos(IProcessoSeletivoRepository repository, IMotivoDeDecisaoDoProcessoRepository motivos) => new(
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
    internal static EtapaProcesso EtapaDeProvaObjetiva(string nome) => EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
        new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    /// <summary>A etapa que o operador envia no corpo da requisição, sem produtos, bancas nem recursos.</summary>
    internal static EtapaProcessoInput EtapaInput(
        string nome, CaraterEtapa carater, Guid tipoEtapaOrigemId, decimal? peso, decimal? notaMinima, int ordem, Guid? id = null) =>
        new(nome, carater.Codigo(), tipoEtapaOrigemId, peso, notaMinima, ordem, id, Produtos: [], Bancas: [], Recursos: []);

    /// <summary>O tipo de etapa como o cadastro o devolve hoje, sem descrição e sem conceitos.</summary>
    internal static TipoEtapaView TipoNoCadastro(
        Guid id, string codigo, string nome, bool admitePontuacao = true, bool admiteEliminacao = true, bool notaDeOrigemNoEnem = false, bool exigeCorrecaoIndependente = false) =>
        TipoEtapaViewDeTeste.Com(id, codigo).Chamado(nome).ComCarater(admitePontuacao, admiteEliminacao)
            .ComNotaDoEnem(notaDeOrigemNoEnem).ComCorrecaoIndependente(exigeCorrecaoIndependente).Construir();

    /// <summary>O tipo de etapa como ficou congelado na etapa do processo.</summary>
    internal static TipoEtapaSnapshot TipoCongelado(
        Guid origemId, string codigo, string nome, bool admitePontuacao, bool admiteEliminacao, bool notaDeOrigemNoEnem = false) =>
        TipoEtapaSnapshot.Criar(origemId, codigo, nome, admitePontuacao, admiteEliminacao, notaDeOrigemNoEnem).Value!;

    /// <summary>Cadastro que devolve cada um dos tipos dados, ativo, para o id dele.</summary>
    internal static ITipoEtapaReader ReaderDoTipo(params TipoEtapaView[] tipos)
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        foreach (TipoEtapaView tipo in tipos)
        {
            reader.ObterAtivoPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        }

        return reader;
    }

    /// <summary>Etapa do processo com o tipo congelado dado, sem faixa de nota máxima nem fase.</summary>
    internal static EtapaProcesso EtapaDoProcesso(
        string nome, CaraterEtapa carater, int ordem, TipoEtapaSnapshot tipo, decimal? peso, decimal? notaMinima = null) => EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
        new IdentificacaoDaEtapa(Nome: nome, Carater: carater, TipoEtapa: tipo, Ordem: ordem, FaseCodigo: null),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: notaMinima, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    /// <summary>Certame recém-criado que já tem as etapas dadas; recusa do agregado aqui é ambienteDeTeste inválida, não cenário.</summary>
    internal static ProcessoSeletivo ProcessoComAsEtapas(params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        processo.DefinirEtapas(etapas).IsSuccess.Should().BeTrue("as etapas da ambienteDeTeste têm de ser aceitas pelo agregado");

        return processo;
    }

    /// <summary>Define as etapas no processo, com o cadastro dado e um controle de gravação novo.</summary>
    internal static Task<Result> ExecutarSobre(ProcessoSeletivo processo, ITipoEtapaReader? tipoEtapaReader, params EtapaProcessoInput[] etapas) =>
        Executar(new DefinirEtapasCommand(processo.Id, etapas), RepositorioCom(processo), Substitute.For<IUnitOfWork>(), tipoEtapaReader);

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
