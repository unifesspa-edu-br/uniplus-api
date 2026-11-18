namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Monta os serviços que o container entrega ao handler de salvar e o invoca, para os testes controlarem cada porta.</summary>
internal static class SalvarDeTeste
{
    internal static readonly Guid ProcessoId = Guid.CreateVersion7();

    internal static Inscricao NovaInscricao(Cpf cpf, string contaId, Guid versaoFormularioId) =>
        Inscricao.Iniciar(ProcessoId, cpf, contaId, "https://issuer", versaoFormularioId).Value!;

    internal static IUserContext UsuarioComCpf(Cpf cpf, string contaId = "conta-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.NomeCompleto.Returns(PersonasDeTeste.NomeFernandaDrumond);
        contexto.UserId.Returns(contaId);
        contexto.Issuer.Returns("https://issuer");
        return contexto;
    }

    internal static (VersaoConfiguracao Versao, GrafoConfiguracao Grafo) MontarVersaoComItem(
        FatoColetado item, IReadOnlyList<GrupoColetado>? grupos = null)
    {
        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Salvar rascunho");
        processoBase.DefinirItens([item], grupos: grupos).IsSuccess.Should().BeTrue();

        // A definição avaliável só reconhece um item que esteja numa etapa de um FORMULÁRIO
        // (RecorteDaFinalidade filtra por etapa de FormularioProcesso, não por EtapaProcesso do
        // certame) — por isso o grafo leva processoBase.Formularios, que FormularioDeTeste.DefinirItens
        // já colocou o item dentro da seção "DADOS" da finalidade Inscrição.
        return (DivulgacaoDeTeste.VersaoSemReferenciaTemporal(ProcessoId), DivulgacaoDeTeste.GrafoDe(processoBase));
    }

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
