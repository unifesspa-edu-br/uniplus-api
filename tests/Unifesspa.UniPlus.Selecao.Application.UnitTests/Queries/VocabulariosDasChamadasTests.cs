namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

/// <summary>As opções que a configuração das chamadas oferece à tela: a mesma lista que a gravação aceita, com o código que o contrato publica.</summary>
public sealed class VocabulariosDasChamadasTests
{
    [Fact(DisplayName = "As participações da reserva oferecidas são todas as do enum, na ordem declarada, cada uma com o rótulo e a descrição")]
    public void ParticipacoesDaReserva_OferecemTodasComDescricao()
    {
        IReadOnlyList<CodigoComDescricao<ParticipacaoDaReserva>> opcoes = ListarParticipacoesDaReservaQueryHandler.Handle(new ListarParticipacoesDaReservaQuery());

        opcoes.Select(static o => o.Codigo).Should().Equal("ANTECIPA_A_HABILITACAO", "ASSUME_NA_MESMA_CHAMADA");
        opcoes.Should().OnlyContain(static o => !string.IsNullOrWhiteSpace(o.Rotulo) && !string.IsNullOrWhiteSpace(o.Descricao));
    }

    [Fact(DisplayName = "As manifestações de interesse oferecidas são todas as do enum, na ordem declarada, cada uma com o rótulo e a descrição")]
    public void ManifestacoesDeInteresse_OferecemTodasComDescricao()
    {
        IReadOnlyList<CodigoComDescricao<ManifestacaoDeInteresse>> opcoes = ListarManifestacoesDeInteresseQueryHandler.Handle(new ListarManifestacoesDeInteresseQuery());

        opcoes.Select(static o => o.Codigo).Should().Equal("UMA_VEZ", "CONFIRMA_A_CADA_CHAMADA");
        opcoes.Should().OnlyContain(static o => !string.IsNullOrWhiteSpace(o.Rotulo) && !string.IsNullOrWhiteSpace(o.Descricao));
    }

    [Fact(DisplayName = "As leituras exigem a pergunta")]
    public void Leituras_SemPergunta_Lancam()
    {
        FluentActions.Invoking(() => ListarParticipacoesDaReservaQueryHandler.Handle(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ListarManifestacoesDeInteresseQueryHandler.Handle(null!)).Should().Throw<ArgumentNullException>();
    }
}
