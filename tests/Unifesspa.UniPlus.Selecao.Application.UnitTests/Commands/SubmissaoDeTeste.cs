namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;

internal sealed class SubmissaoDeTeste(
    IInscricaoRepository inscricoes, IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs,
    IUnitOfWork unitOfWork, IUserContext usuario)
{
    public IConferenciaEnderecoGeo Geo { get; init; } = SubmeterDeTeste.ConferenciaQueAceitaTudo();
    public IGeradorDeNumeroInscricao Numeros { get; init; } = SubmeterDeTeste.GeradorQueDevolve("00000001");
    public TimeProvider Relogio { get; init; } = DivulgacaoDeTeste.Relogio;
    public IDocumentoDaInscricaoRepository Documentos { get; init; } = Substitute.For<IDocumentoDaInscricaoRepository>();

    public Task<Result<ReciboDaInscricaoDto>> SubmeterAsync(SubmeterInscricaoCommand command) =>
        SubmeterInscricaoCommandHandler.Handle(command, new InscricaoDoDono(inscricoes, usuario), CriarSubmissao(), unitOfWork, CancellationToken.None);

    private SubmissaoDaInscricao CriarSubmissao()
    {
        ICertameDivulgadoRepository certames = DivulgacaoDeTeste.Certame(ProcessoId);
        IResolvedorFusoInstitucional fuso = DivulgacaoDeTeste.Fuso();
        DocumentosEnviadosDaInscricao enviados = new(Documentos);
        return new SubmissaoDaInscricao(
            new PreparacaoDoEnvioDaInscricao(new PeriodoDaInscricaoResolver(certames, processos, codecs, fuso),
                new VersaoDaInscricaoResolver(certames, processos, codecs), new ContextoDaVersaoFixadaResolver(processos, codecs), Relogio),
            new ConferenciaDoEnvioDaInscricao(new ConferenciaDoEnderecoDaInscricao(Geo), new ConferenciaDosMunicipiosDaInscricao(Geo), enviados, fuso, usuario),
            new RegistroDoEnvioDaInscricao(inscricoes, SubmeterDeTeste.ComprovantesDe(inscricoes), Numeros, enviados));
    }
}
