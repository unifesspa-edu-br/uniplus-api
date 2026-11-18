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
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static SalvarDeTeste;

/// <summary>
/// Salvar o rascunho valida só a estrutura das respostas: recusa por campo a que viola uma restrição do formulário,
/// nunca por obrigatoriedade. As regras vêm da versão fixada pela inscrição ao iniciar, não da divulgação atual (UNI-REQ-0084).
/// </summary>
public sealed class SalvarInscricaoValidacaoDasRespostasTests
{
    [Fact(DisplayName = "Resposta dentro do tamanho permitido é salva, sobre a versão fixada ao iniciar")]
    public async Task Handle_RespostaValida_Salva()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 10)], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Nanda") }, null, null);

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Respostas.Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
        await inscricaoRepository.Received(1).AtualizarAsync(existente, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Salvar com a oferta A avalia só as modalidades da oferta A, mesmo com o rascunho gravado na oferta B")]
    public async Task Handle_OfertaDoCorpo_AvaliaSoAsModalidadesDaOfertaSalva()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase) = MontarVersaoComItem(FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!);
        Guid ofertaA = Guid.CreateVersion7();
        Guid ofertaB = Guid.CreateVersion7();
        GrafoConfiguracao grafo = OfertasDeTeste.ComDistribuicao(
            grafoBase, OfertasDeTeste.Distribuicao(ofertaA, "AC"), OfertasDeTeste.Distribuicao(ofertaB, "AC", "AC_FEM"));
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);
        existente.Salvar(
            ofertaB, null, new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [],
            Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver.Admitidas(grafo) with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond },
            PrecondicaoIfMatch.DeTags([existente.ETag])).IsSuccess.Should().BeTrue();

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        InscricaoRequest request = new(
            ofertaA,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [OfertasDeTeste.FatoDaAcaoAfirmativa] = JsonSerializer.SerializeToElement(OfertasDeTeste.ValorDaAcaoAfirmativa),
            },
            null, null);

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(),
            Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.OfertaCursoId.Should().Be(ofertaA);
        resultado.Value!.Avaliacao.Modalidades.Itens.Select(static m => m.Codigo).Should().Equal(
            ["AC"], "a modalidade que só a oferta B tem não vale para quem salvou na oferta A");
    }

    [Fact(DisplayName = "Endereço com CEP que o Geo não encontra é recusado no campo do endereço, sem gravar o rascunho")]
    public async Task Handle_EnderecoComCepNaoEncontrado_RecusaSemGravar()
    {
        IConferenciaEnderecoGeo geo = Substitute.For<IConferenciaEnderecoGeo>();
        geo.ResolverCepAsync("79096730", Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.CepNaoResolve, "sem cobertura")));

        (Result<InscricaoDto> resultado, IUnitOfWork unitOfWork) = await SalvarEnderecoAsync(geo);

        resultado.Errors.Should().ContainSingle(e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoCepNaoEncontrado);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Geo fora do ar não impede salvar o rascunho do endereço, e a recusa nunca é de CEP não encontrado")]
    public async Task Handle_EnderecoComGeoIndisponivel_SalvaORascunho()
    {
        IConferenciaEnderecoGeo geo = Substitute.For<IConferenciaEnderecoGeo>();
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "fora do ar")));

        (Result<InscricaoDto> resultado, IUnitOfWork unitOfWork) = await SalvarEnderecoAsync(geo);

        resultado.Errors.Should().NotContain(e => e.Error.Code == InscricaoErrorCodes.EnderecoCepNaoEncontrado);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static async Task<(Result<InscricaoDto> Resultado, IUnitOfWork UnitOfWork)> SalvarEnderecoAsync(IConferenciaEnderecoGeo geo)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        FatoColetado item = FatoColetado.Criar(
            "ENDERECO_RESIDENCIAL", 0, "Endereço residencial", TipoRenderizacao.Endereco, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        InscricaoRequest request = new(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new
                {
                    cep = "79096-730",
                    cidade = new { codigoIbge = "5002704", nome = "Campo Grande", uf = "MS" },
                }),
            },
            null, null);

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(),
            geo, DivulgacaoDeTeste.Enderecos(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);
        return (resultado, unitOfWork);
    }

    [Fact(DisplayName = "Resposta que viola uma restrição do formulário é recusada por campo, sem gravar nada — obrigatoriedade sem resposta não é recusada")]
    public async Task Handle_RespostaViolaRestricao_RecusaPorCampoSemGravar()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // "Apelido Enorme" tem mais de 5 caracteres — viola o TamanhoTexto(1, 5) da versão fixada.
        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "APELIDO" && e.Error.Code == "Inscricao.RespostaInvalida");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Data fora da faixa é recusada no rascunho com os limites do dia do salvar")]
    public async Task Handle_DataForaDaFaixa_RecusaComOsLimites()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        FatoColetado item = FatoColetado.Criar(
            "DATA_NASCIMENTO", 0, "Data de nascimento", TipoRenderizacao.Data, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            restricoes: [new FaixaDeData(LimiteDeData.NoEnvio(anos: -120, dias: 0), LimiteDeData.NoEnvio(anos: 0, dias: 0))], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement("2027-01-10") }, null, null);

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.Errors.Should().ContainSingle(static e => e.Field == "DATA_NASCIMENTO" && e.Error.Code == InscricaoErrorCodes.DataForaDaFaixa)
            .Which.Error.Message.Should().Be("Informe uma data entre 15/01/1906 e 15/01/2026.");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resposta de um membro de grupo repetível que viola uma restrição do formulário também é recusada por campo, sem gravar nada")]
    public async Task Handle_RespostaDeMembroDeGrupoViolaRestricao_RecusaPorCampoSemGravar()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;

        FatoColetado itemDoTopo = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 10)], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        GrupoColetado composicaoFamiliar = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", 1, etapaCodigo: FormularioDeTeste.Secao, "Composição familiar",
            minimo: 0, maximo: null, exibicao: null, Obrigatoriedade.Sempre,
            [FatoColetado.Criar(
                "RG_NUMERO_MEMBRO", 0, "RG do membro", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                classificacaoProtecao: "IDENTIFICADOR", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            FinalidadeFormulario.Inscricao).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(itemDoTopo, [composicaoFamiliar]);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // O RG do membro excede o TamanhoTexto(1, 5) do subitem do grupo — a mesma violação do
        // teste anterior, agora dentro de uma ocorrência de COMPOSICAO_FAMILIAR.
        InscricaoRequest request = new(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Nanda") },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new OcorrenciaDeGrupoInscricaoInput("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["RG_NUMERO_MEMBRO"] = JsonSerializer.SerializeToElement("12.345.678-9"),
                    }),
                ],
            },
            null);

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "COMPOSICAO_FAMILIAR.membro-1.RG_NUMERO_MEMBRO" && e.Error.Code == "Inscricao.RespostaInvalida");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
