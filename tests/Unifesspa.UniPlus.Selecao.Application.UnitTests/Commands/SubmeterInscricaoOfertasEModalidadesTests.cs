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
/// O envio conforme as ofertas de curso escolhidas e as modalidades a que o candidato concorre.
/// </summary>
public sealed class SubmeterInscricaoOfertasEModalidadesTests
{
    [Fact(DisplayName = "O envio com a oferta A concorre só às modalidades da oferta A, mesmo com o rascunho gravado na oferta B")]
    public async Task Handle_OfertaDoCorpo_ConcorreSoAsModalidadesDaOfertaEnviada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Guid ofertaA = Guid.CreateVersion7();
        Guid ofertaB = Guid.CreateVersion7();
        GrafoConfiguracao grafo = OfertasDeTeste.ComDistribuicao(
            grafoBase, OfertasDeTeste.Distribuicao(ofertaA, "AC"), OfertasDeTeste.Distribuicao(ofertaB, "AC", "AC_FEM"));
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);
        existente.Salvar(
            ofertaB, null, new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [],
            Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver.Admitidas(grafo) with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([existente.ETag])).IsSuccess.Should().BeTrue();

        Result<ReciboDaInscricaoDto> daOfertaA = await SubmeterNaOfertaAsync(cpf, versao, grafo, termo, existente, ofertaA);

        daOfertaA.IsSuccess.Should().BeTrue(daOfertaA.Error?.Message);
        Emitido(existente).OfertaCursoId.Should().Be(ofertaA);
        Emitido(existente).Modalidades!.Select(static m => m.Codigo).Should().Equal(
            ["AC"], "a modalidade que só a oferta B tem não vale para quem enviou na oferta A");
    }

    [Fact(DisplayName = "O envio com a oferta B concorre também à modalidade que só a oferta B tem")]
    public async Task Handle_OfertaDoCorpo_ConcorreAsModalidadesDaPropriaOferta()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Guid ofertaA = Guid.CreateVersion7();
        Guid ofertaB = Guid.CreateVersion7();
        GrafoConfiguracao grafo = OfertasDeTeste.ComDistribuicao(
            grafoBase, OfertasDeTeste.Distribuicao(ofertaA, "AC"), OfertasDeTeste.Distribuicao(ofertaB, "AC", "AC_FEM"));

        Inscricao inscricao = NovaInscricao(cpf, "conta-1", versao.Id);
        Result<ReciboDaInscricaoDto> daOfertaB = await SubmeterNaOfertaAsync(cpf, versao, grafo, termo, inscricao, ofertaB);

        daOfertaB.IsSuccess.Should().BeTrue(daOfertaB.Error?.Message);
        Emitido(inscricao).Modalidades!.Select(static m => m.Codigo).Should().Equal("AC", "AC_FEM");
    }

    [Fact(DisplayName = "O envio com 1ª e 2ª opção concorre às modalidades da distribuição de cada oferta, na versão que declara duas opções")]
    public async Task Handle_DuasOpcoes_ConcorreAsModalidadesDeCadaOferta()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Guid direito = Guid.CreateVersion7();
        Guid computacao = Guid.CreateVersion7();
        GrafoConfiguracao grafo = OfertasDeTeste.ComOpcoesDeCurso(
            OfertasDeTeste.ComDistribuicao(grafoBase, OfertasDeTeste.Distribuicao(direito, "AC", "AC_FEM"), OfertasDeTeste.Distribuicao(computacao, "AC")),
            numeroDeOpcoesDeCurso: 2);

        Inscricao inscricao = NovaInscricao(cpf, "conta-1", versao.Id);
        Result<ReciboDaInscricaoDto> resultado = await SubmeterNaOfertaAsync(cpf, versao, grafo, termo, inscricao, direito, computacao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        ComprovanteInscricao comprovante = Emitido(inscricao);
        comprovante.OfertaCursoId.Should().Be(direito);
        comprovante.OfertaCursoSegundaOpcaoId.Should().Be(computacao);
        comprovante.Modalidades!.Select(static m => m.Codigo).Should().Equal("AC", "AC_FEM");
        comprovante.ModalidadesSegundaOpcao!.Select(static m => m.Codigo).Should().Equal("AC");
    }

    [Fact(DisplayName = "A 2ª opção é recusada quando a versão que a inscrição fixou declara uma opção só")]
    public async Task Handle_SegundaOpcaoEmVersaoFixadaDeUmaOpcao_Recusa()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Guid direito = Guid.CreateVersion7();
        Guid computacao = Guid.CreateVersion7();
        GrafoConfiguracao grafo = OfertasDeTeste.ComDistribuicao(
            grafoBase, OfertasDeTeste.Distribuicao(direito, "AC"), OfertasDeTeste.Distribuicao(computacao, "AC"));

        Inscricao inscricao = NovaInscricao(cpf, "conta-1", versao.Id);
        Result<ReciboDaInscricaoDto> resultado = await SubmeterNaOfertaAsync(cpf, versao, grafo, termo, inscricao, direito, computacao);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoSegundaOpcaoId" && e.Error.Code == InscricaoErrorCodes.SegundaOpcaoNaoAdmitida);
    }

    [Fact(DisplayName = "Sem nenhuma modalidade aplicável, o envio é recusado no campo de que ela depende, sem gravar nada")]
    public async Task Handle_NenhumaModalidade_RecusaNoCampoQueADecide()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("CONCORRER_PCD", 1)], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false) },
            ajustar: AmplaSoComDeficiencia);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("CONCORRER_PCD");
        resultado.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.NenhumaModalidade);
        inscricao.Situacao.Should().Be(Domain.Enums.SituacaoInscricao.Rascunho);
        inscricao.Comprovantes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com o Geo fora do ar, a modalidade decidida em que o candidato não concorre a nenhuma segue como recusa 422")]
    public async Task Handle_NenhumaModalidadeComGeoForaDoAr_Recusa422()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [Pergunta("CONCORRER_PCD", 1)], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false) },
            ajustar: AmplaSoComDeficiencia, geo: GeoQueConhece().Indisponivel());

        resultado.Errors.Should().ContainSingle(e => e.Field == "CONCORRER_PCD" && e.Error.Code == InscricaoErrorCodes.NenhumaModalidade);
    }

    [Fact(DisplayName = "Com o Geo fora do ar, a modalidade que deriva da residência e fica sem decidir não é recusa: o envio devolve indisponível")]
    public async Task Handle_ModalidadeQueDependeDaResidenciaComGeoForaDoAr_Recusa503()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], grupos: null, Texto(),
            ajustar: processo => processo.DefinirDistribuicaoVagas(
                [ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(PredicadoDnf.CriarDeCondicoesAgrupadas(
                    [(0, CondicaoDnf.Criar("MUNICIPIO_RESIDENCIA", Operador.Igual, JsonSerializer.SerializeToElement(Belem)).Value!)]).Value!)],
                FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(),
            geo: GeoQueConhece().Indisponivel());

        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
        inscricao.EhRascunho.Should().BeTrue();
    }

    [Fact(DisplayName = "Com a modalidade aplicável, o envio a grava como derivada e o comprovante a traz com o nome congelado")]
    public async Task Handle_ComModalidade_GravaEComprova()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("CONCORRER_PCD", 1)], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true) },
            ajustar: AmplaSoComDeficiencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Emitido(inscricao).Modalidades.Should().Equal(new ModalidadeDeConcorrencia("AC", "AC", 0));
        inscricao.Derivados["MODALIDADE"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC");
        inscricao.Comprovantes.Should().ContainSingle().Which.IntegridadeConfere().Should().BeTrue();
    }

    [Fact(DisplayName = "A modalidade enviada pelo candidato é recusada e nunca gravada")]
    public async Task Handle_ModalidadeEnviadaPeloCandidato_Recusa()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("CONCORRER_PCD", 1)], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true),
                ["MODALIDADE"] = JsonSerializer.SerializeToElement(new[] { "AC" }),
            },
            ajustar: AmplaSoComDeficiencia);

        resultado.Errors.Should().Contain(e => e.Field == "MODALIDADE" && e.Error.Code == InscricaoErrorCodes.FatoForaDoFormulario);
        inscricao.Derivados.Should().NotContainKey("MODALIDADE");
        inscricao.Respostas.Should().NotContainKey("MODALIDADE");
    }
}
