namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
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
using static SubmeterDeTeste;

/// <summary>
/// O veredito do envio decide por obrigatórios, termos e documentos antes de qualquer gravação, e o que é gravado depende dele.
/// </summary>
public sealed class SubmeterInscricaoVereditoEGravacaoTests
{
    [Fact(DisplayName = "Item obrigatório respondido e termo obrigatório aceito na versão fixada: submete, gera número e grava o comprovante, com 'Prefiro não informar' em identidade de gênero e orientação sexual")]
    public async Task Handle_ItemRespondidoETermoAceito_Submete()
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.FernandaDrumond, MontarVersaoComItemObrigatorioETermo());

        IGeradorDeNumeroInscricao gerador = GeradorQueDevolve("00000042");

        Result<ReciboDaInscricaoDto> resultado = await cenario.SubmeterAsync(cenario.Pedido, numeros: gerador);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        ConferirComprovante(cenario, resultado.Value!);
        await cenario.Inscricoes.Received(1).AtualizarAsync(cenario.Inscricao, Arg.Any<CancellationToken>());
        await cenario.Comprovantes.Received(1).AdicionarComprovanteAsync(Arg.Any<ComprovanteInscricao>(), Arg.Any<CancellationToken>());
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Item obrigatório sem resposta recusa por ausência, sem gerar número nem gravar nada")]
    public async Task Handle_ItemObrigatorioSemResposta_RecusaSemGerarNumero()
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.KevinCarvalho, MontarVersaoComItemObrigatorioETermo());

        IGeradorDeNumeroInscricao gerador = Substitute.For<IGeradorDeNumeroInscricao>();

        InscricaoRequest request = PedidoSemApelido(cenario);

        Result<ReciboDaInscricaoDto> resultado = await cenario.SubmeterAsync(request, numeros: gerador);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "APELIDO" && e.Error.Code == "Inscricao.ItemObrigatorioAusente");
        await gerador.DidNotReceive().ProximoAsync(Arg.Any<CancellationToken>());
        await cenario.Inscricoes.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Termo obrigatório não aceito recusa a submissão, mesmo com o item obrigatório respondido")]
    public async Task Handle_TermoObrigatorioNaoAceito_Recusa()
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.FernandaDrumond, MontarVersaoComItemObrigatorioETermo());

        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cenario.Cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement("Nanda");
        InscricaoRequest request = new(null, respostas, null, null);

        Result<ReciboDaInscricaoDto> resultado = await cenario.SubmeterAsync(request);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "TERMO_VERACIDADE" && e.Error.Code == "Inscricao.TermoNaoAceito",
            "a recusa sai no código do aceite que o candidato envia");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Estrangeiro que informa só um dos documentos, passaporte ou RNM, é recusado pela falta do outro")]
    [InlineData("PASSAPORTE_NUMERO", "RNM_NUMERO")]
    [InlineData("RNM_NUMERO", "PASSAPORTE_NUMERO")]
    public async Task Handle_EstrangeiroComUmSoDosDocumentos_Recusa(string informado, string faltante)
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.KevinCarvalho, MontarVersaoComItemObrigatorioETermo());

        Dictionary<string, JsonElement> respostas = RespostasDoEstrangeiro(cenario.Cpf, informado);
        InscricaoRequest request = new(
            OfertaDo(cenario.Configuracao), respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", cenario.Termo.VersaoId, cenario.Termo.Hash)]);

        Result<ReciboDaInscricaoDto> resultado = await cenario.SubmeterAsync(request);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == faltante && e.Error.Code == "Inscricao.ItemObrigatorioAusente");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Item respondido numa chamada anterior de Salvar e não reenviado na submissão continua preenchido, porque o veredito avalia o estado mesclado")]
    public async Task Handle_ItemRespondidoAntesNaoReenviado_PermaneceGravado()
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.KevinCarvalho, MontarVersaoComItemObrigatorioETermo());
        SalvarApelido(cenario);

        // Só o aceite do termo vem nesta chamada — APELIDO já estava gravado pela chamada de Salvar.
        InscricaoRequest request = new(
            OfertaDo(cenario.Configuracao), new Dictionary<string, JsonElement>(StringComparer.Ordinal), null,
            [new AceiteDeTermoInput("TERMO_VERACIDADE", cenario.Termo.VersaoId, cenario.Termo.Hash)]);

        Result<ReciboDaInscricaoDto> resultado = await cenario.SubmeterAsync(request);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Guardar(cenario.Inscricao, cenario.Inscricoes);
        Emitido(cenario.Inscricao).Respostas.Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
    }

    [Fact(DisplayName = "O item e o termo obrigatórios de outra finalidade, como a isenção, não recusam o envio da inscrição")]
    public async Task Handle_ObrigatoriosDaIsencao_NaoRecusamOEnvio()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], grupos: null, new Dictionary<string, JsonElement>(StringComparer.Ordinal), ajustar: DefinirObrigatoriosDaIsencao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "DbUpdateConcurrencyException (xmin) no commit descarta o rastreamento e devolve conflito, sem deixar a exceção propagar")]
    public async Task Handle_ConflitoDeConcorrenciaNoCommit_DescartaEDevolveConflito()
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.FernandaDrumond, MontarVersaoComItemObrigatorioETermo());

        cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result<ReciboDaInscricaoDto> resultado = await cenario.SubmeterAsync(cenario.Pedido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.ConflitoDeConcorrencia");
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
    private static void ConferirComprovante(CenarioDoEnvioDeTeste cenario, ReciboDaInscricaoDto recibo)
    {
        recibo.NumeroInscricao.Should().Be("00000042");
        recibo.InscricaoId.Should().Be(cenario.Inscricao.Id);
        recibo.NumeroDoComprovante.Should().Be(1);
        Guardar(cenario.Inscricao, cenario.Inscricoes);
        ComprovanteInscricao emitido = Emitido(cenario.Inscricao);
        recibo.Instante.Should().Be(emitido.Instante);
        emitido.TodasAsRespostas().Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
        emitido.TodasAsRespostas()["IDENTIDADE_GENERO"].GetString().Should().Be("PREFIRO_NAO_INFORMAR", "'Prefiro não informar' é resposta válida e volta ao titular no comprovante");
        emitido.TodasAsRespostas()["ORIENTACAO_SEXUAL"].GetString().Should().Be("PREFIRO_NAO_INFORMAR");
        HashCanonicalComputer.IsValidHashShape(emitido.HashIntegridade).Should().BeTrue();
    }

    private static InscricaoRequest PedidoSemApelido(CenarioDoEnvioDeTeste cenario)
    {
        return new InscricaoRequest(OfertaDo(cenario.Configuracao), RespostasDoConjuntoBasico(cenario.Cpf), null,
            [new AceiteDeTermoInput("TERMO_VERACIDADE", cenario.Termo.VersaoId, cenario.Termo.Hash)]);
    }

    private static Dictionary<string, JsonElement> RespostasDoEstrangeiro(Cpf cpf, string informado)
    {
        // O estrangeiro não responde o RG nem a naturalidade, que ficam ocultos para ele.
        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        foreach (string doBrasileiro in new[] { "RG_NUMERO", "RG_ORGAO_EMISSOR", "RG_UF", "RG_DATA_EMISSAO", "NATURALIDADE_UF", "NATURALIDADE_MUNICIPIO" })
        {
            respostas.Remove(doBrasileiro);
        }

        respostas["NACIONALIDADE"] = JsonSerializer.SerializeToElement("ESTRANGEIRO");
        respostas[informado] = JsonSerializer.SerializeToElement("FX1234567");
        respostas["APELIDO"] = JsonSerializer.SerializeToElement("Kevin");
        return respostas;
    }

    private static void SalvarApelido(CenarioDoEnvioDeTeste cenario)
    {
        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cenario.Cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement("Nanda");
        Result salvo = cenario.Inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []), Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver.Admitidas(cenario.Configuracao)
                with
        { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([cenario.Inscricao.ETag]));
        salvo.IsSuccess.Should().BeTrue(salvo.Error?.Message);
    }

    private static void DefinirObrigatoriosDaIsencao(ProcessoSeletivo processo)
    {
        FormularioDeTeste.DefinirItens(processo, [RendaDeclarada()], finalidade: FinalidadeFormulario.IsencaoTaxa).IsSuccess.Should().BeTrue();
        VersaoTermoEscolhida termo = new(Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1,
            "Termo da isenção", "Declaro a renda informada.", "Lei 12.799/2013", "ACEITE_EXPLICITO", new string('d', 64));
        FormularioDeTeste.DefinirTermos(processo,
            [TermoExigidoFormulario.Criar("TERMO_ISENCAO", 0, termo, exibicao: null, Obrigatoriedade.Sempre, FinalidadeFormulario.IsencaoTaxa).Value!],
            finalidade: FinalidadeFormulario.IsencaoTaxa).IsSuccess.Should().BeTrue();
    }

    private static FatoColetado RendaDeclarada() => FatoColetado.Criar(
        "RENDA_DECLARADA", 0, "Renda declarada", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
        classificacaoProtecao: "PESSOAL", formato: "LIVRE", finalidade: FinalidadeFormulario.IsencaoTaxa,
        hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade,
        descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
}
