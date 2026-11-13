namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

/// <summary>A providência como a API a expõe, na leitura e na trilha da concessão.</summary>
public sealed class ProvidenciaMappingTests
{
    private static readonly DateTimeOffset Agora = ProvidenciaBuilder.Agora;

    [Fact(DisplayName = "A aberta cuja concessão foi revogada sai prejudicada, com a revogação como encerramento")]
    public void Detalhe_Prejudicada_TrazOEncerramento()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();
        cenario.Concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddMinutes(5)), "Revogada pela revogação comum.");

        ProvidenciaDto dto = ProvidenciaMapping.ToDto(Item(p, cenario), Agora.AddMinutes(10), ProvidenciaBuilder.Diogo);

        (dto.Situacao, dto.EncerramentoDaConcessao, dto.Concessao.Ativa).Should().Be(("prejudicada", new EncerramentoDaConcessaoDto("revogada", Agora.AddMinutes(5)), false));
        (dto.Fundamento, dto.Desfecho, dto.ImpedidoDeResponder).Should().Be((ProvidenciaBuilder.Fundamento, (DesfechoDaProvidenciaDto?)null, false));
    }

    [Fact(DisplayName = "A aberta cuja concessão venceu sai prejudicada, com o vencimento como encerramento")]
    public void Detalhe_Vencida_TrazOVencimento()
    {
        ProvidenciaBuilder cenario = new ProvidenciaBuilder().SobreA(ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1)));

        ProvidenciaDto dto = ProvidenciaMapping.ToDto(Item(cenario.Aberta(), cenario), Agora.AddHours(2), null);

        (dto.Situacao, dto.EncerramentoDaConcessao).Should().Be(("prejudicada", new EncerramentoDaConcessaoDto("vencida", Agora.AddHours(1))));
    }

    [Fact(DisplayName = "O item da fila não traz o fundamento nem o motivo do desfecho, e marca quem lê se está impedido")]
    public void Resumo_SemTextoLivre()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Respondida();

        ProvidenciaResumoDto resumo = ProvidenciaMapping.ToResumo(Item(p, cenario), Agora, ProvidenciaBuilder.Cecilia.Identidade);

        (resumo.Situacao, resumo.Desfecho, resumo.ImpedidoDeResponder).Should().Be(("respondida", new AtoDoDesfechoDto(Agora, new IdentidadeDto(ProvidenciaBuilder.Emissor, "diogo.souza", ProvidenciaBuilder.Diogo.Nome)), true));
        resumo.EncerramentoDaConcessao.Should().BeNull();
    }

    [Fact(DisplayName = "A trilha da concessão mostra a solicitação, com o tipo e o fundamento, e o atendimento, com o motivo")]
    public void Trilha_SolicitacaoEAtendimento()
    {
        Providencia p = new ProvidenciaBuilder().DoTipo("revisao").Atendida();

        EventoDaTrilhaDto[] eventos = [.. ProvidenciaMapping.EventosDaTrilha(p)];

        eventos.Select(static e => (e.Tipo, e.Detalhe)).Should().Equal(
            ("providencia_solicitada", $"Revisão do acesso. Fundamento: {ProvidenciaBuilder.Fundamento}"), ("providencia_atendida", ProvidenciaBuilder.Motivo));
        eventos.Select(static e => e.Autor!.Sub).Should().Equal(ProvidenciaBuilder.Cecilia.Identidade.Sub, ProvidenciaBuilder.Diogo.Sub);
    }

    [Fact(DisplayName = "A trilha da concessão mostra a resposta, e a aberta só a solicitação")]
    public void Trilha_RespostaEAberta()
    {
        ProvidenciaMapping.EventosDaTrilha(new ProvidenciaBuilder().Respondida()).Select(static e => e.Tipo).Should().Equal("providencia_solicitada", "providencia_respondida");
        ProvidenciaMapping.EventosDaTrilha(new ProvidenciaBuilder().Aberta()).Select(static e => e.Tipo).Should().Equal("providencia_solicitada");
    }

    [Fact(DisplayName = "A trilha da própria concessão mostra o vencimento como evento do sistema, e a revogação no lugar dele quando revogada")]
    public void TrilhaDaConcessao_VencimentoERevogacao()
    {
        Concessao vencida = ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1));
        Concessao revogada = ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1));
        revogada.Revogar(ProvidenciaBuilder.AtoDeDiogo(), "Mudou de função.");

        EventoDaTrilhaDto fim = ConcessaoMapping.EventosDaConcessao(vencida, Agora.AddHours(2)).Last();
        (fim.Tipo, fim.Instante, fim.Autor, fim.Ator).Should().Be(("vencida", Agora.AddHours(1), (IdentidadeDto?)null, "sistema"));
        ConcessaoMapping.EventosDaConcessao(revogada, Agora.AddHours(2)).Select(static e => e.Tipo).Should().Equal("concedida", "revogada");
        ConcessaoMapping.EventosDaConcessao(vencida, Agora).Select(static e => e.Tipo).Should().Equal("concedida");
    }

    private static ProvidenciaComConcessao Item(Providencia p, ProvidenciaBuilder cenario) =>
        new(p, new ConcessaoDaProvidencia(cenario.Concessao, CenarioDaProvidencia.PerfilNome));
}
