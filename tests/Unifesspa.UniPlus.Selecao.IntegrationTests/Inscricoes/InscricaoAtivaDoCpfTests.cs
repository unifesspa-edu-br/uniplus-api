namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDaInscricaoGravada;

/// <summary>
/// Cada CPF tem uma só inscrição ativa por processo: o índice único recusa a segunda, a cancelada sai da busca da ativa e
/// libera uma nova, e o dono ainda alcança o arquivo da inscrição cancelada.
/// </summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class InscricaoAtivaDoCpfTests
{
    private readonly CenarioDaInscricaoGravada _cenario;

    public InscricaoAtivaDoCpfTests(InscricaoDbFixture fixture) => _cenario = new CenarioDaInscricaoGravada(fixture);

    [Fact(DisplayName = "Dois inícios simultâneos do mesmo CPF no mesmo processo colidem no índice único, não geram duas linhas")]
    public async Task IndiceUnico_RecusaDuasInscricoesDoMesmoCpfNoMesmoProcesso()
    {
        Guid processo = Guid.CreateVersion7();
        await _cenario.GravarAsync(NovaInscricao(PersonasDeTeste.FernandaDrumond, processo));

        await using UniPlusDbContext segundoContexto = _cenario.CriarDbContext();
        await _cenario.Repositorio(segundoContexto).AdicionarAsync(NovaInscricao(PersonasDeTeste.FernandaDrumond, processo));

        Func<Task> segundaGravacao = async () => await segundoContexto.SaveChangesAsync();
        await segundaGravacao.Should().ThrowAsync<DbUpdateException>(
            "o índice único (processo_seletivo_id, cpf_indice) recusa a segunda linha — quem chama traduz isso em 'devolver a existente', não um 500");
    }

    [Fact(DisplayName = "A inscrição cancelada é gravada com data, ator e motivo, sai da busca da ativa e libera nova inscrição, mas duas ativas seguem recusadas")]
    public async Task Cancelada_LiberaNovaInscricaoAtivaDoMesmoCpfNoMesmoProcesso()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao primeira = NovaInscricao(PersonasDeTeste.FernandaDrumond, processo);
        Enviar(primeira, "000777", new ConteudoDoEnvio { Respostas = Respostas(("NOME_SOCIAL", "Fê")), Derivados = Respostas(("UF_RESIDENCIA", "PA")) })
            .IsSuccess.Should().BeTrue();
        await _cenario.GravarAsync(primeira);
        DateTimeOffset canceladaEm = DateTimeOffset.UtcNow;
        await _cenario.CancelarAsync(primeira.Id, "Mudei de curso", canceladaEm);

        await ConferirCanceladaELiberacaoDeNovaAsync(primeira.Id, processo, canceladaEm);
        await ConferirQueDuasAtivasSeguemRecusadasAsync(processo);
    }

    [Fact(DisplayName = "O dono lê o arquivo da inscrição cancelada, que a busca da ativa não alcança; outra pessoa recebe a recusa do inexistente")]
    public async Task ArquivoDoDono_AlcancaAInscricaoCancelada()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao inscricao = NovaInscricao(PersonasDeTeste.FernandaDrumond, processo);
        Enviar(inscricao, "000778", new ConteudoDoEnvio { Respostas = Respostas(("NOME_SOCIAL", "Fê")), Derivados = Respostas(("UF_RESIDENCIA", "PA")) })
            .IsSuccess.Should().BeTrue();
        ArquivoDaApresentacao arquivo = await GravarComArquivoPendenteAsync(inscricao);
        await _cenario.CancelarAsync(inscricao.Id, null, DateTimeOffset.UtcNow);

        (await ResolverArquivoDoDonoAsync("conta-1", processo, arquivo.Id)).Value!.Id.Should().Be(arquivo.Id);
        (await ResolverArquivoDoDonoAsync("conta-2", processo, arquivo.Id)).Error.Should().Be(AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado);
    }

    private static DocumentoExigido LaudoMedico() =>
        DocumentoExigido.Criar(
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_laudo").Value!,
            new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal),
                new MomentoDaExigencia(Guid.CreateVersion7(), null, null),
                new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
                new IndeferimentoDoDocumento(false, null, []),
                RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(false, [("PDF", null)]).Value!),
                null)).Value!;

    private static void ConferirDadosDoCancelamento(Inscricao cancelada, DateTimeOffset canceladaEm)
    {
        cancelada.Estado.Situacao.Should().Be(SituacaoInscricao.Cancelada);
        cancelada.Estado.NumeroInscricao.Should().Be("000777");
        cancelada.Estado.Cancelamento?.CanceladaPor.Should().Be("conta-1");
        cancelada.Estado.Cancelamento?.MotivoDoCancelamento.Should().Be("Mudei de curso");
        cancelada.Estado.Cancelamento?.CanceladaEm.Should().BeCloseTo(canceladaEm, TimeSpan.FromMilliseconds(1));
    }

    private async Task ConferirCanceladaELiberacaoDeNovaAsync(Guid canceladaId, Guid processo, DateTimeOffset canceladaEm)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.Repositorio(contexto);
        ConferirDadosDoCancelamento((await repositorio.ObterPorIdAsync(canceladaId))!, canceladaEm);
        (await repositorio.ObterAtivaPorProcessoECpfAsync(processo, PersonasDeTeste.FernandaDrumond))
            .Should().BeNull("a cancelada não é a inscrição ativa");

        Inscricao nova = NovaInscricao(PersonasDeTeste.FernandaDrumond, processo);
        await repositorio.AdicionarAsync(nova);
        await contexto.SaveChangesAsync();
        (await repositorio.ObterAtivaPorProcessoECpfAsync(processo, PersonasDeTeste.FernandaDrumond))!.Id.Should().Be(nova.Id);
    }

    private async Task ConferirQueDuasAtivasSeguemRecusadasAsync(Guid processo)
    {
        await using UniPlusDbContext terceiroContexto = _cenario.CriarDbContext();
        await _cenario.Repositorio(terceiroContexto).AdicionarAsync(NovaInscricao(PersonasDeTeste.FernandaDrumond, processo));

        Func<Task> segundaAtiva = async () => await terceiroContexto.SaveChangesAsync();
        await segundaAtiva.Should().ThrowAsync<DbUpdateException>("o índice parcial segue recusando duas inscrições ativas");
    }

    private async Task<ArquivoDaApresentacao> GravarComArquivoPendenteAsync(Inscricao inscricao)
    {
        DocumentoExigido laudo = LaudoMedico();
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricao.Id, laudo, null);
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, inscricao.Id, laudo, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        await _cenario.Repositorio(contexto).AdicionarAsync(inscricao);
        DocumentoDaInscricaoRepository documentos = new(contexto);
        await documentos.AdicionarApresentacaoAsync(apresentacao);
        await documentos.AdicionarArquivoAsync(arquivo);
        await contexto.SaveChangesAsync();

        return arquivo;
    }

    private async Task<Result<ArquivoDaApresentacao>> ResolverArquivoDoDonoAsync(string contaId, Guid processo, Guid arquivoId)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        IUserContext usuario = Substitute.For<IUserContext>();
        usuario.UserId.Returns(contaId);
        usuario.Cpf.Returns(PersonasDeTeste.FernandaDrumond.Valor);

        return await new AcessoAoDocumentoDaInscricao(_cenario.Repositorio(contexto), new DocumentoDaInscricaoRepository(contexto), usuario,
            new PeriodoDaInscricaoResolver(Substitute.For<ICertameDivulgadoRepository>(), Substitute.For<IProcessoSeletivoRepository>(),
                CenarioDeInscricaoPublicada.RegistroCodecs, CenarioDeInscricaoPublicada.Fuso()), TimeProvider.System)
            .ResolverArquivoDoDonoAsync(processo, arquivoId, CancellationToken.None);
    }
}
