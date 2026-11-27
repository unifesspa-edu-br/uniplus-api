namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static SalvarDeTeste;

internal sealed class SalvarRespostaDeTeste
{
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IRegistroCodecsEnvelope _codecs = Substitute.For<IRegistroCodecsEnvelope>();

    public SalvarRespostaDeTeste(VersaoConfiguracao versao, ConfiguracaoPublicada configuracao, Cpf? cpf = null)
    {
        Inscricao = NovaInscricao(cpf ?? PersonasDeTeste.FernandaDrumond, "conta-1", versao.Id);
        Configuracao = configuracao;
        Inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, Inscricao.Titular.Cpf, Arg.Any<CancellationToken>()).Returns(Inscricao);
        DivulgacaoDeTeste.ConfigurarVersao(_processos, _codecs, versao, configuracao);
    }

    public Inscricao Inscricao { get; }
    public ConfiguracaoPublicada Configuracao { get; }
    public IInscricaoRepository Inscricoes { get; } = Substitute.For<IInscricaoRepository>();
    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public static SalvarRespostaDeTeste ComItem(FatoColetado item, IReadOnlyList<GrupoColetado>? grupos = null, Cpf? cpf = null)
    {
        (VersaoConfiguracao versao, ConfiguracaoPublicada configuracao) = MontarVersaoComItem(item, grupos);
        return new(versao, configuracao, cpf);
    }

    public void GravarOferta(Guid oferta) =>
        Inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(oferta, null),
            new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []),
            Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver.Admitidas(Configuracao)
                with
            { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([Inscricao.ETag])).IsSuccess.Should().BeTrue();

    public Task<Result<InscricaoDto>> SalvarAsync(InscricaoRequest request, IConferenciaEnderecoGeo? geo = null, PrecondicaoIfMatch? precondicao = null) =>
        SalvarDeTeste.Handle(
            new(ProcessoId, request, precondicao ?? PrecondicaoIfMatch.DeTags([Inscricao.ETag])),
            Rascunho(Inscricoes, _processos, _codecs, DivulgacaoDeTeste.Relogio),
            Inscricoes, UnitOfWork, UsuarioComCpf(Inscricao.Titular.Cpf), geo);
}
