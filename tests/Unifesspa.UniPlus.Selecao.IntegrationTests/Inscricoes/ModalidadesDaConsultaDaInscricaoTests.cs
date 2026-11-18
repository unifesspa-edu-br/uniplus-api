namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessoComMatrizDaLeiDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessosDeInscricaoDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>As modalidades em que o candidato concorre, derivadas das respostas e da matriz normativa congelada, na consulta, no salvar e no ler da inscrição.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class ModalidadesDaConsultaDaInscricaoTests
{
    private readonly CenarioDeInscricaoPublicada _cenario;

    public ModalidadesDaConsultaDaInscricaoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "A consulta da inscrição sobre o processo publicado devolve as modalidades das respostas, o que falta e o nome congelado, sem gravar nada")]
    public async Task Consulta_SobreVersaoFixadaReal_DevolveAsModalidadesSemGravar()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, ProcessoQueDerivaAModalidade(nameof(Consulta_SobreVersaoFixadaReal_DevolveAsModalidadesSemGravar)));
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(db, processoId, fernanda);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        iniciar.Value!.Avaliacao.Modalidades.Should().BeEquivalentTo(new ModalidadesDaInscricaoDto(
            true, "FALTA_RESPONDER", [],
            [new PendenciaDaAvaliacaoDto("CONCORRER_PCD", "Você quer concorrer às vagas para pessoa com deficiência?", FormularioDeTeste.Secao)]));

        await ConferirModalidadesAsync(db, processoId, fernanda);

        await ConferirQueConsultaNaoGravouAsync(iniciar.Value!);
    }

    private async Task ConferirQueConsultaNaoGravouAsync(InscricaoDto inscricao)
    {
        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        Inscricao gravada = (await _cenario.NovoRepositorioDeInscricao(leitura)
            .ObterPorIdAsync(inscricao.Id))!;
        gravada.Formulario.Preenchimento.Respostas.Should().NotContainKey("CONCORRER_PCD", "a consulta não grava nada");
        gravada.ETag.Should().Be(inscricao.ETag, "nenhuma consulta muda a revisão da inscrição");
    }

    private async Task ConferirModalidadesAsync(UniPlusDbContext db, Guid processoId, IUserContext usuario)
    {
        Result<AvaliacaoDaInscricaoDto> concorre = await _cenario.AvaliarAsync(db, processoId, usuario, new(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true),
        });
        Result<AvaliacaoDaInscricaoDto> naoConcorre = await _cenario.AvaliarAsync(db, processoId, usuario, new(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false),
        });

        concorre.IsSuccess.Should().BeTrue(concorre.Error?.Message);
        concorre.Value!.Modalidades.Situacao.Should().Be("CONCORRE");
        concorre.Value!.Modalidades.Itens.Should().Equal(new ModalidadeDaInscricaoDto("AC", "AC", 0));
        naoConcorre.Value!.Modalidades.Situacao.Should().Be("NAO_CONCORRE");
        naoConcorre.Value!.Modalidades.Itens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sobre o processo que só oferta a ampla, incondicional, a consulta diz que o candidato concorre a ela")]
    public async Task Consulta_SoAmpla_ConcorreAAmpla()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, NovoProcessoDeInscricao(nameof(Consulta_SoAmpla_ConcorreAAmpla)));
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        (await _cenario.IniciarRascunhoAsync(db, processoId, fernanda)).IsSuccess.Should().BeTrue();

        Result<AvaliacaoDaInscricaoDto> avaliacao = await _cenario.AvaliarAsync(db, processoId, fernanda, null);

        avaliacao.Value!.Modalidades.Derivada.Should().BeTrue("a modalidade sai das modalidades ofertadas");
        avaliacao.Value.Modalidades.Situacao.Should().Be("CONCORRE");
        avaliacao.Value.Modalidades.Itens.Select(static m => m.Codigo).Should().Equal(["AC"]);
    }

    [Fact(DisplayName = "A consulta só responde ao titular: sem inscrição no processo é não encontrada, e a conta divergente é dono divergente")]
    public async Task Consulta_SoAoTitular()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, ProcessoQueDerivaAModalidade(nameof(Consulta_SoAoTitular)));
        (await _cenario.IniciarRascunhoAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond))).IsSuccess.Should().BeTrue();

        IUserContext mesmoCpfOutroContaId = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        mesmoCpfOutroContaId.UserId.Returns("conta-de-outra-conta");

        (await _cenario.AvaliarAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.KevinCarvalho), null)).Error!.Code
            .Should().Be(InscricaoErrorCodes.NaoEncontrada);
        (await _cenario.AvaliarAsync(db, processoId, mesmoCpfOutroContaId, null)).Error!.Code
            .Should().Be(InscricaoErrorCodes.DonoDivergente);
    }

    [Fact(DisplayName = "Sobre a versão publicada com a matriz da Lei 12.711, a consulta deriva as modalidades da origem escolar e da opção pelas cotas")]
    public async Task Consulta_ComAMatrizNormativaPublicada_DerivaAsModalidadesDaLei()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(
            db, ProcessoComAMatrizNormativa(nameof(Consulta_ComAMatrizNormativaPublicada_DerivaAsModalidadesDaLei)), OpcoesDaOrigemEscolar());
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(db, processoId, fernanda);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        await ConferirModalidadesDaLeiAsync(db, processoId, fernanda);
    }

    private async Task ConferirModalidadesDaLeiAsync(UniPlusDbContext db, Guid processoId, IUserContext usuario)
    {
        ModalidadesDaInscricaoDto comCotas = (await _cenario.AvaliarAsync(db, processoId, usuario,
            RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true))).Value!.Modalidades;
        ModalidadesDaInscricaoDto semCotas = (await _cenario.AvaliarAsync(db, processoId, usuario,
            RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: false, ppi: false, renda: false))).Value!.Modalidades;
        ModalidadesDaInscricaoDto semOrigem = (await _cenario.AvaliarAsync(db, processoId, usuario,
            RespostasDaOrigemEDasCotas(ondeCursou: null, ep: true, ppi: false, renda: false))).Value!.Modalidades;

        comCotas.Situacao.Should().Be("CONCORRE");
        comCotas.Itens.Select(static m => m.Codigo).Should().Equal("AC", "LB_EP", "LB_PPI", "LI_EP", "LI_PPI");
        comCotas.Itens.Single(static m => m.Codigo == "AC").Nome.Should().Be("Ampla concorrência");
        semCotas.Itens.Select(static m => m.Codigo).Should().Equal("AC");
        semOrigem.Situacao.Should().Be("FALTA_RESPONDER");
        semOrigem.Pendencias.Select(static p => p.FatoCodigo).Should().Equal(OrigemEscolar.FatoFormaDeConclusao, OrigemEscolar.FatoOndeCursou);
    }

    [Fact(DisplayName = "Salvar e ler a inscrição devolvem a avaliação do que ficou gravado, com as modalidades que a resposta decide")]
    public async Task SalvarELer_DevolvemAAvaliacaoDoGravado()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, ProcessoQueDerivaAModalidade(nameof(SalvarELer_DevolvemAAvaliacaoDoGravado)));
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(db, processoId, fernanda);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        Result<InscricaoDto> salvar = await SalvarRespostaAsync(db, processoId, iniciar.Value!.ETag);
        Result<InscricaoDto> ler = await _cenario.ObterAsync(db, processoId, fernanda);

        salvar.IsSuccess.Should().BeTrue(salvar.Error?.Message);
        ModalidadesDaInscricaoDto concorre = new(true, "CONCORRE", [new ModalidadeDaInscricaoDto("AC", "AC", 0)], []);
        salvar.Value!.Avaliacao.Modalidades.Should().BeEquivalentTo(concorre);
        ler.Value!.Avaliacao.Modalidades.Should().BeEquivalentTo(concorre);
    }

    private async Task<Result<InscricaoDto>> SalvarRespostaAsync(UniPlusDbContext db, Guid processoId, string etag)
    {
        return await _cenario.SalvarAsync(
            db, processoId,
            new InscricaoRequest(null, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true),
            }, null, null),
            etag, Substitute.For<IConferenciaEnderecoGeo>());
    }
}
