namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;
using static SubmeterDeTeste;

/// <summary>
/// A resposta de item ou grupo que a regra de exibição oculta é descartada, e a do nome social segue a conta do candidato.
/// </summary>
public sealed class SubmeterInscricaoRespostasCondicionaisTests
{
    [Fact]
    public async Task Handle_GrupoForaComJsonDescartado_RecusaGrupo()
    {
        JsonDocument documento = JsonDocument.Parse("\"valor\"");
        JsonElement valor = documento.RootElement;
        documento.Dispose();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> grupos = new()
        {
            ["FORA"] = [new("1", new Dictionary<string, JsonElement> { ["CAMPO"] = valor })],
        };
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync([], null, new Dictionary<string, JsonElement>(), grupos);
        resultado.Errors.Should().Contain(e => e.Error.Code == InscricaoErrorCodes.GrupoForaDoFormulario);
    }

    /// <summary>Um texto opcional que só aparece com <paramref name="pergunta"/> respondida "sim".</summary>
    private static FatoColetado SoComSim(string fato, int ordem, string pergunta) => FatoColetadoDeTeste.Coletando(fato, ordem)
        .DeTexto("LIVRE")
        .ExibidoQuando(ExibicaoDeTeste.Quando(pergunta, Operador.Igual, true))
        .ComClassificacao("SENSIVEL")
        .Construir();

    [Fact(DisplayName = "A resposta ao item que outra resposta esconde é descartada no envio: não fica gravada nem sai no comprovante, e o envio passa")]
    public async Task Handle_RespostaDeItemOculto_EDescartadaSemRecusar()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("PRECISA_ATENDIMENTO", 1), SoComSim("CONDICAO_DESCRITA", 2, "PRECISA_ATENDIMENTO")],
            grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["PRECISA_ATENDIMENTO"] = JsonSerializer.SerializeToElement(false),
                ["CONDICAO_DESCRITA"] = JsonSerializer.SerializeToElement("Lactante"),
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Emitido(inscricao).TodasAsRespostas().Should().ContainKey("PRECISA_ATENDIMENTO").And.NotContainKey("CONDICAO_DESCRITA");
        inscricao.Formulario.Preenchimento.Respostas.Should().NotContainKey("CONDICAO_DESCRITA", "a inscrição enviada guarda só o que se aplica");
    }

    [Fact(DisplayName = "Com sim e a conta sem nome social, o envio recusa o campo só com a orientação do Meu perfil, no lugar da obrigatoriedade genérica")]
    public async Task Handle_SimSemNomeSocialNaConta_RecusaComOrientacaoAntesDaObrigatoriedade()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(true) });

        resultado.Errors.Where(e => e.Field == "NOME_SOCIAL").Select(static e => e.Error.Code)
            .Should().Equal(InscricaoErrorCodes.NomeSocialAusenteNaConta);
        GravouComprovante(inscricao).Should().BeFalse("a recusa não grava comprovante");
    }

    [Fact(DisplayName = "Com sim e o nome social da conta, o envio passa e grava o nome social")]
    public async Task Handle_SimComNomeSocialDaConta_Aceita()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(true),
                ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeSocialFernandaDrumond),
            },
            nomeSocialDaConta: PersonasDeTeste.NomeSocialFernandaDrumond);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Formulario.Preenchimento.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Com não, o envio passa e não grava o nome social, ainda que a conta tenha um")]
    public async Task Handle_NaoDesejaNomeSocial_NaoGravaNomeSocial()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(false),
                ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeSocialFernandaDrumond),
            },
            nomeSocialDaConta: PersonasDeTeste.NomeSocialFernandaDrumond);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Formulario.Preenchimento.Respostas.Should().NotContainKey("NOME_SOCIAL");
        Emitido(inscricao).TodasAsRespostas().Should().NotContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "O grupo que outra resposta esconde é descartado inteiro no envio")]
    public async Task Handle_GrupoOculto_EDescartadoInteiro()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("TEM_DEPENDENTES", 1)],
            [GrupoDeDependentes()],
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["TEM_DEPENDENTES"] = JsonSerializer.SerializeToElement(false) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["DEPENDENTES"] = [new("dependente-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NOME_DEPENDENTE"] = JsonSerializer.SerializeToElement(PersonasDeTeste.Candidatas[2].Nome) })],
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Emitido(inscricao).Grupos.Should().NotContainKey("DEPENDENTES");
        inscricao.Formulario.Preenchimento.Grupos.Should().NotContainKey("DEPENDENTES");
    }

    [Fact(DisplayName = "No grupo que aparece, a resposta ao subitem que a ocorrência esconde é descartada, e a dos demais subitens fica")]
    public async Task Handle_SubitemOcultoNaOcorrencia_EDescartado()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [],
            [GrupoDeComposicao()],
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            RespostasDaComposicao());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Emitido(inscricao).Grupos["COMPOSICAO_FAMILIAR"].Single().Respostas.Should().ContainKey("MEMBRO_TEM_RENDA").And.NotContainKey("RENDA_DO_MEMBRO");
        inscricao.Formulario.Preenchimento.Grupos["COMPOSICAO_FAMILIAR"].Single().Respostas.Should().NotContainKey("RENDA_DO_MEMBRO");
    }

    private static GrupoColetado GrupoDeDependentes() => GrupoColetado.Criar(
        "DEPENDENTES", 2, FormularioDeTeste.Secao, "Dependentes", minimo: 0, maximo: null,
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("TEM_DEPENDENTES", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
        Obrigatoriedade.Nunca,
        [FatoColetadoDeTeste.Coletando("NOME_DEPENDENTE", 0).ComRotulo("Nome").DeTexto("LIVRE").ComObrigatoriedade(Obrigatoriedade.Sempre).Construir()],
        FinalidadeFormulario.Inscricao).Value!;

    private static GrupoColetado GrupoDeComposicao() => GrupoColetado.Criar(
        "COMPOSICAO_FAMILIAR", 1, FormularioDeTeste.Secao, "Composição familiar", minimo: 0, maximo: null, exibicao: null,
        Obrigatoriedade.Nunca,
        [Pergunta("MEMBRO_TEM_RENDA", 0), SoComSim("RENDA_DO_MEMBRO", 1, "MEMBRO_TEM_RENDA")],
        FinalidadeFormulario.Inscricao).Value!;

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> RespostasDaComposicao() => new(StringComparer.Ordinal)
    {
        ["COMPOSICAO_FAMILIAR"] = [new("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["MEMBRO_TEM_RENDA"] = JsonSerializer.SerializeToElement(false),
            ["RENDA_DO_MEMBRO"] = JsonSerializer.SerializeToElement("1500"),
        })],
    };
}
