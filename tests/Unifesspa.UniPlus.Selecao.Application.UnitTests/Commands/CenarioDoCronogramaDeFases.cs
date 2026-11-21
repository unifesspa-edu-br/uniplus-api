namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// Um processo e os cadastros de Configuração e de Publicações que o handler do cronograma consulta.
/// Os testes descrevem o que o cadastro tem; a chamada ao handler fica toda aqui, de modo que uma mudança
/// de assinatura dele se faz num lugar só.
/// </summary>
internal sealed class CenarioDoCronogramaDeFases
{
    private CenarioDoCronogramaDeFases(ProcessoSeletivo processo)
    {
        Processo = processo;
        Repository = RepositorioQueDevolve(processo);
        PrecedenciaFaseReader = PrecedenciasDeTeste.SemArestas();
        FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<FatoCandidatoView>)[]);
    }

    public ProcessoSeletivo Processo { get; }

    public IProcessoSeletivoRepository Repository { get; }

    public IFaseCanonicaReader FaseCanonicaReader { get; } = Substitute.For<IFaseCanonicaReader>();

    public IResolvedorDaUnidadeResponsavel UnidadeResponsavel { get; } = Substitute.For<IResolvedorDaUnidadeResponsavel>();

    public ITipoBancaReader TipoBancaReader { get; } = Substitute.For<ITipoBancaReader>();

    public ICategoriaDocumentoReader CategoriaDocumentoReader { get; } = Substitute.For<ICategoriaDocumentoReader>();

    public IPrecedenciaFaseReader PrecedenciaFaseReader { get; private set; }

    public IRegraCatalogoReader RegraCatalogoReader { get; } = Substitute.For<IRegraCatalogoReader>();

    public IFatoCandidatoReader FatoCandidatoReader { get; } = Substitute.For<IFatoCandidatoReader>();

    public ITipoAtoPublicadoReader TipoAtoPublicadoReader { get; } = Substitute.For<ITipoAtoPublicadoReader>();

    public IRegistroCodecsEnvelope Codecs { get; } = Substitute.For<IRegistroCodecsEnvelope>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public IMotivoDoProcessoRepository MotivosEscolhidos { get; } = MotivoDeTeste.SemEscolha();

    public static CenarioDoCronogramaDeFases EmRascunho() => Para(NovoProcessoEmRascunho());

    public static ProcessoSeletivo NovoProcessoEmRascunho() =>
        ProcessoSeletivo.Criar(
            "PS", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            IdentificadoresDeTeste.Novo());

    public static CenarioDoCronogramaDeFases Para(ProcessoSeletivo processo) => new(processo);

    /// <summary>O repositório não encontra o processo do comando.</summary>
    public CenarioDoCronogramaDeFases SemOProcessoNoRepositorio()
    {
        Repository.ObterParaMutacaoAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns((ProcessoSeletivo?)null);
        return this;
    }

    /// <summary>
    /// Cadastra a fase canônica no cadastro de Configuração e devolve a entrada de fase do payload que a
    /// referencia.
    /// </summary>
    public FaseCronogramaInput CadastrarFase(FaseCanonicaView fase)
    {
        FaseCanonicaReader.ObterPorIdAsync(fase.Id, Arg.Any<CancellationToken>()).Returns(fase);
        return EntradaDaFase(fase.Id);
    }

    public CenarioDoCronogramaDeFases ComTipoDeAto(TipoAtoPublicadoView tipo)
    {
        TipoAtoPublicadoReader.ObterVigenteAsync(tipo.Codigo, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(tipo);
        return this;
    }

    public CenarioDoCronogramaDeFases SemVersaoVigenteDoTipoDeAto(string codigo)
    {
        TipoAtoPublicadoReader.ObterVigenteAsync(codigo, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        return this;
    }

    /// <summary>O catálogo de fatos do candidato passa a ser o dado.</summary>
    public CenarioDoCronogramaDeFases ComFatosNoCatalogo(params FatoCandidatoView[] fatos)
    {
        FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<FatoCandidatoView>)fatos);
        return this;
    }

    public CenarioDoCronogramaDeFases ComRegraNoCatalogo(RegraCatalogo regra)
    {
        RegraCatalogoReader.ObterAsync(regra.Codigo, regra.Versao, Arg.Any<CancellationToken>()).Returns(regra);
        return this;
    }

    public TimeProvider Relogio { get; set; } = TimeProvider.System;

    public CenarioDoCronogramaDeFases ComUnidadeNoCadastro(UnidadeResponsavelSnapshot unidade)
    {
        UnidadeResponsavel.ResolverAsync(unidade.OrigemId, Arg.Any<CancellationToken>()).Returns(unidade);
        return this;
    }

    public CenarioDoCronogramaDeFases SemAUnidadeNoCadastro(Guid unidadeId)
    {
        UnidadeResponsavel.ResolverAsync(unidadeId, Arg.Any<CancellationToken>()).Returns((UnidadeResponsavelSnapshot?)null);
        return this;
    }

    /// <summary>O processo já tem uma versão publicada que congelou a classificação dos tipos de ato dados.</summary>
    public CenarioDoCronogramaDeFases ComVersaoPublicadaQueCongelou(params TipoDeAtoCongelado[] congelados)
    {
        VersaoPublicadaDeTeste.Publicar(Processo, Repository, Codecs, congelados);
        return this;
    }

    /// <summary>A versão publicada que o codec não consegue ler.</summary>
    public CenarioDoCronogramaDeFases ComVersaoPublicadaIlegivel(DomainError falhaDoCodec)
    {
        VersaoPublicadaDeTeste.PublicarIlegivel(Processo, Repository, Codecs, falhaDoCodec);
        return this;
    }

    /// <summary>
    /// Como <see cref="ComVersaoPublicadaQueCongelou"/>, com a configuração vinda de outro processo: a fase é
    /// reconciliada no lugar, e a configuração publicada tem de sobreviver à edição do processo vivo.
    /// </summary>
    public CenarioDoCronogramaDeFases ComVersaoPublicadaDeOutroProcesso(ProcessoSeletivo processoPublicado, params TipoDeAtoCongelado[] congelados)
    {
        VersaoPublicadaDeTeste.Publicar(Processo, processoPublicado, Repository, Codecs, congelados);
        return this;
    }

    /// <summary>O cadastro de precedência entre fases passa a ter as relações dadas.</summary>
    public CenarioDoCronogramaDeFases ComPrecedencias(params (string Antecessora, string Sucessora, bool PermiteSobreposicao)[] arestas)
    {
        PrecedenciaFaseReader = PrecedenciasDeTeste.Com(arestas);
        return this;
    }

    public Task<Result> DefinirFasesAsync(params FaseCronogramaInput[] fases) =>
        DefinirAsync(new DefinirCronogramaFasesCommand(Processo.Id, fases));

    /// <summary>Grava as fases noutro processo, consultando os mesmos cadastros.</summary>
    public Task<Result> DefinirEmOutroProcessoAsync(ProcessoSeletivo outro, params FaseCronogramaInput[] fases) =>
        ChamarHandlerAsync(
            new DefinirCronogramaFasesCommand(outro.Id, fases),
            RepositorioQueDevolve(outro));

    public Task<Result> DefinirAsync(DefinirCronogramaFasesCommand command) =>
        ChamarHandlerAsync(command, Repository);

    private Task<Result> ChamarHandlerAsync(DefinirCronogramaFasesCommand command, IProcessoSeletivoRepository repositorio) =>
        DefinirCronogramaFasesCommandHandler.Handle(
            command, repositorio, DefinicaoSobre(repositorio), UnitOfWork, Relogio, CancellationToken.None);

    private DefinicaoDoCronogramaDeFases DefinicaoSobre(IProcessoSeletivoRepository repositorio) => new(
        new MontagemDasFases(FaseCanonicaReader, UnidadeResponsavel, TipoBancaReader, CategoriaDocumentoReader, RegraCatalogoReader, FatoCandidatoReader),
        new LeituraDasPrecedenciasDosCadastros(PrecedenciaFaseReader), TipoAtoPublicadoReader, repositorio, Codecs, new MotivosDosLugaresQueSaem(MotivosEscolhidos));

    private static IProcessoSeletivoRepository RepositorioQueDevolve(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return repository;
    }
}
