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

/// <summary>Monta o envio da inscrição e as portas que o handler de submeter consulta, para os testes controlarem cada uma.</summary>
internal static class SubmeterDeTeste
{
    internal static Inscricao NovaInscricao(Cpf cpf, string contaId, Guid versaoFormularioId) =>
        Inscricao.Iniciar(ProcessoId, cpf, contaId, "https://issuer", versaoFormularioId).Value!;

    internal static IUserContext UsuarioComCpf(Cpf cpf, string contaId = "conta-1", string? nomeSocial = null)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.NomeCompleto.Returns(PersonasDeTeste.NomeFernandaDrumond);
        contexto.NomeSocial.Returns(nomeSocial);
        contexto.UserId.Returns(contaId);
        contexto.Issuer.Returns("https://issuer");
        return contexto;
    }

    internal static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Inscricao, ComprovanteInscricao> Emitidos = [];

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IInscricaoRepository, IComprovanteDaInscricaoRepository> Comprovantes = [];

    internal static IComprovanteDaInscricaoRepository ComprovantesDe(IInscricaoRepository inscricoes) =>
        Comprovantes.GetValue(inscricoes, static _ => Substitute.For<IComprovanteDaInscricaoRepository>());

    /// <summary>O comprovante que o envio gravou para a inscrição: o recibo não o repete, quem o lê é a consulta.</summary>
    internal static ComprovanteInscricao Emitido(Inscricao inscricao) =>
        Emitidos.TryGetValue(inscricao, out ComprovanteInscricao? comprovante) ? comprovante : throw new InvalidOperationException("O envio não gravou comprovante.");

    internal static bool GravouComprovante(Inscricao inscricao) => Emitidos.TryGetValue(inscricao, out _);

    internal static void Guardar(Inscricao inscricao, IInscricaoRepository repositorio)
    {
        ComprovanteInscricao? gravado = ComprovantesDe(repositorio).ReceivedCalls()
            .Where(static c => c.GetMethodInfo().Name == nameof(IComprovanteDaInscricaoRepository.AdicionarComprovanteAsync))
            .Select(static c => (ComprovanteInscricao)c.GetArguments()[0]!)
            .SingleOrDefault();
        if (gravado is not null)
        {
            Emitidos.AddOrUpdate(inscricao, gravado);
        }
    }

    internal static Task<Result<ReciboDaInscricaoDto>> SubmeterAsync(
        SubmeterInscricaoCommand command, IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository, IRegistroCodecsEnvelope registroCodecs,
        IUnitOfWork unitOfWork, IUserContext userContext, IConferenciaEnderecoGeo? conferenciaEnderecoGeo = null) =>
        new SubmissaoDeTeste(inscricaoRepository, processoSeletivoRepository, registroCodecs, unitOfWork, userContext)
        {
            Geo = conferenciaEnderecoGeo ?? ConferenciaQueAceitaTudo(),
        }.SubmeterAsync(command);

    internal static IInscricaoRepository InscricoesComAtiva(Inscricao inscricao)
    {
        IInscricaoRepository inscricoes = Substitute.For<IInscricaoRepository>();
        inscricoes.ObterAtivaPorProcessoECpfAsync(inscricao.ProcessoSeletivoId, inscricao.Titular.Cpf, Arg.Any<CancellationToken>()).Returns(inscricao);
        return inscricoes;
    }

    internal static (IInscricaoRepository Inscricoes, IProcessoSeletivoRepository Processos, IRegistroCodecsEnvelope Codecs) PortasPara(
        Inscricao inscricao, VersaoConfiguracao versao, ConfiguracaoPublicada grafo)
    {
        IInscricaoRepository inscricoes = Substitute.For<IInscricaoRepository>();
        inscricoes.ObterAtivaPorProcessoECpfAsync(inscricao.ProcessoSeletivoId, inscricao.Titular.Cpf, Arg.Any<CancellationToken>()).Returns(inscricao);
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processos, codecs, versao, grafo);
        return (inscricoes, processos, codecs);
    }

    internal static IGeradorDeNumeroInscricao GeradorQueDevolve(string numero)
    {
        IGeradorDeNumeroInscricao gerador = Substitute.For<IGeradorDeNumeroInscricao>();
        gerador.ProximoAsync(Arg.Any<CancellationToken>()).Returns(numero);
        return gerador;
    }

    internal static IConferenciaEnderecoGeo ConferenciaQueAceitaTudo()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();
        conferencia.ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        conferencia.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        conferencia.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Success(new CepResolvido("1501402", "Marabá", "PA", "logradouro")));
        return conferencia;
    }

    internal static Task<(Result<ReciboDaInscricaoDto> Resultado, Inscricao Inscricao)> SubmeterComAsync(
        IReadOnlyList<FatoColetado> itens, IReadOnlyList<GrupoColetado>? grupos,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? respostasDosGrupos = null,
        Action<ProcessoSeletivo>? ajustar = null, string? nomeSocialDaConta = null, IConferenciaEnderecoGeo? geo = null) =>
        new FormularioASubmeterDeTeste(itens, grupos, ajustar)
        {
            NomeSocialDaConta = nomeSocialDaConta,
            Geo = geo ?? ConferenciaQueAceitaTudo(),
        }.SubmeterAsync(respostas, respostasDosGrupos);

    internal const string Belem = "1501402";

    /// <summary>O Geo que conhece Belém no Pará e o CEP de Belém usado nas respostas básicas.</summary>
    internal static FakeConferenciaEnderecoGeo GeoQueConhece() =>
        new FakeConferenciaEnderecoGeo().ComMunicipio(Belem, "PA", "Belém").ComCep("66010000", Belem, "PA", cidade: "Belém");
}
