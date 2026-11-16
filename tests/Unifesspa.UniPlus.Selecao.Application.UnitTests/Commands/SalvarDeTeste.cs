namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Monta os serviços que o container entrega ao handler de salvar e o invoca, para os testes controlarem cada porta.</summary>
internal static class SalvarDeTeste
{
    internal static Task<Result<InscricaoDto>> Handle(
        SalvarInscricaoCommand command,
        IInscricaoRepository inscricoes,
        IProcessoSeletivoRepository processos,
        IDocumentoDaInscricaoRepository documentos,
        ICertameDivulgadoRepository certames,
        IRegistroCodecsEnvelope codecs,
        IResolvedorFusoInstitucional fuso,
        IConferenciaEnderecoGeo geo,
        EnderecosDosDocumentosDoAto enderecos,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider relogio,
        CancellationToken cancellationToken) =>
        SalvarInscricaoCommandHandler.Handle(
            command,
            new RascunhoDoCandidato(new InscricaoAtivaDoCandidato(inscricoes), certames, processos, codecs, fuso, relogio),
            new SalvamentoDoRascunho(geo, inscricoes, unitOfWork),
            new RespostaDoRascunho(documentos, geo, enderecos),
            userContext,
            cancellationToken);
}
