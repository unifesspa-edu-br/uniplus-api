namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDaInscricaoGravada;

/// <summary>
/// A concorrência otimista da inscrição gravada (ADR-0119): a revisão do registro é o token. Quem grava com a revisão
/// que já foi superada, ou remove o que outra requisição já removeu, recebe o conflito, e a revisão só avança de uma revisão
/// lida e para a inscrição que não foi cancelada.
/// </summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class ConcorrenciaDaInscricaoGravadaTests
{
    private readonly CenarioDaInscricaoGravada _cenario;

    public ConcorrenciaDaInscricaoGravadaTests(InscricaoDbFixture fixture) => _cenario = new CenarioDaInscricaoGravada(fixture);

    [Fact(DisplayName = "Remover uma inscrição que outra requisição já removeu é conflito de concorrência, não falha do servidor")]
    public async Task Remover_InscricaoJaRemovida_LancaConflitoDeConcorrencia()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.Repositorio(contexto);

        Func<Task> remover = () => repositorio.RemoverAsync(original);

        await remover.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact(DisplayName = "Atualizar um agregado lido cuja revisão outra requisição já avançou é conflito de concorrência")]
    public async Task Atualizar_RevisaoDoRegistroAvancadaPorOutraRequisicao_LancaConflitoDeConcorrencia()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        await _cenario.GravarAsync(original);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.Repositorio(contexto);
        Inscricao lida = (await repositorio.ObterPorIdAsync(original.Id))!;
        (await TentarAvancarARevisaoAsync(original.Id, lida.Revisao)).Should().BeTrue();

        lida.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(("NOME_SOCIAL", "Ariel")), SemGrupos(), []), Admitidas, PrecondicaoDe(lida)).IsSuccess.Should().BeTrue();
        await repositorio.AtualizarAsync(lida);

        Func<Task> gravar = () => contexto.SaveChangesAsync();
        await gravar.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact(DisplayName = "TentarAvancarRevisao devolve falso quando a revisão lida já foi superada e quando a inscrição foi cancelada")]
    public async Task TentarAvancarRevisao_RevisaoVencidaOuInscricaoCancelada_DevolveFalso()
    {
        Inscricao enviada = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        Enviar(enviada, "000902").IsSuccess.Should().BeTrue();
        await _cenario.GravarAsync(enviada);

        (await TentarAvancarARevisaoAsync(enviada.Id, enviada.Revisao)).Should().BeTrue("a revisão lida é a gravada");
        (await TentarAvancarARevisaoAsync(enviada.Id, enviada.Revisao)).Should().BeFalse("a revisão lida já foi superada");

        await _cenario.CancelarAsync(enviada.Id, null, DateTimeOffset.UtcNow);
        Inscricao cancelada = await _cenario.LerAsync(enviada.Id);

        (await TentarAvancarARevisaoAsync(cancelada.Id, cancelada.Revisao)).Should().BeFalse("a inscrição cancelada não avança a revisão");
        (await _cenario.LerAsync(cancelada.Id)).Revisao.Should().Be(cancelada.Revisao);
    }

    private async Task<bool> TentarAvancarARevisaoAsync(Guid inscricaoId, int revisaoLida)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();

        return await _cenario.Repositorio(contexto).TentarAvancarRevisaoAsync(inscricaoId, revisaoLida, apenasRascunho: false);
    }
}
