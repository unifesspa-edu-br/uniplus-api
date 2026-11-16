namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.SubmeterDeTeste;

/// <summary>
/// O envio descarta a resposta que outra resposta esconde, e trata o nome social e os itens de outra finalidade.
/// </summary>
public sealed class SubmeterInscricaoRespostasEscondidasTests
{
    [Fact(DisplayName = "O item e o termo obrigatórios de outra finalidade, como a isenção, não recusam o envio da inscrição")]
    public async Task Handle_ObrigatoriosDaIsencao_NaoRecusamOEnvio()
    {
        VersaoTermoEscolhida versaoDoTermo = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Termo da isenção", "Declaro a renda informada.",
            "Lei 12.799/2013", "ACEITE_EXPLICITO", new string('d', 64));

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], grupos: null, new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            ajustar: processo =>
            {
                FormularioDeTeste.DefinirItens(
                    processo,
                    [FatoColetado.Criar(
                        "RENDA_DECLARADA", 0, "Renda declarada", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                        classificacaoProtecao: "PESSOAL", formato: "LIVRE", finalidade: FinalidadeFormulario.IsencaoTaxa, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
                    finalidade: FinalidadeFormulario.IsencaoTaxa).IsSuccess.Should().BeTrue();
                FormularioDeTeste.DefinirTermos(
                    processo,
                    [TermoExigidoFormulario.Criar("TERMO_ISENCAO", 0, versaoDoTermo, exibicao: null, Obrigatoriedade.Sempre, FinalidadeFormulario.IsencaoTaxa).Value!],
                    finalidade: FinalidadeFormulario.IsencaoTaxa).IsSuccess.Should().BeTrue();
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

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
        inscricao.Respostas.Should().NotContainKey("CONDICAO_DESCRITA", "a inscrição enviada guarda só o que se aplica");
    }

    [Fact(DisplayName = "Com sim e a conta sem nome social, o envio recusa o campo só com a orientação do Meu perfil, no lugar da obrigatoriedade genérica")]
    public async Task Handle_SimSemNomeSocialNaConta_RecusaComOrientacaoAntesDaObrigatoriedade()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(true) });

        resultado.Errors.Where(e => e.Field == "NOME_SOCIAL").Select(static e => e.Error.Code)
            .Should().Equal(InscricaoErrorCodes.NomeSocialAusenteNaConta);
        inscricao.Comprovantes.Should().BeEmpty();
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
        inscricao.Respostas.Should().ContainKey("NOME_SOCIAL");
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
        inscricao.Respostas.Should().NotContainKey("NOME_SOCIAL");
        Emitido(inscricao).TodasAsRespostas().Should().NotContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "O grupo que outra resposta esconde é descartado inteiro no envio")]
    public async Task Handle_GrupoOculto_EDescartadoInteiro()
    {
        GrupoColetado dependentes = GrupoColetado.Criar(
            "DEPENDENTES", 2, FormularioDeTeste.Secao, "Dependentes", minimo: 0, maximo: null,
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("TEM_DEPENDENTES", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            Obrigatoriedade.Nunca,
            [FatoColetado.Criar("NOME_DEPENDENTE", 0, "Nome", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            FinalidadeFormulario.Inscricao).Value!;

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("TEM_DEPENDENTES", 1)],
            [dependentes],
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["TEM_DEPENDENTES"] = JsonSerializer.SerializeToElement(false) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["DEPENDENTES"] = [new("dependente-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NOME_DEPENDENTE"] = JsonSerializer.SerializeToElement(PersonasDeTeste.Candidatas[2].Nome) })],
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Emitido(inscricao).Grupos.Should().NotContainKey("DEPENDENTES");
        inscricao.Grupos.Should().NotContainKey("DEPENDENTES");
    }

    [Fact(DisplayName = "No grupo que aparece, a resposta ao subitem que a ocorrência esconde é descartada, e a dos demais subitens fica")]
    public async Task Handle_SubitemOcultoNaOcorrencia_EDescartado()
    {
        GrupoColetado composicao = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", 1, FormularioDeTeste.Secao, "Composição familiar", minimo: 0, maximo: null, exibicao: null,
            Obrigatoriedade.Nunca,
            [Pergunta("MEMBRO_TEM_RENDA", 0), SoComSim("RENDA_DO_MEMBRO", 1, "MEMBRO_TEM_RENDA")],
            FinalidadeFormulario.Inscricao).Value!;

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [],
            [composicao],
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] = [new("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["MEMBRO_TEM_RENDA"] = JsonSerializer.SerializeToElement(false),
                    ["RENDA_DO_MEMBRO"] = JsonSerializer.SerializeToElement("1500"),
                })],
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Emitido(inscricao).Grupos["COMPOSICAO_FAMILIAR"].Single().Respostas.Should().ContainKey("MEMBRO_TEM_RENDA").And.NotContainKey("RENDA_DO_MEMBRO");
        inscricao.Grupos["COMPOSICAO_FAMILIAR"].Single().Respostas.Should().NotContainKey("RENDA_DO_MEMBRO");
    }
}
