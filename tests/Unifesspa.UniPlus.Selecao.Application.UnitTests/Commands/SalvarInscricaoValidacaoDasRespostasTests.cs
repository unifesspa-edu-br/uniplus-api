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
        SalvarRespostaDeTeste cenario = SalvarRespostaDeTeste.ComItem(Apelido(10));
        Result<InscricaoDto> resultado = await cenario.SalvarAsync(Resposta("APELIDO", "Nanda"));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Formulario.Respostas.Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
        await cenario.Inscricoes.Received(1).AtualizarAsync(cenario.Inscricao, Arg.Any<CancellationToken>());
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Salvar com a oferta A avalia só as modalidades da oferta A, mesmo com o rascunho gravado na oferta B")]
    public async Task Handle_OfertaDoCorpo_AvaliaSoAsModalidadesDaOfertaSalva()
    {
        (VersaoConfiguracao versao, ConfiguracaoPublicada configuracaoBase) = MontarVersaoComItem(Apelido(null, Obrigatoriedade.Nunca));
        Guid ofertaA = Guid.CreateVersion7();
        Guid ofertaB = Guid.CreateVersion7();
        SalvarRespostaDeTeste cenario = new(versao, OfertasDeTeste.ComDistribuicao(
            configuracaoBase, OfertasDeTeste.Distribuicao(ofertaA, "AC"), OfertasDeTeste.Distribuicao(ofertaB, "AC", "AC_FEM")));
        cenario.GravarOferta(ofertaB);
        InscricaoRequest request = Resposta(OfertasDeTeste.FatoDaAcaoAfirmativa, OfertasDeTeste.ValorDaAcaoAfirmativa) with { OfertaCursoId = ofertaA };
        Result<InscricaoDto> resultado = await cenario.SalvarAsync(request);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.EscolhasDeCurso.OfertaCursoId.Should().Be(ofertaA);
        resultado.Value.Avaliacao.Modalidades.Itens.Select(static m => m.Codigo).Should().Equal(
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
        FatoColetado endereco = FatoColetadoDeTeste.Coletando("ENDERECO_RESIDENCIAL", 0)
            .ComRotulo("Endereço residencial")
            .DoTipo(TipoDeCampo.Endereco)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir();
        SalvarRespostaDeTeste cenario = SalvarRespostaDeTeste.ComItem(endereco);
        InscricaoRequest request = new(null, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = "79096-730", cidade = new { codigoIbge = "5002704", nome = "Campo Grande", uf = "MS" } }),
        }, null, null);
        return (await cenario.SalvarAsync(request, geo), cenario.UnitOfWork);
    }

    [Fact(DisplayName = "Resposta que viola uma restrição do formulário é recusada por campo, sem gravar nada — obrigatoriedade sem resposta não é recusada")]
    public async Task Handle_RespostaViolaRestricao_RecusaPorCampoSemGravar()
    {
        SalvarRespostaDeTeste cenario = SalvarRespostaDeTeste.ComItem(Apelido(5), cpf: PersonasDeTeste.KevinCarvalho);
        Result<InscricaoDto> resultado = await cenario.SalvarAsync(Resposta("APELIDO", "Apelido Enorme"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "APELIDO" && e.Error.Code == "Inscricao.RespostaInvalida");
        await cenario.Inscricoes.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Data fora da faixa é recusada no rascunho com os limites do dia do salvar")]
    public async Task Handle_DataForaDaFaixa_RecusaComOsLimites()
    {
        SalvarRespostaDeTeste cenario = SalvarRespostaDeTeste.ComItem(DataDeNascimento(), cpf: PersonasDeTeste.KevinCarvalho);
        Result<InscricaoDto> resultado = await cenario.SalvarAsync(Resposta("DATA_NASCIMENTO", "2027-01-10"));

        resultado.Errors.Should().ContainSingle(static e => e.Field == "DATA_NASCIMENTO" && e.Error.Code == InscricaoErrorCodes.DataForaDaFaixa)
            .Which.Error.Message.Should().Be("Informe uma data entre 15/01/1906 e 15/01/2026.");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resposta de um membro de grupo repetível que viola uma restrição do formulário também é recusada por campo, sem gravar nada")]
    public async Task Handle_RespostaDeMembroDeGrupoViolaRestricao_RecusaPorCampoSemGravar()
    {
        SalvarRespostaDeTeste cenario = SalvarRespostaDeTeste.ComItem(Apelido(10), [ComposicaoFamiliar()]);
        Result<InscricaoDto> resultado = await cenario.SalvarAsync(ComRespostaDoMembro());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "COMPOSICAO_FAMILIAR.membro-1.RG_NUMERO_MEMBRO" && e.Error.Code == "Inscricao.RespostaInvalida");
        await cenario.Inscricoes.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static FatoColetado Apelido(int? tamanhoMaximo, Obrigatoriedade? obrigatoriedade = null) =>
        FatoColetadoDeTeste.Coletando("APELIDO", 0)
            .ComRotulo("Apelido")
            .DeTexto("LIVRE")
            .ComObrigatoriedade(obrigatoriedade ?? Obrigatoriedade.Sempre)
            .ComRestricoes(tamanhoMaximo is { } maximo ? [new TamanhoTexto(1, maximo)] : null)
            .Construir();

    private static FatoColetado DataDeNascimento() =>
        FatoColetadoDeTeste.Coletando("DATA_NASCIMENTO", 0)
            .ComRotulo("Data de nascimento")
            .DoTipo(TipoDeCampo.Data)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComRestricoes(new FaixaDeData(LimiteDeData.NoEnvio(anos: -120, dias: 0), LimiteDeData.NoEnvio(anos: 0, dias: 0)))
            .Construir();

    private static GrupoColetado ComposicaoFamiliar() =>
        GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 1)
            .ComOcorrencias(0, null)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComCampos(FatoColetadoDeTeste.Coletando("RG_NUMERO_MEMBRO", 0)
                .ComRotulo("RG do membro")
                .DeTexto("LIVRE")
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .ComClassificacao("IDENTIFICADOR")
                .ComRestricoes(new TamanhoTexto(1, 5))
                .Construir())
            .Construir();

    private static InscricaoRequest ComRespostaDoMembro() => Resposta("APELIDO", "Nanda") with
    {
        Grupos = new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = [new("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["RG_NUMERO_MEMBRO"] = JsonSerializer.SerializeToElement("12.345.678-9"),
            })],
        },
    };

    private static InscricaoRequest Resposta(string codigo, string valor) =>
        new(null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [codigo] = JsonSerializer.SerializeToElement(valor) }, null, null);
}
