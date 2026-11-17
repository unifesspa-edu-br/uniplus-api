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

    /// <summary>O comprovante que o envio gravou para a inscrição: o recibo não o repete, quem o lê é a consulta.</summary>
    internal static ComprovanteInscricao Emitido(Inscricao inscricao) =>
        Emitidos.TryGetValue(inscricao, out ComprovanteInscricao? comprovante) ? comprovante : throw new InvalidOperationException("O envio não gravou comprovante.");

    internal static void Guardar(Inscricao inscricao, IInscricaoRepository repositorio)
    {
        ComprovanteInscricao? gravado = repositorio.ReceivedCalls()
            .Where(static c => c.GetMethodInfo().Name == nameof(IInscricaoRepository.AdicionarComprovanteAsync))
            .Select(static c => (ComprovanteInscricao)c.GetArguments()[0]!)
            .LastOrDefault();
        if (gravado is not null)
        {
            Emitidos.AddOrUpdate(inscricao, gravado);
        }
    }

    internal static Task<Result<ReciboDaInscricaoDto>> SubmeterAsync(
        SubmeterInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IConferenciaEnderecoGeo? conferenciaEnderecoGeo = null,
        IGeradorDeNumeroInscricao? geradorDeNumeroInscricao = null,
        TimeProvider? relogio = null,
        IDocumentoDaInscricaoRepository? documentoRepository = null) =>
        SubmeterInscricaoCommandHandler.Handle(
            command, inscricaoRepository, processoSeletivoRepository, DivulgacaoDeTeste.Certame(ProcessoId), documentoRepository ?? Substitute.For<IDocumentoDaInscricaoRepository>(), registroCodecs,
            DivulgacaoDeTeste.Fuso(),
            conferenciaEnderecoGeo ?? ConferenciaQueAceitaTudo(),
            geradorDeNumeroInscricao ?? GeradorQueDevolve("00000001"),
            unitOfWork, userContext, relogio ?? DivulgacaoDeTeste.Relogio, CancellationToken.None);

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

    internal static async Task<(Result<ReciboDaInscricaoDto> Resultado, Inscricao Inscricao)> SubmeterComAsync(
        IReadOnlyList<FatoColetado> itens,
        IReadOnlyList<GrupoColetado>? grupos,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? respostasDosGrupos = null,
        Action<ProcessoSeletivo>? ajustar = null,
        string? nomeSocialDaConta = null,
        IConferenciaEnderecoGeo? geo = null,
        IReadOnlyCollection<string>? remover = null)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(itens, grupos, ajustar);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        InscricaoRequest comApelido = RequestComApelidoETermoAceito(cpf, termo, grafo);
        Dictionary<string, JsonElement> todas = new(comApelido.Respostas!, StringComparer.Ordinal);
        foreach (string fato in remover ?? [])
        {
            todas.Remove(fato);
        }

        foreach ((string fato, JsonElement valor) in respostas)
        {
            todas[fato] = valor;
        }

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, comApelido with { Respostas = todas, Grupos = respostasDosGrupos }, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf, nomeSocial: nomeSocialDaConta), geo);
        Guardar(existente, inscricaoRepository);
        return (resultado, existente);
    }

    internal const string Belem = "1501402";

    /// <summary>O Geo que conhece Belém no Pará e o CEP de Belém usado nas respostas básicas.</summary>
    internal static FakeConferenciaEnderecoGeo GeoQueConhece() =>
        new FakeConferenciaEnderecoGeo().ComMunicipio(Belem, "PA", "Belém").ComCep("66010000", Belem, "PA", cidade: "Belém");
}
