namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

internal sealed class FluxoDaInscricaoDeTeste
{
    public IInscricaoRepository Inscricoes { get; init; } = Substitute.For<IInscricaoRepository>();
    public ICertameDivulgadoRepository Certames { get; init; } = Substitute.For<ICertameDivulgadoRepository>();
    public IProcessoSeletivoRepository Processos { get; init; } = Substitute.For<IProcessoSeletivoRepository>();
    public IDocumentoDaInscricaoRepository Documentos { get; init; } = Substitute.For<IDocumentoDaInscricaoRepository>();
    public IRegistroCodecsEnvelope Codecs { get; init; } = Substitute.For<IRegistroCodecsEnvelope>();
    public IResolvedorFusoInstitucional Fuso { get; init; } = DivulgacaoDeTeste.Fuso();
    public IConferenciaEnderecoGeo Geo { get; init; } = Substitute.For<IConferenciaEnderecoGeo>();
    public EnderecosDosDocumentosDoAto Enderecos { get; init; } = DivulgacaoDeTeste.Enderecos();
    public TimeProvider Relogio { get; init; } = DivulgacaoDeTeste.Relogio;
    public IUnitOfWork UnitOfWork { get; init; } = Substitute.For<IUnitOfWork>();
    public IUserContext Usuario { get; init; } = Substitute.For<IUserContext>();

    private PeriodoDaInscricaoResolver Periodo => new(Certames, Processos, Codecs, Fuso);
    private VersaoDaInscricaoResolver Versao => new(Certames, Processos, Codecs);
    private LeituraDaInscricao Leitura => new(Versao, new ContextoDaVersaoFixadaResolver(Processos, Codecs),
        new DocumentosEnviadosDaInscricao(Documentos), new PreparacaoDaAvaliacaoDaInscricao(Geo), Enderecos, Fuso, Relogio);

    public Task<Result<InscricaoDto>> IniciarAsync(IniciarInscricaoCommand command, CancellationToken cancellationToken) =>
        IniciarInscricaoCommandHandler.Handle(command, Inscricoes, Usuario,
            new InicioDaInscricao(Inscricoes, Certames, Processos, Periodo, Usuario, Relogio), Leitura, UnitOfWork, cancellationToken);

    public Task<Result<InscricaoDto>> ObterAsync(ObterInscricaoQuery query, CancellationToken cancellationToken) =>
        ObterInscricaoQueryHandler.Handle(query, new InscricaoDoDono(Inscricoes, Usuario), Leitura, cancellationToken);
    public Task<Result<AvaliacaoDaInscricaoDto>> AvaliarAsync(AvaliarInscricaoQuery query, CancellationToken cancellationToken) =>
        AvaliarInscricaoQueryHandler.Handle(query, Inscricoes, Usuario,
            new ConsultaDaAvaliacaoDaInscricao(Periodo, Versao, new ContextoDaVersaoFixadaResolver(Processos, Codecs),
                new DocumentosEnviadosDaInscricao(Documentos), new PreparacaoDaAvaliacaoDaInscricao(Geo), Fuso, Enderecos), Relogio, cancellationToken);

}
